using ApiGateway.ReverseProxy.OpenApi;

namespace ApiGateway.ReverseProxy.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddGatewayOpenApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env)
    {
        services.Configure<OpenApiAggregationOptions>(configuration.GetSection("OpenApiAggregation"));
        services.AddMemoryCache();
        services.AddSingleton<OpenApiAggregator>();

        var builder = services.AddHttpClient(OpenApiAggregator.HttpClientName);
        if (env.IsDevelopment())
        {
            builder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        return services;
    }

    public static WebApplication MapGatewayOpenApi(this WebApplication app)
    {
        app.MapGet("/openapi/v1.json", async (OpenApiAggregator aggregator, CancellationToken ct) =>
        {
            var doc = await aggregator.GetAggregatedAsync(ct);
            return Results.Text(doc.ToJsonString(), "application/json");
        });

        return app;
    }
}
