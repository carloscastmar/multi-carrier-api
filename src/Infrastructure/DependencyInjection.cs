using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Infrastructure.Carriers;
using MultiCarrierApi.Infrastructure.Carriers.FedEx;
using MultiCarrierApi.Infrastructure.Carriers.Ups;
using MultiCarrierApi.Infrastructure.Data;
using MultiCarrierApi.Infrastructure.Data.Interceptors;
using MultiCarrierApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
        });

        builder.EnrichNpgsqlDbContext<ApplicationDbContext>();

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.Configure<CarriersOptions>(builder.Configuration.GetSection(CarriersOptions.SectionName));
        AddCarrierAdapters(builder);

        builder.Services.AddAuthentication()
            .AddBearerToken(IdentityConstants.BearerScheme);

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();
    }

    private static void AddCarrierAdapters(IHostApplicationBuilder builder)
    {
        var carriersOptions = builder.Configuration
            .GetSection(CarriersOptions.SectionName)
            .Get<CarriersOptions>() ?? new CarriersOptions();

        if (carriersOptions.Ups.UseMock)
        {
            AddInstrumentedAdapter<MockUpsAdapter>(builder.Services);
        }
        else
        {
            builder.Services.AddHttpClient(UpsShipmentAdapter.HttpClientName);
            builder.Services.AddSingleton<UpsTokenProvider>();
            AddInstrumentedAdapter<UpsShipmentAdapter>(builder.Services);
        }

        if (carriersOptions.FedEx.UseMock)
        {
            AddInstrumentedAdapter<MockFedExAdapter>(builder.Services);
        }
        else
        {
            AddInstrumentedAdapter<FedExShipmentAdapter>(builder.Services);
        }

        builder.Services.AddScoped<ICarrierAdapterFactory, CarrierAdapterFactory>();

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(InstrumentedCarrierAdapter.ActivitySourceName));
    }

    private static void AddInstrumentedAdapter<TAdapter>(IServiceCollection services)
        where TAdapter : class, ICarrierShipmentAdapter
    {
        services.AddScoped<TAdapter>();
        services.AddScoped<ICarrierShipmentAdapter>(sp =>
            ActivatorUtilities.CreateInstance<InstrumentedCarrierAdapter>(sp, sp.GetRequiredService<TAdapter>()));
    }
}
