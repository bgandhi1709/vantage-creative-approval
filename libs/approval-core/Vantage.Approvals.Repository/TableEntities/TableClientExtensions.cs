using Azure.Data.Tables;

namespace Vantage.Approvals.Core.Storage;

internal static class TableClientExtensions
{
    /// <summary>
    /// Creates the table on first use. Calling this at the head of each repository method keeps
    /// first-run setup out of application startup: a cold environment (a fresh Azurite container,
    /// a new deployment slot) works without a provisioning step, and the call is a no-op after that.
    /// </summary>
    internal static async Task<TableClient> EnsureExistsAsync(
        this TableClient client,
        CancellationToken cancellationToken
    )
    {
        await client.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        return client;
    }
}
