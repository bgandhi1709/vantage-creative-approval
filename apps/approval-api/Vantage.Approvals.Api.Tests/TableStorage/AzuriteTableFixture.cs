using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Azure.Data.Tables;

namespace Vantage.Approvals.Api.Tests.TableStorage;

/// <summary>
/// Runs the storage repository tests against a real Azurite table endpoint instead of an
/// in-memory fake.
/// </summary>
/// <remarks>
/// The bugs this layer actually produces are emulator-visible and fake-invisible:
/// <list type="bullet">
///   <item>DateTimeOffset values come back normalized to UTC, so a local-time write round-trips
///   to a different value than it was given.</item>
///   <item>PartitionKey and RowKey comparison is case-sensitive, so a GUID written in one format
///   is simply not found when read in another.</item>
///   <item>ETag concurrency is enforced by the service, not by the client library.</item>
/// </list>
/// A dictionary-backed fake passes all three and tells you nothing.
/// </remarks>
public sealed class AzuriteTableFixture : IAsyncLifetime, IDisposable
{
    /// <summary>
    /// Azurite's well-known development account. Not a secret: the emulator accepts only this
    /// account, and the same key ships in the Azure SDK's own samples.
    /// </summary>
    private const string EmulatorAccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private readonly StringBuilder output = new();
    private Process? process;
    private string dataDirectory = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var port = GetFreeTcpPort();
        this.dataDirectory = Directory.CreateTempSubdirectory("vantage-azurite-").FullName;

        this.ConnectionString = string.Create(
            CultureInfo.InvariantCulture,
            $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
                + $"AccountKey={EmulatorAccountKey};"
                + $"TableEndpoint=http://127.0.0.1:{port}/devstoreaccount1;"
        );

        var startInfo = new ProcessStartInfo("azurite-table")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--tableHost");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--tablePort");
        startInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--location");
        startInfo.ArgumentList.Add(this.dataDirectory);
        startInfo.ArgumentList.Add("--silent");

        try
        {
            this.process = new Process { StartInfo = startInfo };

            // Drain both pipes asynchronously: a full pipe buffer would block the emulator, and
            // the captured text is what a start-up failure is reported with.
            this.process.OutputDataReceived += (_, args) => this.output.AppendLine(args.Data);
            this.process.ErrorDataReceived += (_, args) => this.output.AppendLine(args.Data);

            this.process.Start();
            this.process.BeginOutputReadLine();
            this.process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start 'azurite-table'. Install it with 'npm install -g azurite'.",
                ex
            );
        }

        await this.WaitUntilReadyAsync().ConfigureAwait(false);
    }

    public Task DisposeAsync()
    {
        this.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (this.process is { HasExited: false })
        {
            this.process.Kill(entireProcessTree: true);
            this.process.WaitForExit(5_000);
        }

        this.process?.Dispose();
        this.process = null;

        if (Directory.Exists(this.dataDirectory))
        {
            Directory.Delete(this.dataDirectory, recursive: true);
        }
    }

    public TableServiceClient CreateClient() => new(this.ConnectionString);

    /// <summary>Polls a throwaway table until the emulator answers, or gives up after 20 seconds.</summary>
    private async Task WaitUntilReadyAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        var probe = this.CreateClient().GetTableClient("startupprobe");
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await probe.CreateIfNotExistsAsync().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (DateTimeOffset.UtcNow < deadline)
            {
                lastError = ex;
                await Task.Delay(250).ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            "Azurite did not become ready within 20 seconds. "
                + $"Exited: {this.process?.HasExited}. Output: {this.output}. "
                + $"Last probe error: {lastError?.Message}"
        );
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[CollectionDefinition(Name)]
public sealed class AzuriteTableCollection : ICollectionFixture<AzuriteTableFixture>
{
    public const string Name = "azurite-table";
}
