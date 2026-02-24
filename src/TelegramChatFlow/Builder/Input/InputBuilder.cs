using TelegramChatFlow.Builder.Step;

namespace TelegramChatFlow.Builder.Input;

/// <summary>
/// Typed builder for configuring the input handler of a step.
/// <typeparamref name="TInput"/> is the type extracted from <see cref="UserInput"/> based on the declared input type.
/// <typeparamref name="TData"/> is the flow's data type.
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

    /// <summary>Handler with full async result.</summary>
    public InputBuilder<TInput, TData> OnInput(Func<FlowContext<TData>, TInput, Task<StepResult>> handler)
    {
        _parent.SetHandler((ctx, input) => handler((FlowContext<TData>)ctx, _extractor(input)));
        return this;
    }

    /// <summary>Synchronous handler with <see cref="StepResult"/>.</summary>
    public InputBuilder<TInput, TData> OnInput(Func<FlowContext<TData>, TInput, StepResult> handler)
    {
        _parent.SetHandler((ctx, input) => Task.FromResult(handler((FlowContext<TData>)ctx, _extractor(input))));
        return this;
    }

    /// <summary>Simplified handler: executes an action and always advances to the next step.</summary>
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
