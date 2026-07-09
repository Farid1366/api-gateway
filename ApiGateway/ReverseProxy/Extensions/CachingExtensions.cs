namespace ApiGateway.ReverseProxy.Extensions;

public static class CachingExtensions
{
    public static IServiceCollection AddGatewayOutputCache(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            options.AddPolicy("catalog-short", b => b
                .Expire(TimeSpan.FromSeconds(60))
                .Tag("music-catalog"));

            options.AddPolicy("static-long", b => b
                .Expire(TimeSpan.FromHours(24))
                .Tag("music-uploads"));
        });

        return services;
    }
}
