using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Storage.Repositories;

namespace Vantage.Approvals.Api.Tests.TableStorage;

[Collection(AzuriteTableCollection.Name)]
public sealed class ReviewNoteTableRepositoryTests(AzuriteTableFixture fixture)
{
    private ReviewNoteTableRepository CreateRepository() => new(fixture.CreateClient());

    [Fact]
    public async Task GetForReviewAsync_WithoutInternal_LeavesInternalNotesInStorage()
    {
        // The filter is server-side: an internal note must not reach a client-facing caller even
        // if that caller forgets to filter after the read.
        var repository = this.CreateRepository();
        var reviewId = Guid.NewGuid();

        await repository.CreateAsync(NewNote(reviewId, isInternal: false, "Looks good to us."));
        await repository.CreateAsync(NewNote(reviewId, isInternal: true, "Client is over budget."));

        var visible = await repository.GetForReviewAsync(reviewId, includeInternal: false);

        Assert.Single(visible);
        Assert.False(visible[0].IsInternal);
        Assert.Equal("Looks good to us.", visible[0].Body);
    }

    [Fact]
    public async Task GetForReviewAsync_WithInternal_ReturnsBothKinds()
    {
        var repository = this.CreateRepository();
        var reviewId = Guid.NewGuid();

        await repository.CreateAsync(NewNote(reviewId, isInternal: false, "Client comment"));
        await repository.CreateAsync(NewNote(reviewId, isInternal: true, "Producer comment"));

        var all = await repository.GetForReviewAsync(reviewId, includeInternal: true);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetForReviewAsync_IsScopedToOneReview()
    {
        var repository = this.CreateRepository();
        var mine = Guid.NewGuid();

        await repository.CreateAsync(NewNote(mine, isInternal: false, "Mine"));
        await repository.CreateAsync(NewNote(Guid.NewGuid(), isInternal: false, "Someone else's"));

        var notes = await repository.GetForReviewAsync(mine, includeInternal: true);

        Assert.Single(notes);
        Assert.Equal("Mine", notes[0].Body);
    }

    [Fact]
    public async Task CreateAsync_NormalizesTheCreationTimeToUtc()
    {
        var repository = this.CreateRepository();
        var reviewId = Guid.NewGuid();
        var note = NewNote(reviewId, isInternal: false, "Timezone check");
        note.CreatedUtc = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.FromHours(-4));

        await repository.CreateAsync(note);
        var stored = await repository.GetForReviewAsync(reviewId, includeInternal: false);

        Assert.Single(stored);
        Assert.Equal(TimeSpan.Zero, stored[0].CreatedUtc.Offset);
        Assert.Equal(note.CreatedUtc.UtcDateTime, stored[0].CreatedUtc.UtcDateTime);
    }

    private static ReviewNote NewNote(Guid reviewId, bool isInternal, string body) =>
        new()
        {
            NoteId = Guid.NewGuid(),
            ReviewId = reviewId,
            AuthorEmail = isInternal ? "producer@vantage.test" : "client@northwind.test",
            Body = body,
            CreatedUtc = DateTimeOffset.UtcNow,
            IsInternal = isInternal,
        };
}
