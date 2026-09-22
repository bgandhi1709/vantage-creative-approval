using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Vantage.Approvals.Api.Configuration;
using Vantage.Approvals.Api.Services.Interfaces;

namespace Vantage.Approvals.Api.Services;

internal sealed class BlobReviewSnapshotStore : IReviewSnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Enums by name, matching the table rows. A snapshot is read back years later by a person,
        // and "AwaitingDecision" survives a renumbering that "1" does not.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly BlobContainerClient container;

    public BlobReviewSnapshotStore(BlobServiceClient serviceClient, IOptions<SnapshotOptions> options)
    {
        ArgumentNullException.ThrowIfNull(serviceClient);
        ArgumentNullException.ThrowIfNull(options);

        this.container = serviceClient.GetBlobContainerClient(options.Value.ContainerName);
    }

    public async Task WriteAsync<T>(
        string clientCode,
        Guid reviewId,
        int version,
        string name,
        T payload,
        CancellationToken cancellationToken = default
    )
    {
        await this.container.CreateIfNotExistsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var blob = this.container.GetBlobClient(BuildPath(clientCode, reviewId, version, name));
        var json = JsonSerializer.Serialize(payload, SerializerOptions);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await blob.UploadAsync(content, overwrite: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ReadAsync(
        string clientCode,
        Guid reviewId,
        int version,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var blob = this.container.GetBlobClient(BuildPath(clientCode, reviewId, version, name));

        if (!await blob.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var response = await blob.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
        return response.Value.Content.ToString();
    }

    /// <summary>
    /// Forward slashes only. Blob storage treats the name as one flat string, and a backslash
    /// produces a path that looks nested in code and is not nested in the portal.
    /// </summary>
    private static string BuildPath(string clientCode, Guid reviewId, int version, string name) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{clientCode.ToLowerInvariant()}/{reviewId:D}/v{version}/{name}.json"
        );
}
