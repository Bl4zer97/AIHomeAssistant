using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Infrastructure;
using AIHomeAssistant.Infrastructure.Migrations;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Json;

// Bootstrap logger captures startup errors before DI is configured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext:l} » {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            formatter: new JsonFormatter(),
            path: "logs/aihomeassistant-.log",
            rollingInterval: RollingInterval.Day));

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    // Hot-reloadable entity mapping from config/entities.json
    builder.Configuration.AddJsonFile("config/entities.json", optional: true, reloadOnChange: true);
    builder.Services.Configure<EntityMappingOptions>(builder.Configuration);

    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // Log entity mapping reloads (hot-reload via IOptionsMonitor)
    var entityMappingMonitor = app.Services.GetRequiredService<IOptionsMonitor<EntityMappingOptions>>();
    entityMappingMonitor.OnChange(opts =>
        Log.Information("Entity mapping reloaded: {LightCount} lights, {ClimateCount} climate zones",
            opts.Lights.Count, opts.Climate.Count));

    // Run DbUp migrations synchronously before accepting requests
    var connectionString = builder.Configuration.GetConnectionString("Sqlite")
        ?? throw new InvalidOperationException("Sqlite connection string is not configured.");
    DbUpMigrator.Migrate(connectionString);

    app.UseSerilogRequestLogging();
    app.MapOpenApi();   // serves OpenAPI doc at /openapi/v1.json
    app.UseMiddleware<AIHomeAssistant.Api.Middleware.ExceptionHandlerMiddleware>();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
