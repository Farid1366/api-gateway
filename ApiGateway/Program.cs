using ApiGateway.ReverseProxy.Extensions;
using Microsoft.AspNetCore.HttpLogging;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestQuery |
        HttpLoggingFields.ResponseStatusCode |
        HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

builder.Services.AddHealthChecks();

builder.Services
    .AddGatewayCors(builder.Configuration)
    .AddGatewayAuthentication(builder.Configuration)
    .AddGatewayAuthorization()
    .AddGatewayRateLimiting()
    .AddGatewayReverseProxy(builder.Configuration)
    .AddGatewayOpenApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseHttpLogging();

app.UseCors("gateway-cors");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapHealthChecks("/healthz");
app.MapHealthChecks("/ready");

app.MapGatewayOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("API Gateway")
            .AddDocument("v1", "API Gateway v1", "/openapi/v1.json");
    });
}

app.MapReverseProxy();

app.Run();
