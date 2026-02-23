namespace TelegramChatFlow.Models.Flow;

/// <summary>
/// Contesto passato agli handler degli step.
/// Contiene i dati raccolti durante il flusso e un campo per messaggi di validazione.
/// </summary>
public class FlowContext
{
    internal object FlowData { get; set; } = null!;

    /// <summary>Se impostato insieme a <see cref="StepResult.Retry"/>, viene mostrato all'utente.</summary>
    public string? ValidationError { get; set; }
}

/// <summary>
/// Contesto tipizzato per un flusso con dati di tipo <typeparamref name="TData"/>.
/// </summary>
public sealed class FlowContext<TData> : FlowContext where TData : class, new()
{
    public TData Data
    {
        get => FlowData as TData ?? throw new InvalidOperationException("Unexpected FlowData Type.");
        set => FlowData = value;
    }
}
