using AIHomeAssistant.Infrastructure.Options;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace AIHomeAssistant.Infrastructure.Telegram;

/// <summary>
/// Sends notifications via the Telegram Bot API.
/// </summary>
public class TelegramNotificationService : INotificationService
{
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(IOptions<TelegramOptions> options, ILogger<TelegramNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PipelineResult> SendAsync(string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken) || _options.ChatId == 0)
        {
            _logger.LogWarning("Telegram credentials not configured");
            return new PipelineResult(false,
                new PipelineError("TELEGRAM_NOT_CONFIGURED", "Telegram bot token or chat ID not set"));
        }

        try
        {
            var bot = new TelegramBotClient(_options.BotToken);
            await bot.SendMessage(new ChatId(_options.ChatId), message, cancellationToken: ct);
            _logger.LogInformation("Telegram notification sent to chat {ChatId}", _options.ChatId);
            return new PipelineResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram dispatch failed: {Code}", "TELEGRAM_DISPATCH_FAILED");
            return new PipelineResult(false,
                new PipelineError("TELEGRAM_DISPATCH_FAILED", "Failed to send Telegram message", ex));
        }
    }
}
