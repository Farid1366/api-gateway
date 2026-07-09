namespace ApiGateway.ReverseProxy.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddGatewayAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("authenticated", policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy("admin", policy =>
                policy.RequireAuthenticatedUser().RequireRole("admin"));

            options.AddPolicy("read-scope", policy =>
                policy.RequireAuthenticatedUser().RequireClaim("scope", "api.read"));

            options.AddPolicy("write-scope", policy =>
                policy.RequireAuthenticatedUser().RequireClaim("scope", "api.write"));
        });

        return services;
    }
}
