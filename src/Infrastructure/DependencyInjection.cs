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
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
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
            builder.Services.AddScoped<ICarrierShipmentAdapter, MockUpsAdapter>();
        }
        else
        {
            builder.Services.AddScoped<ICarrierShipmentAdapter, UpsShipmentAdapter>();
        }

        if (carriersOptions.FedEx.UseMock)
        {
            builder.Services.AddScoped<ICarrierShipmentAdapter, MockFedExAdapter>();
        }
        else
        {
            builder.Services.AddScoped<ICarrierShipmentAdapter, FedExShipmentAdapter>();
        }

        builder.Services.AddScoped<ICarrierAdapterFactory, CarrierAdapterFactory>();
    }
}
