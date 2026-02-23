using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Controlla periodicamente le sessioni attive e resetta quelle inattive
/// oltre la soglia configurata.
/// </summary>
public sealed class InactivityWatchdog : IDisposable
{
    private readonly ISessionStore _store;
    private readonly FlowEngine _engine;
    private readonly FlowBotOptions _options;
    private readonly ILogger<InactivityWatchdog> _logger;
    private Timer? _timer;

    public InactivityWatchdog(
        ISessionStore store,
        FlowEngine engine,
        IOptions<FlowBotOptions> options,
        ILogger<InactivityWatchdog> logger)
    {
        _store = store;
        _engine = engine;
        _options = options.Value;
        _logger = logger;
    }

    public void Start()
    {
        _timer = new Timer(
            _ => _ = CheckAsync(),
            null,
            _options.WatchdogInterval,
            _options.WatchdogInterval);
    }

    private async Task CheckAsync()
    {
        try
        {
            var sessions = await _store.GetAllAsync();
            var cutoff = DateTime.UtcNow - _options.InactivityTimeout;

            foreach (var session in sessions)
            {
                if (session.CurrentFlowId is null || session.LastActivity >= cutoff) continue;
                _logger.LogInformation("Sessione chat {ChatId} scaduta per inattività", session.ChatId);

                await _engine.HandleInactivityAsync(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nel watchdog di inattività");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
