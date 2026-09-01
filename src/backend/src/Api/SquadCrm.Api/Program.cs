using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Hangfire;
using Hangfire.PostgreSql;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SquadCrm.Api;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Errors;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Infrastructure.FileStorage;
using SquadCrm.Modules.Audit;
using SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;
using SquadCrm.Modules.BranchManagement;
using SquadCrm.Modules.DepartmentManagement;
using SquadCrm.Modules.SystemConfiguration;

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

bool otlpExportEnabled = !string.IsNullOrWhiteSpace(
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: builder.Environment.ApplicationName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
        serviceNamespace: "SquadCrm"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsql()
            .AddSource(OutboxTelemetry.ActivitySourceName);

        if (otlpExportEnabled)
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Npgsql")
            .AddMeter(OutboxTelemetry.MeterName);

        if (otlpExportEnabled)
        {
            metrics.AddOtlpExporter();
        }
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;

    if (otlpExportEnabled)
    {
        logging.AddOtlpExporter();
    }
});

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
        // Refresh cookies require credentials; the explicit allow-list above prevents
        // credentialed cross-origin access from ever combining with a wildcard origin.
        policy.WithOrigins(corsOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
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
builder.AddSquadCrmFileStorage();

string postgresConnectionString = builder.Configuration.GetSquadCrmPostgresConnectionString();
bool backgroundProcessingEnabled = builder.Configuration.GetValue("BackgroundProcessing:Enabled", true);
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(postgresConnectionString),
        new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = true,
        }));
if (backgroundProcessingEnabled)
{
    builder.Services.AddHangfireServer();
    builder.Services.AddHostedService<ArchitectureFixtureRecurringJobRegistration>();
}

// Registrations contributed by infrastructure/modules are tagged "ready".
// An untagged empty check set is intentional liveness: process/pipeline only.
builder.Services.AddHealthChecks();

// Explicit module list. A module that is not listed here is a compile-time
// absence, never a silent runtime gap. No runtime assembly scanning.
IModule[] modules =
[
    new AuditModule(),
    new SquadCrm.Modules.StaffIdentity.StaffIdentityModule(),
    new SquadCrm.Modules.RoleManagement.RoleManagementModule(),
    new DepartmentManagementModule(),
    new BranchManagementModule(),
    new SystemConfigurationModule(),
    new SquadCrm.Modules.ArchitectureFixture.ArchitectureFixtureModule(),
];

builder.Services.RegisterModules(builder.Configuration, modules);

WebApplication app = builder.Build();

app.UseExceptionHandler();

// Registered first, right after the exception handler, so 500 responses
// carry the header baseline too — not after UseCors(), which would skip them.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    if (backgroundProcessingEnabled)
    {
        app.UseHangfireDashboard("/hangfire");
    }
}

HealthCheckOptions livenessOptions = new()
{
    Predicate = static _ => false,
    ResponseWriter = WriteHealthResponseAsync,
};
HealthCheckOptions readinessOptions = new()
{
    Predicate = static registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync,
};

app.MapHealthChecks("/health", livenessOptions);
app.MapHealthChecks("/health/live", livenessOptions);
app.MapHealthChecks("/health/ready", readinessOptions);

app.MapModuleEndpoints(modules);

await app.RunAsync();

static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    await JsonSerializer.SerializeAsync(
        context.Response.Body,
        new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    data = entry.Value.Data,
                }),
        },
        cancellationToken: context.RequestAborted);
}

/// <summary>
/// Test seam only: exposes the generated entry point to
/// <c>WebApplicationFactory&lt;Program&gt;</c>. Not a business surface.
/// </summary>
public partial class Program;
