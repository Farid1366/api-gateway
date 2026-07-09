# API Gateway

YARP-based reverse proxy on **.NET 10** that fronts three microservices and centralizes JWT auth, CORS, rate limiting, and health checks.

## Layout

```
api-gateway.slnx
ApiGateway/
├── Program.cs                              # pipeline wiring, endpoint mapping
├── appsettings.json                        # routes, clusters, JWT/CORS/aggregation config
├── appsettings.Development.json            # dev JWT signing key (do not commit real secrets)
└── ReverseProxy/
    ├── Extensions/
    │   ├── AuthenticationExtensions.cs     # symmetric-key JWT bearer setup
    │   ├── AuthorizationExtensions.cs      # policies: authenticated, admin, read-scope, write-scope
    │   ├── CorsExtensions.cs               # "gateway-cors" policy from Cors:AllowedOrigins
    │   ├── OpenApiExtensions.cs            # /openapi/v1.json endpoint registration
    │   ├── RateLimitingExtensions.cs       # policies: fixed-per-user, burst-per-ip, uploads
    │   └── ReverseProxyExtensions.cs       # YARP + transforms + service discovery
    ├── OpenApi/
    │   ├── OpenApiAggregationOptions.cs
    │   └── OpenApiAggregator.cs            # fetches + merges downstream OpenAPI docs
    └── Transforms/
        └── CorrelationIdTransformProvider.cs
```

## Downstream services

| Service       | Port  | Gateway path prefixes                                   |
|---------------|-------|---------------------------------------------------------|
| Identity.API  | 5001  | `/identity/auth/*`, `/identity/users/*`, `/identity/admin/*` |
| Order.API     | 5004  | `/orders/*` (all methods, `authenticated`)              |
| Music.API     | 5005  | `/music/*`, `/music/uploads/*`, `/uploads/*` (static)   |

## Auth pattern (must stay consistent across gateway + services)

- **HS256** with a shared symmetric secret
- `Issuer = identity-api`, `Audience = identity-api-clients`
- `MapInboundClaims = false`, `NameClaimType = JwtRegisteredClaimNames.UniqueName`, `RoleClaimType = "role"`, `ClockSkew = TimeSpan.Zero`
- Same `SigningKey` byte-for-byte across gateway, Identity.API, Music.API, Order.API. Dev value lives in `appsettings.Development.json`; prod should use user-secrets or env vars.

## YARP route conventions

- Every route explicitly sets `AuthorizationPolicy`, `RateLimiterPolicy`, `CorsPolicy`.
- CORS policy is named `gateway-cors`. **Never** use `Default` or `Disable` — YARP reserves those (case-insensitive) and startup will throw.
- Rate-limit partition keys prefer `sub` claim → `Name` → IP.
- Clusters set `DangerousAcceptAnyServerCertificate: true` for local dev certs. Remove for prod.

## Endpoints on the gateway itself

- `/healthz`, `/ready` — basic health checks
- `/openapi/v1.json` — merged OpenAPI across all downstream services, cached 30s
- `/scalar/v1` — Scalar UI (dev only)

## Run / build

```powershell
dotnet build                              # from api-gateway/
dotnet run --project ApiGateway           # HTTPS https://localhost:5000, HTTP http://localhost:6000
```

Gateway must be launched with the `https` profile for `/scalar/v1` to be at port 5000. All three downstream services must be running for aggregated OpenAPI to populate.

## Adding a new downstream service

1. Add a `Clusters:<name>-cluster` block in `appsettings.json` with the destination address.
2. Add one or more `Routes:*` entries under a distinct path prefix, setting `AuthorizationPolicy` / `RateLimiterPolicy` / `CorsPolicy`.
3. Use `Transforms: [{ "PathPattern": "..." }]` to rewrite the path from the gateway prefix to the downstream's route.
4. Add an entry to `OpenApiAggregation:Sources` with `Name`, `OpenApiUrl`, and `PathMap` (longest-prefix-wins) so it shows up in Scalar.

## Known gotchas

- **File lock on rebuild**: `dotnet build` fails with MSB3021 if a running `ApiGateway.exe` holds the output. Stop the process first.
- **`Cors:AllowedOrigins` + `AllowCredentials`**: `CorsExtensions` uses `AllowCredentials` only when specific origins are listed; empty list falls back to `AllowAnyOrigin` (which is incompatible with credentials).
- **Order.API JWT config divergence**: if Order.API's `Jwt` section doesn't match identity's (`Issuer`, `Audience`, `SigningKey`), tokens issued by identity won't validate at Order.API. Applies to any service that adds `[Authorize]`.
- **`appsettings.Development.json` is not gitignored by default** — safe today because it only holds a placeholder signing key. Uncomment the ignore rule in `.gitignore` before putting a real secret there, or use `dotnet user-secrets`.
