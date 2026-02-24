using DotnetBackend.Data;
using DotnetBackend.Endpoints;
using DotnetBackend.Models;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting dotnet-backend");

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

builder.Services.AddSingleton<DataStore>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var outputCacheConfig = builder.Configuration.GetSection("ApiPolicies:OutputCache");
var usersCacheDuration = outputCacheConfig.GetValue<int>("UsersCacheDurationSeconds", 3);
var tasksCacheDuration = outputCacheConfig.GetValue<int>("TasksCacheDurationSeconds", 3);

var rateLimitConfig = builder.Configuration.GetSection("ApiPolicies:RateLimit");
var rateLimitPermit = rateLimitConfig.GetValue<int>("PermitLimit", 1);
var rateLimitWindow = rateLimitConfig.GetValue<int>("WindowSeconds", 1);

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("Users", policy =>
        policy.Expire(TimeSpan.FromSeconds(usersCacheDuration)));
    options.AddPolicy("Tasks", policy =>
        policy.Expire(TimeSpan.FromSeconds(tasksCacheDuration)));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = rateLimitPermit;
        limiterOptions.Window = TimeSpan.FromSeconds(rateLimitWindow);
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddHealthChecks().AddUrlGroup(new Uri("https://github.com"), name: "github", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

var serviceName = builder.Environment.ApplicationName;
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()  // standard ASP.NET Core HTTP server metrics
        .AddPrometheusExporter());       // scraped at /metrics

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature is not null)
        {
            Log.Error(exceptionFeature.Error, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred" });
    });
});

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) =>
        ex != null || httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400
                ? LogEventLevel.Warning
                : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

app.UseCors();
app.UseRateLimiter();
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

const int defaultPort = 8080;
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!int.TryParse(portEnv, out var port))
{
    port = defaultPort;
}

app.MapHealthChecks("/health");

// Prometheus metrics scraping endpoint — GET /metrics
app.MapPrometheusScrapingEndpoint();

app.MapUserEndpoints();
app.MapTaskEndpoints();
app.MapUtilityEndpoints();

app.MapGet("/api/stats", (DataStore store) =>
{
    var stats = store.GetStats();
    return Results.Json(stats);
});

app.Run($"http://0.0.0.0:{port}");
}
catch (Exception ex)
{
    Log.Fatal(ex, "dotnet-backend terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
