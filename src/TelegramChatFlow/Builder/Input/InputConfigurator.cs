using TelegramChatFlow.Builder.Step;

namespace TelegramChatFlow.Builder.Input;

/// <summary>Configuratore del tipo di input atteso da uno step.</summary>
public sealed class InputConfigurator
{
    private readonly StepBuilder _parent;

    internal InputConfigurator(StepBuilder parent) => _parent = parent;

    public InputTypeBuilder<string> UsingButtons(params InlineButton[] buttons)
    {
        _parent.SetInputType(InputType.InlineButtons);
        _parent.SetButtonsProvider(_ => buttons);
        return new InputTypeBuilder<string>(_parent, input => input.CallbackData!);
    }

    public InputTypeBuilder<string> UsingButtons(Func<FlowContext, IReadOnlyList<InlineButton>> provider)
    {
        _parent.SetInputType(InputType.InlineButtons);
        _parent.SetButtonsProvider(provider);
        return new InputTypeBuilder<string>(_parent, input => input.CallbackData!);
    }

    public InputTypeBuilder<string> UsingText()
    {
        _parent.SetInputType(InputType.Text);
        return new InputTypeBuilder<string>(_parent, input => input.Text!);
    }

    public InputTypeBuilder<DocumentInput> UsingDocument()
    {
        _parent.SetInputType(InputType.Document);
        return new InputTypeBuilder<DocumentInput>(_parent, input => new DocumentInput(input.Document!, input.FileId!));
    }

    public InputTypeBuilder<string> UsingWebApp(string url)
    {
        _parent.SetInputType(InputType.WebApp);
        _parent.SetWebAppUrlProvider(_ => url);
        return new InputTypeBuilder<string>(_parent, input => input.WebAppData!);
    }

    public InputTypeBuilder<string> UsingWebApp(Func<FlowContext, string> urlProvider)
    {
        _parent.SetInputType(InputType.WebApp);
        _parent.SetWebAppUrlProvider(urlProvider);
        return new InputTypeBuilder<string>(_parent, input => input.WebAppData!);
    }

    public InputTypeBuilder<string> UsingKeyboard(params string[] buttons)
    {
        _parent.SetInputType(InputType.ReplyKeyboard);
        _parent.SetReplyKeyboardProvider(_ => buttons);
        return new InputTypeBuilder<string>(_parent, input => input.Text!);
    }

    public InputTypeBuilder<string> UsingKeyboard(Func<FlowContext, IReadOnlyList<string>> provider)
    {
        _parent.SetInputType(InputType.ReplyKeyboard);
        _parent.SetReplyKeyboardProvider(provider);
        return new InputTypeBuilder<string>(_parent, input => input.Text!);
    }
}
