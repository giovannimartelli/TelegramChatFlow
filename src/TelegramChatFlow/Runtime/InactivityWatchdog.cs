using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TelegramChatFlow.Runtime;

// OK VISTO

/// <summary>
/// Periodically checks active sessions and resets those that are inactive
/// beyond the configured threshold.
/// </summary>
public sealed class InactivityWatchdog(
    ISessionStore store,
    FlowEngine engine,
    IOptions<FlowBotOptions> options,
    ILogger<InactivityWatchdog> logger)
    : IDisposable
{
    private readonly FlowBotOptions _options = options.Value;
    private Timer? _timer;

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
            var sessions = await store.GetAllAsync();
            var cutoff = DateTime.UtcNow - _options.InactivityTimeout;

            foreach (var session in sessions)
            {
                if (session.CurrentFlowId is null || session.LastActivity >= cutoff) continue;
                logger.LogInformation("Chat session {ChatId} expired due to inactivity", session.ChatId);

                await engine.HandleInactivityAsync(session);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in inactivity watchdog");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
