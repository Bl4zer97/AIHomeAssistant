using System.Net.Http.Headers;
using System.Threading.Channels;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Infrastructure.Audio;
using AIHomeAssistant.Infrastructure.Azure;
using AIHomeAssistant.Infrastructure.Data;
using AIHomeAssistant.Infrastructure.HomeAssistant;
using AIHomeAssistant.Infrastructure.Options;
using AIHomeAssistant.Infrastructure.Pipeline;
using AIHomeAssistant.Infrastructure.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIHomeAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Data ─────────────────────────────────────────────────────────────
        // Lazy-resolved from IConfiguration so TestWebApplicationFactory overrides work
        services.AddSingleton<SqliteConnectionFactory>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var cs = cfg.GetConnectionString("Sqlite")
                ?? throw new InvalidOperationException("Sqlite connection string is not configured.");
            return new SqliteConnectionFactory(cs);
        });
        services.AddScoped<ICommandRepository, CommandRepository>();
        services.AddScoped<IShoppingListRepository, ShoppingListRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();

        // ─── Options binding ───────────────────────────────────────────────────
        services.Configure<HomeAssistantOptions>(configuration.GetSection("HomeAssistant"));
        services.Configure<AudioOptions>(configuration.GetSection("Audio"));
        services.Configure<AzureOptions>(configuration.GetSection("Azure"));
        services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));

        // ─── Home Assistant HTTP client ────────────────────────────────────────
        services.AddHttpClient<HomeAssistantClient>(client =>
        {
            var baseUrl = configuration["HomeAssistant:BaseUrl"];
            var token   = configuration["HomeAssistant:Token"];

            if (!string.IsNullOrWhiteSpace(baseUrl))
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddScoped<IHomeAssistantClient, HomeAssistantClient>();

        // HA state cache — singleton + hosted service (double-registration pattern)
        services.AddSingleton<HaStateCacheService>();
        services.AddSingleton<IHaStateCacheService>(sp => sp.GetRequiredService<HaStateCacheService>());
        services.AddHostedService(sp => sp.GetRequiredService<HaStateCacheService>());

        // ─── Command routing ───────────────────────────────────────────────────
        services.AddScoped<ICommandRouter, CommandRouter>();

        // ─── Azure AI services ─────────────────────────────────────────────────
        services.AddSingleton<ISpeechToTextService, AzureSpeechToTextService>();
        services.AddSingleton<ITextToSpeechService, AzureTextToSpeechService>();
        services.AddSingleton<IIntentResolutionService, AzureOpenAiIntentService>();

        // Azure Face HTTP client (custom header per-request)
        services.AddHttpClient("AzureFace");
        services.AddSingleton<IVisionService, AzureVisionService>();

        // ─── Audio ─────────────────────────────────────────────────────────────
        services.AddSingleton<IWakeWordDetector, PorcupineWakeWordDetector>();
        services.AddSingleton<IAudioFeedbackService, AudioFeedbackService>();
        services.AddSingleton<IAudioCaptureService, NAudioCaptureService>();

        // Unbounded channel — WakeWordHostedService writes, VoicePipelineOrchestrator reads
        services.AddSingleton(_ => Channel.CreateUnbounded<AudioSegment>(
            new UnboundedChannelOptions { SingleReader = true }));
        services.AddHostedService<WakeWordHostedService>();

        // ─── Voice pipeline ────────────────────────────────────────────────────
        services.AddHostedService<VoicePipelineOrchestrator>();

        // ─── Telegram ─────────────────────────────────────────────────────────
        services.AddSingleton<INotificationService, TelegramNotificationService>();

        return services;
    }
}

