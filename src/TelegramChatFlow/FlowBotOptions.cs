namespace TelegramChatFlow;

/// <summary>Bot configuration options.</summary>
public sealed class FlowBotOptions
{
    /// <summary>Telegram bot token.</summary>
    public required string BotToken { get; set; }

    /// <summary>
    /// Authorized user IDs. If empty, all users can use the bot.
    /// </summary>
    public HashSet<long> AllowedUsers { get; set; } = [];

    /// <summary>Inactivity timeout before automatic reset.</summary>
    public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Watchdog check interval.</summary>
    public TimeSpan WatchdogInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Main menu text.</summary>
    public string MainMenuText { get; set; } = "📋 Main Menu\n\nSelect an option:";
}
