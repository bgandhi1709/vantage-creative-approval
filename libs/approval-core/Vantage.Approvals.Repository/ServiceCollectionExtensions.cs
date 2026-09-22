using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vantage.Approvals.Core.Repositories;
using Vantage.Approvals.Core.Storage.Repositories;

namespace Vantage.Approvals.Core.Storage;

public static class ServiceCollectionExtensions
{
    /// <summary>Configuration key holding the Table Storage connection string.</summary>
    public const string ConnectionStringKey = "ConnectionStrings:ApprovalTableStorage";

    /// <summary>Environment variable checked when the configuration key is absent.</summary>
    public const string ConnectionStringEnvironmentVariable = "APPROVAL_TABLE_STORAGE";

    /// <summary>
    /// Registers the storage layer. Fails at startup rather than on first request when the
    /// connection string is missing: a revision that cannot reach its storage should never pass
    /// its own readiness check.
    /// </summary>
    public static IServiceCollection AddApprovalCore(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString =
            configuration[ConnectionStringKey]
            ?? configuration[ConnectionStringEnvironmentVariable];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Approval storage is not configured. Set '{ConnectionStringKey}' or the "
                    + $"'{ConnectionStringEnvironmentVariable}' environment variable."
            );
        }

        services.AddSingleton(new TableServiceClient(connectionString));
        services.AddSingleton<AssignmentIndexSync>();

        services.AddScoped<IReviewRepository, ReviewTableRepository>();
        services.AddScoped<IReviewDecisionRepository, ReviewDecisionTableRepository>();
        services.AddScoped<IReviewNoteRepository, ReviewNoteTableRepository>();
        services.AddScoped<IReviewAssignmentRepository, ReviewAssignmentTableRepository>();

        return services;
    }
}
