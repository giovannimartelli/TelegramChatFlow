using TelegramChatFlow.Builder.Flow;

namespace TelegramChatFlow.Builder;

/// <summary>
/// Classe base da cui ereditano tutti i flussi definiti dal consumer.
/// </summary>
public abstract class FlowBase
{
    /// <summary>Identificativo univoco del flusso.</summary>
    public abstract string Id { get; }

    /// <summary>Etichetta mostrata nel menu.</summary>
    public abstract string MenuLabel { get; }

    /// <summary>Costruisce la <see cref="FlowDefinition"/> a partire dalla configurazione.</summary>
    public FlowDefinition Build()
    {
        var builder = new FlowBuilder();
        Configure(builder);
        return builder.Build(Id, MenuLabel);
    }

    /// <summary>Configura gli step e gli eventuali sub-flow.</summary>
    protected abstract void Configure(FlowBuilder builder);
}
