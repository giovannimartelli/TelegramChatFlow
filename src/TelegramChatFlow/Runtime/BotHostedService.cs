using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Hosted service che avvia il polling Telegram e il watchdog di inattività.
/// </summary>
public sealed class BotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly FlowEngine _engine;
    private readonly InactivityWatchdog _watchdog;
    private readonly ILogger<BotHostedService> _logger;

    public BotHostedService(
        ITelegramBotClient bot,
        FlowEngine engine,
        InactivityWatchdog watchdog,
        ILogger<BotHostedService> logger)
    {
        _bot = bot;
        _engine = engine;
        _watchdog = watchdog;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inizializzazione bot...");
        await _engine.InitializeAsync();

        _watchdog.Start();

        var me = await _bot.GetMe(stoppingToken);
        _logger.LogInformation("Bot avviato: @{Username}", me.Username);

        _bot.StartReceiving(
            updateHandler: (_, update, _) => _engine.HandleUpdateAsync(update),
            errorHandler: (_, ex, source, _) =>
            {
                _logger.LogError(ex, "Errore polling ({Source})", source);
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
            _logger.LogInformation("Bot in arresto...");
        }
    }
}
