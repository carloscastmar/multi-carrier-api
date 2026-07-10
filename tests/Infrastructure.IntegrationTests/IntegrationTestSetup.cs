using Microsoft.Extensions.DependencyInjection;
using MultiCarrierApi.Application.FunctionalTests.Infrastructure;
using MultiCarrierApi.Shared;

namespace MultiCarrierApi.Infrastructure.IntegrationTests;

[SetUpFixture]
public class IntegrationTestSetup
{
    internal static IServiceScopeFactory ScopeFactory { get; private set; } = null!;

    private static WebApiFactory? _factory;
    private static DistributedApplication? _app;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var cancellationToken = cts.Token;

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TestAppHost>(
                args: [],
                configureBuilder: (options, _) =>
                {
                    options.DisableDashboard = true;
                });

        builder.Configuration["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true";

        _app = await builder
            .BuildAsync(cancellationToken)
            .WaitAsync(cancellationToken);

        await _app
            .StartAsync(cancellationToken)
            .WaitAsync(cancellationToken);

        await _app.ResourceNotifications.WaitForResourceHealthyAsync(
            Services.Database, cancellationToken);

        var connectionString = (await _app.GetConnectionStringAsync(Services.Database))!;

        _factory = new WebApiFactory(connectionString);
        ScopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_app is not null) await _app.DisposeAsync();
        if (_factory is not null) await _factory.DisposeAsync();
    }
}
