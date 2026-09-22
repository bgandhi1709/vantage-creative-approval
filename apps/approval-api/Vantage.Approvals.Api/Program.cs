using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Vantage.Approvals.Api.Auth;
using Vantage.Approvals.Api.Clients;
using Vantage.Approvals.Api.Configuration;
using Vantage.Approvals.Api.Middleware;
using Vantage.Approvals.Api.Services;
using Vantage.Approvals.Api.Services.Interfaces;
using Vantage.Approvals.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Configuration. Every options class is validated on start: a revision missing a setting should
// fail to come up, not fail on the first request that happens to need it.
// ---------------------------------------------------------------------------------------------
builder
    .Services.AddOptions<SnapshotOptions>()
    .Bind(builder.Configuration.GetSection(SnapshotOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<NotificationOptions>()
    .Bind(builder.Configuration.GetSection(NotificationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<ProducerTokenOptions>()
    .Bind(builder.Configuration.GetSection(ProducerTokenOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<ReviewerSessionOptions>()
    .Bind(builder.Configuration.GetSection(ReviewerSessionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<CampaignSystemOptions>()
    .Bind(builder.Configuration.GetSection(CampaignSystemOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------------------------------------------------------------------------------------------
// Storage. AddApprovalCore owns the table repositories; blobs are the API's own concern, because
// snapshots are an orchestration artefact rather than part of the aggregate.
// ---------------------------------------------------------------------------------------------
builder.Services.AddApprovalCore(builder.Configuration);

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SnapshotOptions>>();
    return new BlobServiceClient(options.Value.ConnectionString);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IReviewSnapshotStore, BlobReviewSnapshotStore>();
builder.Services.AddScoped<INotificationSender, SmtpNotificationSender>();
builder.Services.AddScoped<IStageTransitionService, StageTransitionService>();
builder.Services.AddScoped<ICreativeReviewService, CreativeReviewService>();
builder.Services.AddSingleton<ISessionVerifier, SignedCookieSessionVerifier>();
builder.Services.AddSingleton<IProducerTokenFactory, ProducerTokenFactory>();

// ---------------------------------------------------------------------------------------------
// Outbound HTTP. Every typed client goes through the factory and carries the same retry policy.
// ---------------------------------------------------------------------------------------------
builder
    .Services.AddHttpClient<ICampaignSystemClient, CampaignSystemClient>(
        (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<CampaignSystemOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + '/');
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
        }
    )
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))
    );

// ---------------------------------------------------------------------------------------------
// Authentication. Two schemes: bearer tokens for the producer console, a signed cookie for client
// reviewers who arrive from an emailed link and have no account to sign in to.
// ---------------------------------------------------------------------------------------------
var producerToken =
    builder.Configuration.GetSection(ProducerTokenOptions.SectionName).Get<ProducerTokenOptions>()
    ?? new ProducerTokenOptions();

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = producerToken.Issuer,
            ValidAudience = producerToken.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    string.IsNullOrEmpty(producerToken.SigningKey)
                        ? new string('0', 32)
                        : producerToken.SigningKey
                )
            ),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ReviewerSessionAuthenticationHandler>(
        ReviewerSessionAuthenticationHandler.SchemeName,
        configureOptions: null
    );

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------------------------
// MVC and versioning. The version travels in a header so routes stay readable and a client can be
// pinned without rewriting every URL it holds.
// ---------------------------------------------------------------------------------------------
builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
        // Enums cross this boundary by name, the same way they are written to tables and blobs.
        // One representation end to end means a payload captured today still reads correctly after
        // somebody inserts an enum member.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
    );

builder
    .Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new HeaderApiVersionReader("x-api-version");
    })
    .AddMvc();

builder.Services.AddOpenApi();

// ---------------------------------------------------------------------------------------------
// Health. /health/live says the process is up; /health/ready asserts the configuration this
// service cannot run without, so a misconfigured revision fails readiness instead of traffic.
// ---------------------------------------------------------------------------------------------
string[] requiredConfiguration =
[
    ServiceCollectionExtensions.ConnectionStringKey,
    $"{SnapshotOptions.SectionName}:ConnectionString",
    $"{SnapshotOptions.SectionName}:ContainerName",
    $"{NotificationOptions.SectionName}:SmtpHost",
    $"{NotificationOptions.SectionName}:FromAddress",
    $"{ProducerTokenOptions.SectionName}:Issuer",
    $"{ProducerTokenOptions.SectionName}:Audience",
    $"{ProducerTokenOptions.SectionName}:SigningKey",
    $"{ProducerTokenOptions.SectionName}:AllowedEmailDomain",
    $"{ReviewerSessionOptions.SectionName}:SigningKey",
    $"{ReviewerSessionOptions.SectionName}:CookieName",
    $"{CampaignSystemOptions.SectionName}:BaseUrl",
    $"{CampaignSystemOptions.SectionName}:ApiKey",
];

builder
    .Services.AddHealthChecks()
    .AddCheck(
        "configuration",
        () =>
        {
            var missing = requiredConfiguration
                .Where(key => string.IsNullOrWhiteSpace(builder.Configuration[key]))
                .ToList();

            return missing.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    $"Missing configuration: {string.Join(", ", missing)}"
                );
        },
        tags: ["ready"]
    );

var app = builder.Build();

// TLS terminates at the reverse proxy and the review links in outgoing mail are built from the
// request. Without this the emailed URL would say http and name the container's own hostname.
app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    }
);

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync().ConfigureAwait(false);
