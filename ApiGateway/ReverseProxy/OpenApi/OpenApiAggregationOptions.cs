namespace ApiGateway.ReverseProxy.OpenApi;

public sealed class OpenApiAggregationOptions
{
    public string Title { get; set; } = "API Gateway";
    public string Version { get; set; } = "v1";
    public string Description { get; set; } = "Aggregated OpenAPI across proxied services.";
    public int CacheSeconds { get; set; } = 30;
    public List<OpenApiSource> Sources { get; set; } = new();
}

public sealed class OpenApiSource
{
    public string Name { get; set; } = string.Empty;
    public string OpenApiUrl { get; set; } = string.Empty;
    public Dictionary<string, string> PathMap { get; set; } = new();
}
