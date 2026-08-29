using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SquadCrm.Api;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Errors;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Infrastructure.Postgres;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root. This file wires infrastructure and composes modules.
// It contains no business rules — those belong inside modules.
// Configuration precedence is the ASP.NET Core default:
// appsettings.json -> appsettings.{Environment}.json -> environment variables -> CLI.
// ---------------------------------------------------------------------------

// Error contract: RFC 9457 Problem Details with a safe traceId.
builder.Services.AddSquadCrmProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();

// Authorization extension point for CRM-110. No scheme, no policy, no
// [Authorize] endpoint, no ICurrentUserAccessor registration yet — this only
// gives CRM-110 one place to plug in. Resolving ICurrentUserAccessor before
// CRM-110 registers an implementation fails DI resolution by design.
builder.Services.AddSquadCrmAuthorizationExtensionPoint();

// CORS, driven entirely by configuration.
CorsOptions corsOptions =
    builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

if (Array.Exists(corsOptions.AllowedOrigins, static origin => origin == "*"))
{
    // Fail fast rather than silently configuring an insecure policy.
    // The offending value is never logged — only the configuration key.
    throw new InvalidOperationException(
        $"Configuration key '{CorsOptions.SectionName}:{nameof(CorsOptions.AllowedOrigins)}' must not "
        + "contain the wildcard origin '*'. List explicit origins instead.");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // No credentials in this story; an empty allow-list blocks every origin.
        policy.WithOrigins(corsOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Built-in OpenAPI only, Development-only. No Swagger UI / Scalar / NSwag —
// consumers read the JSON document at /openapi/v1.json.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

// PostgreSQL coordinates, read once from the POSTGRES_* operator contract
// (CRM-197), validated fail-fast, and published internally as
// ConnectionStrings:SquadCrmPostgres for each module's own DbContext.
// No migration runs here: schema changes are applied by an explicit
// `dotnet ef database update`.
builder.AddSquadCrmPostgres();

// Liveness only. No database/storage/provider probes (owned by later stories).
builder.Services.AddHealthChecks();

// Explicit module list. A module that is not listed here is a compile-time
// absence, never a silent runtime gap. No runtime assembly scanning.
IModule[] modules =
[
    new SquadCrm.Modules.ArchitectureFixture.ArchitectureFixtureModule(),
];

builder.Services.RegisterModules(builder.Configuration, modules);

WebApplication app = builder.Build();

app.UseExceptionHandler();

// Registered first, right after the exception handler, so 500 responses
// carry the header baseline too — not after UseCors(), which would skip them.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = static async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    },
});

app.MapModuleEndpoints(modules);

await app.RunAsync();

/// <summary>
/// Test seam only: exposes the generated entry point to
/// <c>WebApplicationFactory&lt;Program&gt;</c>. Not a business surface.
/// </summary>
public partial class Program;
