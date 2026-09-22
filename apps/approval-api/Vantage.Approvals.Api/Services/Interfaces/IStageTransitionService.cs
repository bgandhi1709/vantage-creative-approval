using Vantage.Approvals.Api.Clients;

namespace Vantage.Approvals.Api.Services.Interfaces;

public interface IStageTransitionService
{
    Task AdvanceAsync(
        IReadOnlyCollection<Guid> campaignIds,
        CampaignStage target,
        CancellationToken cancellationToken = default
    );
}
