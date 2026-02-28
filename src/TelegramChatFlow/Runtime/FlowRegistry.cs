namespace TelegramChatFlow.Runtime;


// OK VISTO
/// <summary>
/// Flow registry: handles recursive registration and lookup by ID.
/// </summary>
public sealed class FlowRegistry
{
    private readonly Dictionary<string, FlowDefinition> _rootFlows = new();
    private readonly Dictionary<string, FlowDefinition> _allFlows = new();

    public FlowRegistry(IReadOnlyList<FlowDefinition> flows)
    {
        foreach (var def in flows)
        {
            _rootFlows[def.Id] = def;
            RegisterRecursive(def);
        }
    }

    /// <summary>Returns a flow (root or sub-flow) by ID, or null if not found.</summary>
    public FlowDefinition GetFlow(string id) => _allFlows.GetValueOrDefault(id) ?? throw new KeyNotFoundException($"Flow with ID '{id}' not found.");
    

    /// <summary>All registered root flows.</summary>
    public IReadOnlyCollection<FlowDefinition> RootFlows => _rootFlows.Values;

    private void RegisterRecursive(FlowDefinition flow)
    {
        _allFlows[flow.Id] = flow;
        foreach (var sub in flow.SubFlows ?? [])
            RegisterRecursive(sub);
    }
}