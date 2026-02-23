namespace TelegramChatFlow.Runtime;

/// <summary>Stato persistente della conversazione di un singolo utente.</summary>
public sealed class FlowSession
{
    public long ChatId { get; init; }
    public string? CurrentFlowId { get; set; }
    public int CurrentStepIndex { get; set; }
    public Dictionary<string, object?> Data { get; set; } = new();
    public List<int> TrackedMessageIds { get; set; } = [];
    public List<int> PersistentMessageIds { get; set; } = [];
    public int? BotMessageId { get; set; }
    public bool HasReplyKeyboard { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public Stack<SubFlowFrame> FlowStack { get; set; } = new();
}

/// <summary>Frame salvato nello stack quando si entra in un sub-flow.</summary>
public sealed class SubFlowFrame
{
    public required string FlowId { get; init; }
    public required int StepIndex { get; init; }
    public required Dictionary<string, object?> Data { get; init; }
}

/// <summary>Interfaccia per la persistenza delle sessioni.</summary>
public interface ISessionStore
{
    Task<FlowSession?> GetAsync(long chatId);
    Task SaveAsync(FlowSession session);
    Task<IReadOnlyList<FlowSession>> GetAllAsync();
    Task DeleteAsync(long chatId);
}
