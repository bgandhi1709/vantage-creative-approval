using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vantage.Approvals.Api.Auth;
using Vantage.Approvals.Api.Contracts;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Repositories;

namespace Vantage.Approvals.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("v1/{clientCode}/reviews/{reviewId:guid}/notes")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme
        + ","
        + ReviewerSessionAuthenticationHandler.SchemeName
)]
public sealed class ReviewNotesController(IReviewNoteRepository notes, TimeProvider clock)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewNote>>> Get(
        Guid reviewId,
        CancellationToken cancellationToken
    )
    {
        // Producers see internal notes; a client reviewer never does. The caller's scheme decides,
        // and the filter is applied in storage rather than after the read.
        var includeInternal = this.IsProducer();

        var found = await notes
            .GetForReviewAsync(reviewId, includeInternal, cancellationToken)
            .ConfigureAwait(false);

        return this.Ok(found);
    }

    [HttpPost]
    public async Task<ActionResult<ReviewNote>> Create(
        Guid reviewId,
        [FromBody] NoteBody body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.IsInternal && !this.IsProducer())
        {
            return this.Forbid();
        }

        var note = new ReviewNote
        {
            NoteId = Guid.NewGuid(),
            ReviewId = reviewId,
            AuthorEmail = this.User.Identity?.Name ?? "unknown",
            Body = body.Body,
            CreatedUtc = clock.GetUtcNow(),
            IsInternal = body.IsInternal,
        };

        await notes.CreateAsync(note, cancellationToken).ConfigureAwait(false);

        return this.Ok(note);
    }

    private bool IsProducer() =>
        string.Equals(
            this.User.Identity?.AuthenticationType,
            JwtBearerDefaults.AuthenticationScheme,
            StringComparison.Ordinal
        );
}
