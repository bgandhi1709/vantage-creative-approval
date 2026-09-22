using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Vantage.Approvals.Api.Auth;
using Vantage.Approvals.Api.Configuration;
using Vantage.Approvals.Api.Contracts;
using Vantage.Approvals.Api.Controllers.V1;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Repositories;

namespace Vantage.Approvals.Api.Tests.Controllers;

public sealed class ReviewNotesControllerTests
{
    private readonly Mock<IReviewNoteRepository> notes = new();

    [Fact]
    public async Task Get_ForAProducer_IncludesInternalNotes()
    {
        var reviewId = Guid.NewGuid();
        var controller = this.CreateController(PrincipalFromRealProducerToken());

        await controller.Get(reviewId, CancellationToken.None);

        this.notes.Verify(
            repository =>
                repository.GetForReviewAsync(reviewId, true, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Get_ForAClientReviewer_ExcludesInternalNotes()
    {
        var reviewId = Guid.NewGuid();
        var controller = this.CreateController(ReviewerPrincipal());

        await controller.Get(reviewId, CancellationToken.None);

        this.notes.Verify(
            repository =>
                repository.GetForReviewAsync(reviewId, false, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Create_InternalNoteFromAProducer_IsAccepted()
    {
        var controller = this.CreateController(PrincipalFromRealProducerToken());

        var result = await controller.Create(
            Guid.NewGuid(),
            new NoteBody { Body = "Client is over budget.", IsInternal = true },
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result.Result);
        this.notes.Verify(
            repository =>
                repository.CreateAsync(
                    It.Is<ReviewNote>(note => note.IsInternal),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Create_InternalNoteFromAClientReviewer_IsForbidden()
    {
        var controller = this.CreateController(ReviewerPrincipal());

        var result = await controller.Create(
            Guid.NewGuid(),
            new NoteBody { Body = "Trying to write an internal note.", IsInternal = true },
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result.Result);
        this.notes.Verify(
            repository =>
                repository.CreateAsync(It.IsAny<ReviewNote>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private ReviewNotesController CreateController(ClaimsPrincipal principal) =>
        new(this.notes.Object, TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    /// <summary>
    /// Builds the principal from a token this service actually mints, rather than hand-rolling the
    /// claims a test wishes were there. A hand-built identity hid a real defect once: the code
    /// asked which scheme had authenticated the caller, the test answered "Bearer", and the JWT
    /// handler in production answered something else entirely.
    /// </summary>
    private static ClaimsPrincipal PrincipalFromRealProducerToken()
    {
        var factory = new ProducerTokenFactory(
            Options.Create(
                new ProducerTokenOptions
                {
                    Issuer = "vantage-approvals-api",
                    Audience = "vantage-producer-console",
                    SigningKey = "a-local-test-producer-key-long-enough",
                    AllowedEmailDomain = "vantage.test",
                    LifetimeMinutes = 60,
                }
            )
        );

        var token = factory.TryIssue("producer@vantage.test");
        Assert.NotNull(token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // The claim types are the ones the handler maps to; the identity's own authentication type
        // is deliberately left as something else, which is the situation that caused the defect.
        return new ClaimsPrincipal(
            new ClaimsIdentity(jwt.Claims, "AuthenticationTypes.Federation", ClaimTypes.Name, ClaimTypes.Role)
        );
    }

    private static ClaimsPrincipal ReviewerPrincipal() =>
        new(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "client@northwind.test")],
                ReviewerSessionAuthenticationHandler.SchemeName,
                ClaimTypes.Name,
                ClaimTypes.Role
            )
        );
}
