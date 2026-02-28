using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace TelegramChatFlow.Runtime;
// OK VISTO

/// <summary>
/// Hosted service that starts Telegram polling and the inactivity watchdog.
/// </summary>
public sealed class BotHostedService(
    ITelegramBotClient bot,
    FlowEngine engine,
    InactivityWatchdog watchdog,
    ILogger<BotHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting bot...");
        await engine.InitializeAsync();

        watchdog.Start();

        var me = await bot.GetMe(stoppingToken);
        logger.LogInformation("Bot Started: @{Username}", me.Username);

        bot.StartReceiving(
            updateHandler: (_, update, _) => engine.HandleUpdateAsync(update),
            errorHandler: (_, ex, source, _) =>
            {
                logger.LogError(ex, "Polling Error ({Source})", source);
                return Task.CompletedTask;
            },
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery]
            },
            cancellationToken: stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shoting down bot...");
        }
    }
}
