namespace TelegramChatFlow.Builder;

/// <summary>Builder fluent per la definizione di uno step.</summary>
public sealed class StepBuilder
{
    private readonly string _id;
    private Func<FlowContext, Task<string>> _renderText = _ => Task.FromResult("");
    private InputType _inputType = InputType.InlineButtons;
    private Func<FlowContext, IReadOnlyList<InlineButton>>? _buttonsProvider;
    private Func<FlowContext, IReadOnlyList<string>>? _replyKeyboardProvider;
    private Func<FlowContext, string>? _webAppUrlProvider;
    private Func<FlowContext, UserInput, Task<StepResult>> _handler = (_, _) => Task.FromResult(StepResult.Exit);
    private bool _skippable;
    private bool _persistent;

    internal StepBuilder(string id) => _id = id;

    // ── Testo ──────────────────────────────────────────

    public StepBuilder Text(string text)
    {
        _renderText = _ => Task.FromResult(text);
        return this;
    }

    public StepBuilder Text(Func<FlowContext, string> provider)
    {
        _renderText = ctx => Task.FromResult(provider(ctx));
        return this;
    }

    public StepBuilder Text(Func<FlowContext, Task<string>> provider)
    {
        _renderText = provider;
        return this;
    }

    // ── Bottoni inline ─────────────────────────────────

    public StepBuilder Buttons(params InlineButton[] buttons)
    {
        _inputType = InputType.InlineButtons;
        _buttonsProvider = _ => buttons;
        return this;
    }

    public StepBuilder Buttons(Func<FlowContext, IReadOnlyList<InlineButton>> provider)
    {
        _inputType = InputType.InlineButtons;
        _buttonsProvider = provider;
        return this;
    }

    // ── Testo libero ───────────────────────────────────

    public StepBuilder ExpectText()
    {
        _inputType = InputType.Text;
        return this;
    }

    // ── Upload documento ───────────────────────────────

    public StepBuilder ExpectDocument()
    {
        _inputType = InputType.Document;
        return this;
    }

    // ── WebApp ─────────────────────────────────────────

    public StepBuilder WebApp(string url)
    {
        _inputType = InputType.WebApp;
        _webAppUrlProvider = _ => url;
        return this;
    }

    public StepBuilder WebApp(Func<FlowContext, string> urlProvider)
    {
        _inputType = InputType.WebApp;
        _webAppUrlProvider = urlProvider;
        return this;
    }

    // ── Reply keyboard ─────────────────────────────────

    public StepBuilder ReplyKeyboard(params string[] buttons)
    {
        _inputType = InputType.ReplyKeyboard;
        _replyKeyboardProvider = _ => buttons;
        return this;
    }

    public StepBuilder ReplyKeyboard(Func<FlowContext, IReadOnlyList<string>> provider)
    {
        _inputType = InputType.ReplyKeyboard;
        _replyKeyboardProvider = provider;
        return this;
    }

    // ── Handler ────────────────────────────────────────

    /// <summary>Handler con risultato asincrono completo.</summary>
    public StepBuilder OnInput(Func<FlowContext, UserInput, Task<StepResult>> handler)
    {
        _handler = handler;
        return this;
    }

    /// <summary>Handler sincrono con <see cref="StepResult"/>.</summary>
    public StepBuilder OnInput(Func<FlowContext, UserInput, StepResult> handler)
    {
        _handler = (ctx, input) => Task.FromResult(handler(ctx, input));
        return this;
    }

    /// <summary>Handler semplificato: true = Next, false = Retry.</summary>
    public StepBuilder OnInput(Func<FlowContext, UserInput, bool> handler)
    {
        _handler = (ctx, input) =>
            Task.FromResult(handler(ctx, input) ? StepResult.Next : StepResult.Retry);
        return this;
    }

    // ── Opzionalità ────────────────────────────────────

    public StepBuilder Skippable()
    {
        _skippable = true;
        return this;
    }

    // ── Persistenza ────────────────────────────────────

    /// <summary>
    /// Il messaggio dello step resta visibile nella chat dopo l'avanzamento.
    /// Viene rimosso solo al ritorno al menu principale o per timeout.
    /// </summary>
    public StepBuilder Persistent()
    {
        _persistent = true;
        return this;
    }

    // ── Build ──────────────────────────────────────────

    internal StepDefinition Build() => new()
    {
        Id = _id,
        RenderText = _renderText,
        InputType = _inputType,
        ButtonsProvider = _buttonsProvider,
        ReplyKeyboardProvider = _replyKeyboardProvider,
        WebAppUrlProvider = _webAppUrlProvider,
        HandleInput = _handler,
        Skippable = _skippable,
        Persistent = _persistent
    };
}
