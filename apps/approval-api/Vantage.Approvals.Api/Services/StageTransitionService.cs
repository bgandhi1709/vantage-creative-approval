using Vantage.Approvals.Api.Clients;
using Vantage.Approvals.Api.Logging;
using Vantage.Approvals.Api.Services.Interfaces;

namespace Vantage.Approvals.Api.Services;

/// <summary>
/// Advances campaign stages upstream, idempotently.
/// </summary>
/// <remarks>
/// The submit and decision flows write to three places in sequence — blob, table, upstream — with
/// no shared transaction, because no transaction spans them. What makes that safe is this class:
/// every transition re-reads the upstream state and does nothing when the target stage is already
/// open, so a retried request, a duplicated call, or a resumed run converges instead of double-
/// advancing. The compensation lives here rather than in a distributed transaction on purpose.
/// </remarks>
internal sealed class StageTransitionService(
    ICampaignSystemClient campaignSystem,
    ILogger<StageTransitionService> logger
) : IStageTransitionService
{
    /// <summary>Stage that must already be open before the target stage may be entered.</summary>
    private static readonly Dictionary<CampaignStage, CampaignStage?> RequiredPredecessor = new()
    {
        [CampaignStage.InProduction] = null,
        [CampaignStage.AwaitingClientDecision] = CampaignStage.InProduction,
        [CampaignStage.DecisionReceived] = CampaignStage.AwaitingClientDecision,
        [CampaignStage.Approved] = CampaignStage.DecisionReceived,
    };

    public async Task AdvanceAsync(
        IReadOnlyCollection<Guid> campaignIds,
        CampaignStage target,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(campaignIds);

        foreach (var campaignId in campaignIds.Distinct())
        {
            await this.AdvanceOneAsync(campaignId, target, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AdvanceOneAsync(
        Guid campaignId,
        CampaignStage target,
        CancellationToken cancellationToken
    )
    {
        var state = await campaignSystem
            .GetStageStateAsync(campaignId, cancellationToken)
            .ConfigureAwait(false);

        if (state.OpenStages.Contains(target))
        {
            Log.StageAlreadyOpen(logger, campaignId, target);
            return;
        }

        var predecessor = RequiredPredecessor[target];

        if (predecessor is not null && !state.OpenStages.Contains(predecessor.Value))
        {
            // Skip rather than throw: the upstream system is the authority on its own lifecycle,
            // and a campaign that has been moved by hand should not fail this service's request.
            Log.PredecessorStageMissing(logger, campaignId, predecessor.Value, target);
            return;
        }

        await campaignSystem
            .AdvanceStageAsync(campaignId, target, cancellationToken)
            .ConfigureAwait(false);

        Log.StageAdvanced(logger, campaignId, target);
    }
}
