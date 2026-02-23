using Telegram.Bot.Types;

namespace TelegramChatFlow;

/// <summary>Tipo di input atteso da uno step.</summary>
public enum InputType
{
    InlineButtons,
    Text,
    Document,
    WebApp,
    ReplyKeyboard
}

/// <summary>Esito dell'elaborazione dell'input di uno step.</summary>
public enum StepResult
{
    /// <summary>Input valido, avanza allo step successivo.</summary>
    Next,
    /// <summary>Input non valido, resta sullo step corrente.</summary>
    Retry,
    /// <summary>Esci dal flusso.</summary>
    Exit
}

/// <summary>Bottone inline con testo e callback data.</summary>
public record InlineButton(string Text, string CallbackData);

/// <summary>Input ricevuto dall'utente.</summary>
public sealed class UserInput
{
    public string? Text { get; init; }
    public string? CallbackData { get; init; }
    public Document? Document { get; init; }
    public string? FileId { get; init; }
    public string? WebAppData { get; init; }
}

/// <summary>Definizione di uno step all'interno di un flusso.</summary>
public sealed class StepDefinition
{
    public required string Id { get; init; }
    public required Func<FlowContext, Task<string>> RenderText { get; init; }
    public InputType InputType { get; init; } = InputType.InlineButtons;
    public Func<FlowContext, IReadOnlyList<InlineButton>>? ButtonsProvider { get; init; }
    public Func<FlowContext, IReadOnlyList<string>>? ReplyKeyboardProvider { get; init; }
    public Func<FlowContext, string>? WebAppUrlProvider { get; init; }
    public required Func<FlowContext, UserInput, Task<StepResult>> HandleInput { get; init; }
    public bool Skippable { get; init; }
    public bool Persistent { get; init; }
}

/// <summary>Definizione completa di un flusso conversazionale.</summary>
public sealed class FlowDefinition
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<StepDefinition> Steps { get; init; }
    public IReadOnlyList<FlowDefinition>? SubFlows { get; init; }
}

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
