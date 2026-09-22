using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Vantage.Approvals.Api.Clients;

/// <summary>
/// Serialized by name everywhere — over HTTP here, in table rows, and in blob snapshots.
/// One representation for one value: a store that writes the ordinal and a store that writes the
/// name disagree the moment somebody inserts an enum member, and the disagreement is invisible
/// until an old payload is read back.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CampaignStage
{
    InProduction = 0,
    AwaitingClientDecision = 1,
    DecisionReceived = 2,
    Approved = 3,
}

public sealed class CampaignStageState
{
    [JsonPropertyName("campaignId")]
    public Guid CampaignId { get; set; }

    /// <summary>Stages currently open upstream. A stage may legitimately be open more than once.</summary>
    [JsonPropertyName("openStages")]
    public List<CampaignStage> OpenStages { get; set; } = [];
}

public sealed class CampaignSystemException(string message, int statusCode, Guid campaignId)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public Guid CampaignId { get; } = campaignId;
}

public interface ICampaignSystemClient
{
    Task<CampaignStageState> GetStageStateAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Closes every open stage and opens <paramref name="stage"/> in one upstream call.
    /// Doing both halves in a single request is what makes the local sequence recoverable: this
    /// service never leaves the campaign with two stages half-open because it crashed mid-way.
    /// </summary>
    Task AdvanceStageAsync(
        Guid campaignId,
        CampaignStage stage,
        CancellationToken cancellationToken = default
    );
}

internal sealed class CampaignSystemClient(HttpClient httpClient) : ICampaignSystemClient
{
    public async Task<CampaignStageState> GetStageStateAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default
    )
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"campaigns/{campaignId:D}/stages");
        using var response = await httpClient
            .GetAsync(path, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new CampaignSystemException(
                "The campaign system rejected a stage read.",
                (int)response.StatusCode,
                campaignId
            );
        }

        return await response
                .Content.ReadFromJsonAsync<CampaignStageState>(cancellationToken)
                .ConfigureAwait(false)
            ?? new CampaignStageState { CampaignId = campaignId };
    }

    public async Task AdvanceStageAsync(
        Guid campaignId,
        CampaignStage stage,
        CancellationToken cancellationToken = default
    )
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"campaigns/{campaignId:D}/stages");
        using var response = await httpClient
            .PostAsJsonAsync(path, new { stage = stage.ToString() }, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new CampaignSystemException(
                $"The campaign system rejected the advance to {stage}.",
                (int)response.StatusCode,
                campaignId
            );
        }
    }
}
