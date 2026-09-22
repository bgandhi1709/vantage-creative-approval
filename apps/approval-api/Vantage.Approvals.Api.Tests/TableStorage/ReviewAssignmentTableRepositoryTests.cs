using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Storage.Repositories;

namespace Vantage.Approvals.Api.Tests.TableStorage;

[Collection(AzuriteTableCollection.Name)]
public sealed class ReviewAssignmentTableRepositoryTests(AzuriteTableFixture fixture)
{
    private ReviewAssignmentTableRepository CreateRepository()
    {
        var client = fixture.CreateClient();
        return new ReviewAssignmentTableRepository(client, new AssignmentIndexSync(client));
    }

    [Fact]
    public async Task UpsertAsync_WritesTheRowAndBothIndexes()
    {
        var repository = this.CreateRepository();
        var assignment = NewAssignment(clientCode: "northwind", version: 1);

        await repository.UpsertAsync(assignment);

        var row = await repository.GetAsync(assignment.AssignmentId);
        var latest = await repository.GetLatestByCampaignAsync([assignment.CampaignId]);
        var forClient = await repository.GetActiveForClientAsync("northwind");

        Assert.NotNull(row);
        Assert.Equal(assignment.ReviewId, row.ReviewId);
        Assert.Equal(assignment.ReviewId, latest[assignment.CampaignId].ReviewId);
        Assert.Contains(forClient, item => item.AssignmentId == assignment.AssignmentId);
    }

    [Fact]
    public async Task GetLatestByCampaignAsync_ReturnsTheHighestVersion()
    {
        // The by-campaign index is keyed on an inverted version, so the newest round is the first
        // row of the partition and the read costs one row no matter how many rounds exist.
        var repository = this.CreateRepository();
        var campaignId = Guid.NewGuid();

        var first = NewAssignment("northwind", version: 1, campaignId);
        var second = NewAssignment("northwind", version: 2, campaignId);
        var third = NewAssignment("northwind", version: 3, campaignId);

        await repository.UpsertAsync(first);
        await repository.UpsertAsync(third);
        await repository.UpsertAsync(second);

        var latest = await repository.GetLatestByCampaignAsync([campaignId]);

        Assert.Equal(3, latest[campaignId].Version);
        Assert.Equal(third.ReviewId, latest[campaignId].ReviewId);
    }

    [Fact]
    public async Task GetLatestByCampaignAsync_SkipsCampaignsWithNoRounds()
    {
        var repository = this.CreateRepository();
        var known = NewAssignment("northwind", version: 1);
        await repository.UpsertAsync(known);

        var latest = await repository.GetLatestByCampaignAsync([known.CampaignId, Guid.NewGuid()]);

        Assert.Single(latest);
        Assert.True(latest.ContainsKey(known.CampaignId));
    }

    [Fact]
    public async Task GetActiveForClientAsync_ExcludesSupersededRounds()
    {
        var repository = this.CreateRepository();
        var clientCode = $"client-{Guid.NewGuid():N}";

        var active = NewAssignment(clientCode, version: 2);
        var superseded = NewAssignment(clientCode, version: 1);
        superseded.IsActive = false;

        await repository.UpsertAsync(active);
        await repository.UpsertAsync(superseded);

        var outstanding = await repository.GetActiveForClientAsync(clientCode);

        Assert.Single(outstanding);
        Assert.Equal(active.AssignmentId, outstanding[0].AssignmentId);
    }

    [Fact]
    public async Task GetActiveForClientAsync_IgnoresTheCasingOfTheClientCode()
    {
        // Partition keys are compared case-sensitively; TableKeys.FromCode is what makes a
        // client code typed in a URL match the row written from a form.
        var repository = this.CreateRepository();
        var clientCode = $"Client-{Guid.NewGuid():N}";
        var assignment = NewAssignment(clientCode, version: 1);

        await repository.UpsertAsync(assignment);

        var outstanding = await repository.GetActiveForClientAsync(clientCode.ToUpperInvariant());

        Assert.Single(outstanding);
    }

    [Fact]
    public async Task UpsertAsync_WhenTheRoundIsSuperseded_UpdatesTheIndexesToo()
    {
        var repository = this.CreateRepository();
        var clientCode = $"client-{Guid.NewGuid():N}";
        var assignment = NewAssignment(clientCode, version: 1);
        await repository.UpsertAsync(assignment);

        assignment.IsActive = false;
        await repository.UpsertAsync(assignment);

        var outstanding = await repository.GetActiveForClientAsync(clientCode);

        Assert.Empty(outstanding);
    }

    private static ReviewAssignment NewAssignment(
        string clientCode,
        int version,
        Guid? campaignId = null
    ) =>
        new()
        {
            AssignmentId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            CampaignId = campaignId ?? Guid.NewGuid(),
            ClientCode = clientCode,
            Version = version,
            SentDate = DateTimeOffset.UtcNow,
            IsActive = true,
        };
}
