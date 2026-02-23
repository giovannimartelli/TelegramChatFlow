namespace TelegramChatFlow.Runtime;

/// <summary>
/// Registro dei flussi: gestisce la registrazione ricorsiva e il lookup per ID.
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

    /// <summary>Restituisce un flusso (root o sub-flow) per ID, o null se non trovato.</summary>
    public FlowDefinition? GetFlow(string id) => _allFlows.GetValueOrDefault(id);

    /// <summary>Restituisce un flusso root per ID, o null se non trovato.</summary>
    public FlowDefinition? GetRootFlow(string id) => _rootFlows.GetValueOrDefault(id);

    /// <summary>Restituisce true se il flusso root esiste.</summary>
    public bool TryGetRootFlow(string id, out FlowDefinition flow) =>
        _rootFlows.TryGetValue(id, out flow!);

    /// <summary>Tutti i flussi root registrati.</summary>
    public IReadOnlyCollection<FlowDefinition> RootFlows => _rootFlows.Values;

    private void RegisterRecursive(FlowDefinition flow)
    {
        _allFlows[flow.Id] = flow;
        if (flow.SubFlows is not null)
            foreach (var sub in flow.SubFlows)
                RegisterRecursive(sub);
    }
}
