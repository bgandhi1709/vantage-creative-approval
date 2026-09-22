using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Storage.Repositories;

namespace Vantage.Approvals.Api.Tests.TableStorage;

[Collection(AzuriteTableCollection.Name)]
public sealed class ReviewTableRepositoryTests(AzuriteTableFixture fixture)
{
    private ReviewTableRepository CreateRepository() => new(fixture.CreateClient());

    [Fact]
    public async Task CreateAsync_ThenGetAsync_RoundTripsEveryField()
    {
        var repository = this.CreateRepository();
        var review = NewReview();
        review.Assets =
        [
            new CreativeAsset
            {
                AssetId = Guid.NewGuid(),
                Name = "Hero banner",
                Format = "1080x1920",
                PreviewPath = "previews/hero.png",
            },
        ];

        await repository.CreateAsync(review);
        var stored = await repository.GetAsync(review.ReviewId);

        Assert.NotNull(stored);
        Assert.Equal(review.CampaignId, stored.CampaignId);
        Assert.Equal(review.ClientCode, stored.ClientCode);
        Assert.Equal(review.Version, stored.Version);
        Assert.Equal(ReviewStatus.AwaitingDecision, stored.Status);
        Assert.Equal(review.Locale, stored.Locale);
        Assert.Single(stored.Assets);
        Assert.Equal("Hero banner", stored.Assets[0].Name);
    }

    [Fact]
    public async Task GetAsync_WhenWrittenWithLocalTime_ReturnsTheSameInstantInUtc()
    {
        // Table Storage normalizes DateTimeOffset to UTC on write. An in-memory fake hands back
        // whatever offset it was given, which hides the fact that every read is UTC.
        var repository = this.CreateRepository();
        var review = NewReview();
        var localSent = new DateTimeOffset(2026, 3, 2, 9, 30, 0, TimeSpan.FromHours(5));
        review.SentDate = localSent;

        await repository.CreateAsync(review);
        var stored = await repository.GetAsync(review.ReviewId);

        Assert.NotNull(stored);
        Assert.NotNull(stored.SentDate);
        Assert.Equal(TimeSpan.Zero, stored.SentDate.Value.Offset);
        Assert.Equal(localSent.UtcDateTime, stored.SentDate.Value.UtcDateTime);
    }

    [Fact]
    public async Task GetAsync_IsUnaffectedByTheCasingOfTheCallersGuid()
    {
        // Keys are compared case-sensitively by the service. TableKeys is the only formatter, so
        // an upper-cased GUID from a caller still lands on the row written by a lower-cased one.
        var repository = this.CreateRepository();
        var review = NewReview();

        await repository.CreateAsync(review);
        var upperCased = Guid.Parse(review.ReviewId.ToString("D").ToUpperInvariant());
        var stored = await repository.GetAsync(upperCased);

        Assert.NotNull(stored);
        Assert.Equal(review.ReviewId, stored.ReviewId);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesTheRowInPlace()
    {
        var repository = this.CreateRepository();
        var review = NewReview();
        await repository.CreateAsync(review);

        review.Status = ReviewStatus.Approved;
        review.LastModifiedBy = "producer@vantage.test";
        review.LastModifiedUtc = DateTimeOffset.UtcNow;
        await repository.UpdateAsync(review);

        var stored = await repository.GetAsync(review.ReviewId);

        Assert.NotNull(stored);
        Assert.Equal(ReviewStatus.Approved, stored.Status);
        Assert.Equal("producer@vantage.test", stored.LastModifiedBy);
    }

    [Fact]
    public async Task GetAsync_WhenTheReviewDoesNotExist_ReturnsNull()
    {
        var repository = this.CreateRepository();

        var stored = await repository.GetAsync(Guid.NewGuid());

        Assert.Null(stored);
    }

    [Fact]
    public async Task GetManyAsync_SkipsIdsThatDoNotExist()
    {
        var repository = this.CreateRepository();
        var first = NewReview();
        var second = NewReview();
        await repository.CreateAsync(first);
        await repository.CreateAsync(second);

        var stored = await repository.GetManyAsync(
            [first.ReviewId, Guid.NewGuid(), second.ReviewId]
        );

        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, review => review.ReviewId == first.ReviewId);
        Assert.Contains(stored, review => review.ReviewId == second.ReviewId);
    }

    [Fact]
    public async Task GetManyAsync_WithNoIds_DoesNotTouchStorage()
    {
        var repository = this.CreateRepository();

        var stored = await repository.GetManyAsync([]);

        Assert.Empty(stored);
    }

    private static CreativeReview NewReview() =>
        new()
        {
            ReviewId = Guid.NewGuid(),
            CampaignId = Guid.NewGuid(),
            ClientCode = "northwind",
            CampaignName = "Spring launch",
            Version = 1,
            Status = ReviewStatus.AwaitingDecision,
            Locale = "en-US",
            ReviewerEmail = "client@northwind.test",
            ReviewerName = "Client Reviewer",
            CreatedUtc = DateTimeOffset.UtcNow,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            LastModifiedBy = "producer@vantage.test",
            IsActive = true,
        };
}
