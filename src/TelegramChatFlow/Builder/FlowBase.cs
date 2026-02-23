using TelegramChatFlow.Builder.Flow;

namespace TelegramChatFlow.Builder;

/// <summary>
/// Classe base da cui ereditano tutti i flussi definiti dal consumer.
/// </summary>
public abstract class FlowBase
{
    /// <summary>Identificativo univoco del flusso.</summary>
    protected abstract string Id { get; }

    /// <summary>Etichetta mostrata nel menu.</summary>
    protected abstract string MenuLabel { get; }

    /// <summary>Costruisce la <see cref="FlowDefinition"/> a partire dalla configurazione.</summary>
    public abstract FlowDefinition Build();
}

/// <summary>
/// Classe base generica: ogni flusso dichiara il proprio tipo dati <typeparamref name="TData"/>.
/// </summary>
public abstract class FlowBase<TData> : FlowBase where TData : class, new()
{
    public sealed override FlowDefinition Build()
    {
        var builder = new FlowBuilder<TData>();
        Configure(builder);
        return builder.Build(Id, MenuLabel);
    }

    /// <summary>Configura gli step e gli eventuali sub-flow.</summary>
    protected abstract void Configure(FlowBuilder<TData> builder);
}
