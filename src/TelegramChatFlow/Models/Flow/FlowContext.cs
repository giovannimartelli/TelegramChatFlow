namespace TelegramChatFlow.Models.Flow;

/// <summary>
/// Contesto passato agli handler degli step.
/// Contiene i dati raccolti durante il flusso e un campo per messaggi di validazione.
/// </summary>
public sealed class FlowContext
{
    public Dictionary<string, object?> Data { get; internal set; } = new();

    /// <summary>Se impostato insieme a <see cref="StepResult.Retry"/>, viene mostrato all'utente.</summary>
    public string? ValidationError { get; set; }

    public T Get<T>(string key) => (T)Data[key]!;

    public T? TryGet<T>(string key) =>
        Data.TryGetValue(key, out var val) && val is T t ? t : default;

    public void Set(string key, object? value) => Data[key] = value;
}
