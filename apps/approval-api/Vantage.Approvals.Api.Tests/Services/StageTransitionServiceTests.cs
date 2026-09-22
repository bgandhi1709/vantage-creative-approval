using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vantage.Approvals.Api.Clients;
using Vantage.Approvals.Api.Services;

namespace Vantage.Approvals.Api.Tests.Services;

public sealed class StageTransitionServiceTests
{
    private readonly Mock<ICampaignSystemClient> campaignSystem = new();
    private readonly StageTransitionService service;

    public StageTransitionServiceTests() =>
        this.service = new StageTransitionService(
            this.campaignSystem.Object,
            NullLogger<StageTransitionService>.Instance
        );

    [Fact]
    public async Task AdvanceAsync_WhenTheTargetStageIsAlreadyOpen_DoesNothing()
    {
        // This is what makes the un-transactional submit path safe to retry.
        var campaignId = Guid.NewGuid();
        this.GivenOpenStages(campaignId, CampaignStage.AwaitingClientDecision);

        await this.service.AdvanceAsync([campaignId], CampaignStage.AwaitingClientDecision);

        this.campaignSystem.Verify(
            client =>
                client.AdvanceStageAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CampaignStage>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task AdvanceAsync_WhenThePredecessorStageIsMissing_SkipsInsteadOfThrowing()
    {
        // The upstream system owns its own lifecycle. A campaign somebody moved by hand should not
        // fail this service's request.
        var campaignId = Guid.NewGuid();
        this.GivenOpenStages(campaignId, CampaignStage.InProduction);

        await this.service.AdvanceAsync([campaignId], CampaignStage.Approved);

        this.campaignSystem.Verify(
            client =>
                client.AdvanceStageAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CampaignStage>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task AdvanceAsync_WhenThePredecessorIsOpen_AdvancesOnce()
    {
        var campaignId = Guid.NewGuid();
        this.GivenOpenStages(campaignId, CampaignStage.InProduction);

        await this.service.AdvanceAsync([campaignId], CampaignStage.AwaitingClientDecision);

        this.campaignSystem.Verify(
            client =>
                client.AdvanceStageAsync(
                    campaignId,
                    CampaignStage.AwaitingClientDecision,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AdvanceAsync_DeduplicatesRepeatedCampaignIds()
    {
        var campaignId = Guid.NewGuid();
        this.GivenOpenStages(campaignId, CampaignStage.InProduction);

        await this.service.AdvanceAsync(
            [campaignId, campaignId, campaignId],
            CampaignStage.AwaitingClientDecision
        );

        this.campaignSystem.Verify(
            client =>
                client.AdvanceStageAsync(
                    campaignId,
                    CampaignStage.AwaitingClientDecision,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AdvanceAsync_WithSeveralCampaigns_AdvancesEachOne()
    {
        // Never "the first campaign": the loop is the point.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        this.GivenOpenStages(first, CampaignStage.InProduction);
        this.GivenOpenStages(second, CampaignStage.InProduction);

        await this.service.AdvanceAsync([first, second], CampaignStage.AwaitingClientDecision);

        this.campaignSystem.Verify(
            client =>
                client.AdvanceStageAsync(
                    It.IsAny<Guid>(),
                    CampaignStage.AwaitingClientDecision,
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task AdvanceAsync_WhenTheUpstreamCallFails_PropagatesTheException()
    {
        var campaignId = Guid.NewGuid();
        this.GivenOpenStages(campaignId, CampaignStage.InProduction);
        this.campaignSystem
            .Setup(client =>
                client.AdvanceStageAsync(
                    campaignId,
                    It.IsAny<CampaignStage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new CampaignSystemException("upstream is down", 503, campaignId));

        await Assert.ThrowsAsync<CampaignSystemException>(
            () => this.service.AdvanceAsync([campaignId], CampaignStage.AwaitingClientDecision)
        );
    }

    private void GivenOpenStages(Guid campaignId, params CampaignStage[] openStages) =>
        this.campaignSystem
            .Setup(client => client.GetStageStateAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new CampaignStageState { CampaignId = campaignId, OpenStages = [.. openStages] }
            );
}
