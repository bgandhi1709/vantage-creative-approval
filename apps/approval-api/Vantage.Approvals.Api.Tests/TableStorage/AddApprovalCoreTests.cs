using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vantage.Approvals.Core.Repositories;
using Vantage.Approvals.Core.Storage;

namespace Vantage.Approvals.Api.Tests.TableStorage;

[Collection(AzuriteTableCollection.Name)]
public sealed class AddApprovalCoreTests(AzuriteTableFixture fixture)
{
    [Fact]
    public void AddApprovalCore_ResolvesEveryRepository()
    {
        using var provider = BuildProvider(fixture.ConnectionString);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReviewRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReviewDecisionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReviewNoteRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReviewAssignmentRepository>());
    }

    [Fact]
    public void AddApprovalCore_WithNoConnectionString_FailsAtStartupWithANamedError()
    {
        var configuration = new ConfigurationBuilder().Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddApprovalCore(configuration)
        );

        Assert.Contains(ServiceCollectionExtensions.ConnectionStringKey, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddApprovalCore_AcceptsTheEnvironmentVariableFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [ServiceCollectionExtensions.ConnectionStringEnvironmentVariable] =
                        fixture.ConnectionString,
                }
            )
            .Build();

        using var provider = new ServiceCollection()
            .AddApprovalCore(configuration)
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReviewRepository>());
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [ServiceCollectionExtensions.ConnectionStringKey] = connectionString,
                }
            )
            .Build();

        return new ServiceCollection()
            .AddApprovalCore(configuration)
            .BuildServiceProvider(validateScopes: true);
    }
}
