using TelegramChatFlow.Builder.Step;

namespace TelegramChatFlow.Builder.Input;

/// <summary>
/// Builder tipizzato per la configurazione dell'handler di input di uno step.
/// <typeparamref name="TInput"/> è il tipo estratto da <see cref="UserInput"/> in base al tipo di input dichiarato.
/// <typeparamref name="TData"/> è il tipo dati del flusso.
/// </summary>
public sealed class InputBuilder<TInput, TData> where TData : class, new()
{
    private readonly StepBuilder<TData> _parent;
    private readonly Func<UserInput, TInput> _extractor;

    internal InputBuilder(StepBuilder<TData> parent, Func<UserInput, TInput> extractor)
    {
        _parent = parent;
        _extractor = extractor;
    }

    /// <summary>Handler con risultato asincrono completo.</summary>
    public InputBuilder<TInput, TData> OnInput(Func<FlowContext<TData>, TInput, Task<StepResult>> handler)
    {
        _parent.SetHandler((ctx, input) => handler((FlowContext<TData>)ctx, _extractor(input)));
        return this;
    }

    /// <summary>Handler sincrono con <see cref="StepResult"/>.</summary>
    public InputBuilder<TInput, TData> OnInput(Func<FlowContext<TData>, TInput, StepResult> handler)
    {
        _parent.SetHandler((ctx, input) => Task.FromResult(handler((FlowContext<TData>)ctx, _extractor(input))));
        return this;
    }

    /// <summary>Handler semplificato: esegue un'azione e avanza sempre al prossimo step.</summary>
    public InputBuilder<TInput, TData> OnInput(Action<FlowContext<TData>, TInput> handler)
    {
        _parent.SetHandler((ctx, input) =>
        {
            handler((FlowContext<TData>)ctx, _extractor(input));
            return Task.FromResult(StepResult.Next);
        });
        return this;
    }
}
