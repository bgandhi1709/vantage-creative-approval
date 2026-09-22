namespace Vantage.Approvals.Api.Exceptions;

public sealed class ReviewNotFoundException(Guid reviewId)
    : Exception($"Review {reviewId:D} does not exist.")
{
    public Guid ReviewId { get; } = reviewId;
}

public sealed class ReviewStateException(Guid reviewId, string message) : Exception(message)
{
    public Guid ReviewId { get; } = reviewId;
}
