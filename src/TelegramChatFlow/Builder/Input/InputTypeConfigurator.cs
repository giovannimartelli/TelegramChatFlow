using TelegramChatFlow.Builder.Step;

namespace TelegramChatFlow.Builder.Input;

/// <summary>Configuratore del tipo di input atteso da uno step.</summary>
public sealed class InputTypeConfigurator<TData> where TData : class, new()
{
    private readonly StepBuilder<TData> _parent;

    internal InputTypeConfigurator(StepBuilder<TData> parent) => _parent = parent;

    // ── Buttons ──────────────────────────────────────────

    public InputBuilder<string, TData> UsingButtons(params InlineButton[] buttons)
    {
        _parent.SetInputType(InputType.InlineButtons);
        _parent.SetButtonsProvider(_ => Task.FromResult<IReadOnlyList<InlineButton>>(buttons));
        return new InputBuilder<string, TData>(_parent, input => input.CallbackData!);
    }

    public InputBuilder<string, TData> UsingButtons(Func<FlowContext<TData>, IReadOnlyList<InlineButton>> provider)
    {
        _parent.SetInputType(InputType.InlineButtons);
        _parent.SetButtonsProvider(ctx => Task.FromResult(provider((FlowContext<TData>)ctx)));
        return new InputBuilder<string, TData>(_parent, input => input.CallbackData!);
    }

    public InputBuilder<string, TData> UsingButtons(Func<FlowContext<TData>, Task<IReadOnlyList<InlineButton>>> provider)
    {
        _parent.SetInputType(InputType.InlineButtons);
        _parent.SetButtonsProvider(ctx => provider((FlowContext<TData>)ctx));
        return new InputBuilder<string, TData>(_parent, input => input.CallbackData!);
    }

    // ── Text ─────────────────────────────────────────────

    public InputBuilder<string, TData> UsingText()
    {
        _parent.SetInputType(InputType.Text);
        return new InputBuilder<string, TData>(_parent, input => input.Text!);
    }

    // ── Media ─────────────────────────────────────────────

    public InputBuilder<InputMedia, TData> UsingMedia()
    {
        _parent.SetInputType(InputType.Media);
        return new InputBuilder<InputMedia, TData>(_parent, input => input.Media!);
    }

    // ── WebApp ───────────────────────────────────────────

    public InputBuilder<string, TData> UsingWebApp(string url)
    {
        _parent.SetInputType(InputType.WebApp);
        _parent.SetWebAppUrlProvider(_ => Task.FromResult(url));
        return new InputBuilder<string, TData>(_parent, input => input.WebAppData!);
    }

    public InputBuilder<string, TData> UsingWebApp(Func<FlowContext<TData>, string> urlProvider)
    {
        _parent.SetInputType(InputType.WebApp);
        _parent.SetWebAppUrlProvider(ctx => Task.FromResult(urlProvider((FlowContext<TData>)ctx)));
        return new InputBuilder<string, TData>(_parent, input => input.WebAppData!);
    }

    public InputBuilder<string, TData> UsingWebApp(Func<FlowContext<TData>, Task<string>> urlProvider)
    {
        _parent.SetInputType(InputType.WebApp);
        _parent.SetWebAppUrlProvider(ctx => urlProvider((FlowContext<TData>)ctx));
        return new InputBuilder<string, TData>(_parent, input => input.WebAppData!);
    }

    // ── Reply Keyboard ───────────────────────────────────

    public InputBuilder<string, TData> UsingKeyboard(params string[] buttons)
    {
        _parent.SetInputType(InputType.ReplyKeyboard);
        _parent.SetReplyKeyboardProvider(_ => Task.FromResult<IReadOnlyList<string>>(buttons));
        return new InputBuilder<string, TData>(_parent, input => input.Text!);
    }

    public InputBuilder<string, TData> UsingKeyboard(Func<FlowContext<TData>, IReadOnlyList<string>> provider)
    {
        _parent.SetInputType(InputType.ReplyKeyboard);
        _parent.SetReplyKeyboardProvider(ctx => Task.FromResult(provider((FlowContext<TData>)ctx)));
        return new InputBuilder<string, TData>(_parent, input => input.Text!);
    }

    public InputBuilder<string, TData> UsingKeyboard(Func<FlowContext<TData>, Task<IReadOnlyList<string>>> provider)
    {
        _parent.SetInputType(InputType.ReplyKeyboard);
        _parent.SetReplyKeyboardProvider(ctx => provider((FlowContext<TData>)ctx));
        return new InputBuilder<string, TData>(_parent, input => input.Text!);
    }
}
