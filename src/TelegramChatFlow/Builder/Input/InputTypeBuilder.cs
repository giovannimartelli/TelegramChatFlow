using TelegramChatFlow.Builder.Step;

namespace TelegramChatFlow.Builder.Input;

/// <summary>
/// Builder tipizzato per la configurazione dell'handler di input di uno step.
/// <typeparamref name="TInput"/> è il tipo estratto da <see cref="UserInput"/> in base al tipo di input dichiarato.
/// </summary>
public sealed class InputTypeBuilder<TInput>
{
    private readonly StepBuilder _parent;
    private readonly Func<UserInput, TInput> _extractor;

    internal InputTypeBuilder(StepBuilder parent, Func<UserInput, TInput> extractor)
    {
        _parent = parent;
        _extractor = extractor;
    }

    /// <summary>Handler con risultato asincrono completo.</summary>
    public InputTypeBuilder<TInput> OnInput(Func<FlowContext, TInput, Task<StepResult>> handler)
    {
        _parent.SetHandler((ctx, input) => handler(ctx, _extractor(input)));
        return this;
    }

    /// <summary>Handler sincrono con <see cref="StepResult"/>.</summary>
    public InputTypeBuilder<TInput> OnInput(Func<FlowContext, TInput, StepResult> handler)
    {
        _parent.SetHandler((ctx, input) => Task.FromResult(handler(ctx, _extractor(input))));
        return this;
    }

    /// <summary>Handler semplificato: esegue un'azione e avanza sempre al prossimo step.</summary>
    public InputTypeBuilder<TInput> OnInput(Action<FlowContext, TInput> handler)
    {
        _parent.SetHandler((ctx, input) =>
        {
            handler(ctx, _extractor(input));
            return Task.FromResult(StepResult.Next);
        });
        return this;
    }
}
