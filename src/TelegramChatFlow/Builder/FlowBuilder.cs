namespace TelegramChatFlow.Builder;

/// <summary>Builder fluent per la definizione di un flusso.</summary>
public sealed class FlowBuilder
{
    private readonly List<StepDefinition> _steps = [];
    private readonly List<FlowDefinition> _subFlows = [];

    /// <summary>Aggiunge uno step al flusso.</summary>
    public FlowBuilder Step(string id, Action<StepBuilder> configure)
    {
        var sb = new StepBuilder(id);
        configure(sb);
        _steps.Add(sb.Build());
        return this;
    }

    /// <summary>Aggiunge un sub-flow definito tramite <see cref="FlowBase"/>.</summary>
    public FlowBuilder SubFlow(FlowBase flow)
    {
        _subFlows.Add(flow.Build());
        return this;
    }

    internal FlowDefinition Build(string id, string label) => new()
    {
        Id = id,
        Label = label,
        Steps = _steps,
        SubFlows = _subFlows.Count > 0 ? _subFlows : null
    };
}
