using System.Text.Json;
using TelegramChatFlow.Builder.Step;

namespace TelegramChatFlow.Builder.Flow;

/// <summary>Builder fluent per la definizione di un flusso tipizzato.</summary>
public sealed class FlowBuilder<TData> where TData : class, new()
{
    private readonly List<StepDefinition> _steps = [];
    private readonly List<FlowDefinition> _subFlows = [];

    /// <summary>Aggiunge uno step al flusso.</summary>
    public FlowBuilder<TData> Step(string id, Action<StepBuilder<TData>> configure)
    {
        var defaultOrdinal = _steps.Count * 10;
        var sb = new StepBuilder<TData>(id, defaultOrdinal);
        configure(sb);
        _steps.Add(sb.Build());
        return this;
    }

    /// <summary>Aggiunge un sub-flow definito tramite <see cref="FlowBase{TData}"/>.</summary>
    public FlowBuilder<TData> SubFlow(FlowBase<TData> flow)
    {
        _subFlows.Add(flow.Build());
        return this;
    }

    internal FlowDefinition Build(string id, string label) => new()
    {
        Id = id,
        Label = label,
        Steps = [.._steps.OrderBy(s => s.Ordinal)],
        SubFlows = _subFlows.Count > 0 ? _subFlows : null,
        CreateContext = data => new FlowContext<TData> { FlowData = data },
        CreateData = () => new TData(),
        CloneData = data => JsonSerializer.Deserialize<TData>(JsonSerializer.Serialize((TData)data))!
    };
}
