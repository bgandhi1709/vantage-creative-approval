using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Vantage.Approvals.Api.Auth;
using Vantage.Approvals.Api.Contracts;
using Vantage.Approvals.Api.Controllers.V1;
using Vantage.Approvals.Api.Services.Interfaces;
using Vantage.Approvals.Core.Models;

namespace Vantage.Approvals.Api.Tests.Controllers;

public sealed class ReviewsControllerTests
{
    private readonly Mock<ICreativeReviewService> service = new();

    [Fact]
    public async Task Get_WhenTheRoundBelongsToAnotherClient_Returns404()
    {
        // The client code is part of every route. A reviewer holding a valid cookie for one client
        // must not be able to read another client's round by editing the URL.
        var review = NewReview("northwind");
        this.service
            .Setup(reviews => reviews.GetAsync(review.ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var controller = this.CreateController(NewReviewerPrincipal(review.ReviewId));

        var result = await controller.Get("contoso", review.ReviewId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Get_ForTheOwningClient_ReturnsTheRound()
    {
        var review = NewReview("northwind");
        this.service
            .Setup(reviews => reviews.GetAsync(review.ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var controller = this.CreateController(NewReviewerPrincipal(review.ReviewId));

        var result = await controller.Get("NORTHWIND", review.ReviewId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ReviewResponse>(ok.Value);
        Assert.Equal(review.ReviewId, body.ReviewId);
    }

    [Fact]
    public async Task RecordDecision_WhenTheCookieIsForADifferentRound_IsForbidden()
    {
        // One emailed link must answer for one round only.
        var review = NewReview("northwind");
        var controller = this.CreateController(NewReviewerPrincipal(Guid.NewGuid()));

        var result = await controller.RecordDecision(
            "northwind",
            review.ReviewId,
            new DecisionBody { Outcome = DecisionOutcome.Approved },
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result.Result);
        this.service.Verify(
            reviews =>
                reviews.RecordDecisionAsync(
                    It.IsAny<RecordDecisionRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RecordDecision_WithAMatchingCookie_DelegatesToTheService()
    {
        var review = NewReview("northwind");
        this.service
            .Setup(reviews =>
                reviews.RecordDecisionAsync(
                    It.IsAny<RecordDecisionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(review);

        var controller = this.CreateController(NewReviewerPrincipal(review.ReviewId));

        var result = await controller.RecordDecision(
            "northwind",
            review.ReviewId,
            new DecisionBody { Outcome = DecisionOutcome.ChangesRequested, Comment = "Tweak it." },
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result.Result);
        this.service.Verify(
            reviews =>
                reviews.RecordDecisionAsync(
                    It.Is<RecordDecisionRequest>(request =>
                        request.ReviewId == review.ReviewId
                        && request.Outcome == DecisionOutcome.ChangesRequested
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Submit_PassesThePortalUrlDerivedFromTheRequest()
    {
        var review = NewReview("northwind");
        this.service
            .Setup(reviews =>
                reviews.SubmitAsync(It.IsAny<SubmitReviewRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(review);

        var controller = this.CreateController(NewProducerPrincipal());
        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("reviews.vantage.test");

        await controller.Submit("northwind", review.ReviewId, CancellationToken.None);

        this.service.Verify(
            reviews =>
                reviews.SubmitAsync(
                    It.Is<SubmitReviewRequest>(request =>
                        request.PortalBaseUrl == "https://reviews.vantage.test"
                        && request.SubmittedBy == "producer@vantage.test"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetOutstanding_ReturnsOneEntryPerOpenRound()
    {
        this.service
            .Setup(reviews =>
                reviews.GetOutstandingForClientAsync("northwind", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([NewReview("northwind"), NewReview("northwind")]);

        var controller = this.CreateController(NewProducerPrincipal());

        var result = await controller.GetOutstanding("northwind", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<ReviewResponse>>(ok.Value);
        Assert.Equal(2, body.Count);
    }

    private ReviewsController CreateController(ClaimsPrincipal principal) =>
        new(this.service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    private static ClaimsPrincipal NewReviewerPrincipal(Guid reviewId) =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, "client@northwind.test"),
                    new Claim(
                        ReviewerSessionAuthenticationHandler.ReviewIdClaimType,
                        reviewId.ToString("D")
                    ),
                ],
                ReviewerSessionAuthenticationHandler.SchemeName
            )
        );

    private static ClaimsPrincipal NewProducerPrincipal() =>
        new(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "producer@vantage.test")],
                "Bearer"
            )
        );

    private static CreativeReview NewReview(string clientCode) =>
        new()
        {
            ReviewId = Guid.NewGuid(),
            CampaignId = Guid.NewGuid(),
            ClientCode = clientCode,
            CampaignName = "Spring launch",
            Version = 1,
            Status = ReviewStatus.AwaitingDecision,
            Locale = "en-US",
            ReviewerEmail = "client@northwind.test",
            ReviewerName = "Client Reviewer",
            CreatedUtc = DateTimeOffset.UtcNow,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            IsActive = true,
        };
}
