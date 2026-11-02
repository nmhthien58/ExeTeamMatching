using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Identity.Repositories.Accounts;
using Identity.Repositories.Context;
using Identity.Repositories.Tokens;
using Identity.Services.Auth;
using Identity.Services.Google;
using Identity.Services.Provisioning;
using Identity.Services.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Identity Service (BE-10 → BE-13): DbContext, kiểm tra Google ID token, phát JWT + refresh token, tạo sẵn tài khoản từ Kafka.</summary>
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("IdentityDb"))
            .UseSnakeCaseNamingConvention());

        services.TryAddSingleton(TimeProvider.System);
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<AccessTokenFactory>();
        services.AddScoped<TokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddKafka(configuration, "identity");
        services.AddProcessedEventStore<IdentityDbContext>();
        services.AddScoped<AccountProvisioningService>();
        services.AddProvisioningConsumers();
        return services;
    }
}
