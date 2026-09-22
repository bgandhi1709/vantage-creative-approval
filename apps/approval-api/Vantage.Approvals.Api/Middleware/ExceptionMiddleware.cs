using System.Net;
using Microsoft.AspNetCore.Mvc;
using Vantage.Approvals.Api.Clients;
using Vantage.Approvals.Api.Exceptions;
using Vantage.Approvals.Api.Logging;

namespace Vantage.Approvals.Api.Middleware;

/// <summary>
/// Maps domain exceptions onto ProblemDetails so the SPA gets one error shape from every endpoint.
/// </summary>
internal sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (ReviewNotFoundException ex)
        {
            Log.ReviewNotFound(logger, ex.ReviewId);
            await WriteProblemAsync(context, HttpStatusCode.NotFound, "Review not found", ex.Message)
                .ConfigureAwait(false);
        }
        catch (ReviewStateException ex)
        {
            Log.ReviewStateRejected(logger, ex.ReviewId, ex.Message);
            await WriteProblemAsync(
                    context,
                    HttpStatusCode.Conflict,
                    "Review state conflict",
                    ex.Message
                )
                .ConfigureAwait(false);
        }
        catch (CampaignSystemException ex)
        {
            Log.CampaignSystemFailed(logger, ex, ex.CampaignId, ex.StatusCode);
            await WriteProblemAsync(
                    context,
                    HttpStatusCode.BadGateway,
                    "Upstream campaign system error",
                    ex.Message
                )
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode status,
        string title,
        string detail
    )
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        await context
            .Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = (int)status,
                    Title = title,
                    Detail = detail,
                    Instance = context.Request.Path,
                }
            )
            .ConfigureAwait(false);
    }
}
