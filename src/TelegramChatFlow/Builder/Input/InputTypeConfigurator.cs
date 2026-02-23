using TelegramChatFlow.Builder.Step;

namespace TelegramChatFlow.Builder.Input;

/// <summary>Configuratore del tipo di input atteso da uno step.</summary>
public sealed class InputTypeConfigurator
{
    private readonly StepBuilder _parent;

    internal InputTypeConfigurator(StepBuilder parent) => _parent = parent;

    // ── Buttons ──────────────────────────────────────────

    public InputBuilder<string> UsingButtons(params InlineButton[] buttons)
    {
        _parent.SetInputType(InputType.InlineButtons);
        _parent.SetButtonsProvider(_ => Task.FromResult<IReadOnlyList<InlineButton>>(buttons));
        return new InputBuilder<string>(_parent, input => input.CallbackData!);
    }

    public InputBuilder<string> UsingButtons(Func<FlowContext, IReadOnlyList<InlineButton>> provider)
    {
        _parent.SetInputType(InputType.InlineButtons);
        _parent.SetButtonsProvider(ctx => Task.FromResult(provider(ctx)));
        return new InputBuilder<string>(_parent, input => input.CallbackData!);
    }

    public InputBuilder<string> UsingButtons(Func<FlowContext, Task<IReadOnlyList<InlineButton>>> provider)
    {
        _parent.SetInputType(InputType.InlineButtons);
        _parent.SetButtonsProvider(provider);
        return new InputBuilder<string>(_parent, input => input.CallbackData!);
    }

    // ── Text ─────────────────────────────────────────────

    public InputBuilder<string> UsingText()
    {
        _parent.SetInputType(InputType.Text);
        return new InputBuilder<string>(_parent, input => input.Text!);
    }

    // ── Media ─────────────────────────────────────────────

    public InputBuilder<InputMedia> UsingMedia()
    {
        _parent.SetInputType(InputType.Media);
        return new InputBuilder<InputMedia>(_parent, input => input.Media!);
    }

    // ── WebApp ───────────────────────────────────────────

    public InputBuilder<string> UsingWebApp(string url)
    {
        _parent.SetInputType(InputType.WebApp);
        _parent.SetWebAppUrlProvider(_ => Task.FromResult(url));
        return new InputBuilder<string>(_parent, input => input.WebAppData!);
    }

    public InputBuilder<string> UsingWebApp(Func<FlowContext, string> urlProvider)
    {
        _parent.SetInputType(InputType.WebApp);
        _parent.SetWebAppUrlProvider(ctx => Task.FromResult(urlProvider(ctx)));
        return new InputBuilder<string>(_parent, input => input.WebAppData!);
    }

    public InputBuilder<string> UsingWebApp(Func<FlowContext, Task<string>> urlProvider)
    {
        _parent.SetInputType(InputType.WebApp);
        _parent.SetWebAppUrlProvider(urlProvider);
        return new InputBuilder<string>(_parent, input => input.WebAppData!);
    }

    // ── Reply Keyboard ───────────────────────────────────

    public InputBuilder<string> UsingKeyboard(params string[] buttons)
    {
        _parent.SetInputType(InputType.ReplyKeyboard);
        _parent.SetReplyKeyboardProvider(_ => Task.FromResult<IReadOnlyList<string>>(buttons));
        return new InputBuilder<string>(_parent, input => input.Text!);
    }

    public InputBuilder<string> UsingKeyboard(Func<FlowContext, IReadOnlyList<string>> provider)
    {
        _parent.SetInputType(InputType.ReplyKeyboard);
        _parent.SetReplyKeyboardProvider(ctx => Task.FromResult(provider(ctx)));
        return new InputBuilder<string>(_parent, input => input.Text!);
    }

    public InputBuilder<string> UsingKeyboard(Func<FlowContext, Task<IReadOnlyList<string>>> provider)
    {
        _parent.SetInputType(InputType.ReplyKeyboard);
        _parent.SetReplyKeyboardProvider(provider);
        return new InputBuilder<string>(_parent, input => input.Text!);
    }
}
