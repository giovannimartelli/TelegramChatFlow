namespace TelegramChatFlow.Models.Flow;

/// <summary>Definizione completa di un flusso conversazionale.</summary>
public sealed class FlowDefinition
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<StepDefinition> Steps { get; init; }
    public IReadOnlyList<FlowDefinition>? SubFlows { get; init; }

    /// <summary>Crea un <see cref="FlowContext"/> tipizzato a partire dall'oggetto dati.</summary>
    public required Func<object, FlowContext> CreateContext { get; init; }

    /// <summary>Crea una nuova istanza dell'oggetto dati del flusso.</summary>
    public required Func<object> CreateData { get; init; }

    /// <summary>Clona l'oggetto dati del flusso (deep copy via JSON).</summary>
    public required Func<object, object> CloneData { get; init; }
}
