namespace TelegramChatFlow.Models.Steps;

/// <summary>Definizione di uno step all'interno di un flusso.</summary>
public sealed class StepDefinition
{
    public required string Id { get; init; }
    public required ShowDefinition Show { get; init; }
    public InputType InputType { get; init; } = InputType.None;
    public Func<FlowContext, Task<IReadOnlyList<InlineButton>>>? ButtonsProvider { get; init; }
    public Func<FlowContext, Task<IReadOnlyList<string>>>? ReplyKeyboardProvider { get; init; }
    public Func<FlowContext, Task<string>>? WebAppUrlProvider { get; init; }
    public required Func<FlowContext, UserInput, Task<StepResult>> HandleInput { get; init; }
    public bool Skippable { get; init; }
    public bool Persistent { get; init; }
    public int Ordinal { get; init; }
}
