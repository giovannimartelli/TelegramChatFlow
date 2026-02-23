using TelegramChatFlow.Builder.Step;

namespace TelegramChatFlow.Builder.Input;

/// <summary>
/// Builder tipizzato per la configurazione dell'handler di input di uno step.
/// <typeparamref name="TInput"/> è il tipo estratto da <see cref="UserInput"/> in base al tipo di input dichiarato.
/// </summary>
public sealed class InputBuilder<TInput>
{
    private readonly StepBuilder _parent;
    private readonly Func<UserInput, TInput> _extractor;

    internal InputBuilder(StepBuilder parent, Func<UserInput, TInput> extractor)
    {
        _parent = parent;
        _extractor = extractor;
    }

    /// <summary>Handler con risultato asincrono completo.</summary>
    public InputBuilder<TInput> OnInput(Func<FlowContext, TInput, Task<StepResult>> handler)
    {
        _parent.SetHandler((ctx, input) => handler(ctx, _extractor(input)));
        return this;
    }

    /// <summary>Handler sincrono con <see cref="StepResult"/>.</summary>
    public InputBuilder<TInput> OnInput(Func<FlowContext, TInput, StepResult> handler)
    {
        _parent.SetHandler((ctx, input) => Task.FromResult(handler(ctx, _extractor(input))));
        return this;
    }

    /// <summary>Handler semplificato: esegue un'azione e avanza sempre al prossimo step.</summary>
    public InputBuilder<TInput> OnInput(Action<FlowContext, TInput> handler)
    {
        _parent.SetHandler((ctx, input) =>
        {
            handler(ctx, _extractor(input));
            return Task.FromResult(StepResult.Next);
        });
        return this;
    }
}
