using ApiGateway.ReverseProxy.Transforms;

namespace ApiGateway.ReverseProxy.Extensions;

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddGatewayReverseProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var builder = services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms<CorrelationIdTransformProvider>();

        // Service discovery: resolves cluster destinations like "http://users" via
        // configured resolvers (Configuration, DNS SRV, etc.). See appsettings "Services".
        builder.AddServiceDiscoveryDestinationResolver();
        services.AddServiceDiscovery();

        return services;
    }
}
