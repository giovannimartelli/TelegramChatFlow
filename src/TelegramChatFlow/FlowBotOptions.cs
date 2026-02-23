namespace TelegramChatFlow;

/// <summary>Opzioni di configurazione del bot.</summary>
public sealed class FlowBotOptions
{
    /// <summary>Token del bot Telegram.</summary>
    public required string BotToken { get; set; }

    /// <summary>
    /// ID degli utenti autorizzati. Se vuoto, tutti possono usare il bot.
    /// </summary>
    public HashSet<long> AllowedUsers { get; set; } = [];

    /// <summary>Timeout di inattività prima del reset automatico.</summary>
    public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Intervallo di controllo del watchdog.</summary>
    public TimeSpan WatchdogInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Testo del menu principale.</summary>
    public string MainMenuText { get; set; } = "📋 Menu Principale\n\nSeleziona un'opzione:";
}
