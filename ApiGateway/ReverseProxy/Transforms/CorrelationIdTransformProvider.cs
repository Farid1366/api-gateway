using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ApiGateway.ReverseProxy.Transforms;

public sealed class CorrelationIdTransformProvider : ITransformProvider
{
    private const string HeaderName = "X-Correlation-Id";

    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transform =>
        {
            var incoming = transform.HttpContext.Request.Headers[HeaderName].ToString();
            var id = string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString("N") : incoming;

            transform.HttpContext.Items[HeaderName] = id;
            transform.ProxyRequest.Headers.Remove(HeaderName);
            transform.ProxyRequest.Headers.Add(HeaderName, id);
            return ValueTask.CompletedTask;
        });

        context.AddResponseTransform(transform =>
        {
            if (transform.HttpContext.Items.TryGetValue(HeaderName, out var id) && id is string s)
            {
                transform.HttpContext.Response.Headers[HeaderName] = s;
            }
            return ValueTask.CompletedTask;
        });
    }
}
