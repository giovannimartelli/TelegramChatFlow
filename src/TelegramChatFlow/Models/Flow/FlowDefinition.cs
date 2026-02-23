namespace TelegramChatFlow.Models.Flow;

/// <summary>Definizione completa di un flusso conversazionale.</summary>
public sealed class FlowDefinition
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<StepDefinition> Steps { get; init; }
    public IReadOnlyList<FlowDefinition>? SubFlows { get; init; }
}
