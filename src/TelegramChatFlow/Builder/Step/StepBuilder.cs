using TelegramChatFlow.Builder.Input;
using TelegramChatFlow.Builder.Show;

namespace TelegramChatFlow.Builder.Step;

/// <summary>Builder fluent per la definizione di uno step tipizzato.</summary>
public sealed class StepBuilder<TData> where TData : class, new()
{
    private readonly string _id;
    private ShowDefinition _show = new() { ContentType = ShowContentType.Text, Text = _ => Task.FromResult("") };
    private InputType _inputType = InputType.None;
    private Func<FlowContext, Task<IReadOnlyList<InlineButton>>>? _buttonsProvider;
    private Func<FlowContext, Task<IReadOnlyList<string>>>? _replyKeyboardProvider;
    private Func<FlowContext, Task<string>>? _webAppUrlProvider;
    private Func<FlowContext, UserInput, Task<StepResult>> _handler = (_, _) => Task.FromResult(StepResult.Exit);
    private bool _skippable;
    private bool _persistent;
    private bool _showBack = true;
    private bool _showMenu = true;
    private int _ordinal;

    internal StepBuilder(string id, int defaultOrdinal)
    {
        _id = id;
        _ordinal = defaultOrdinal;
    }

    // ── Display ────────────────────────────────────────

    /// <summary>Configura il contenuto visivo dello step.</summary>
    public StepBuilder<TData> Show(Action<ShowBuilder<TData>> configure)
    {
        var builder = new ShowBuilder<TData>();
        configure(builder);
        _show = builder.Build();
        return this;
    }

    // ── Input ──────────────────────────────────────────

    /// <summary>
    /// Configura il tipo di input atteso dallo step.
    /// Se omesso, lo step è display-only.
    /// </summary>
    public StepBuilder<TData> Input(Action<InputTypeConfigurator<TData>> configure)
    {
        configure(new InputTypeConfigurator<TData>(this));
        return this;
    }

    // ── Flag ────────────────────────────────────────────

    public StepBuilder<TData> Skippable()
    {
        _skippable = true;
        return this;
    }

    /// <summary>
    /// Il messaggio dello step resta visibile nella chat dopo l'avanzamento.
    /// Viene rimosso solo al ritorno al menu principale o per timeout.
    /// </summary>
    public StepBuilder<TData> Persistent()
    {
        _persistent = true;
        return this;
    }

    /// <summary>Nasconde il bottone "Indietro" per questo step.</summary>
    public StepBuilder<TData> HideBack()
    {
        _showBack = false;
        return this;
    }

    /// <summary>Nasconde il bottone "Menu" per questo step.</summary>
    public StepBuilder<TData> HideMenu()
    {
        _showMenu = false;
        return this;
    }

    /// <summary>Imposta l'ordinale di esecuzione dello step (default: indice × 10).</summary>
    public StepBuilder<TData> WithOrdinal(int ordinal)
    {
        _ordinal = ordinal;
        return this;
    }

    // ── Setter interni (usati da InputConfigurator) ──

    internal void SetInputType(InputType type) => _inputType = type;
    internal void SetButtonsProvider(Func<FlowContext, Task<IReadOnlyList<InlineButton>>> provider) => _buttonsProvider = provider;
    internal void SetReplyKeyboardProvider(Func<FlowContext, Task<IReadOnlyList<string>>> provider) => _replyKeyboardProvider = provider;
    internal void SetWebAppUrlProvider(Func<FlowContext, Task<string>> provider) => _webAppUrlProvider = provider;
    internal void SetHandler(Func<FlowContext, UserInput, Task<StepResult>> handler) => _handler = handler;

    // ── Build ──────────────────────────────────────────

    internal StepDefinition Build() => new()
    {
        Id = _id,
        Show = _show,
        InputType = _inputType,
        ButtonsProvider = _buttonsProvider,
        ReplyKeyboardProvider = _replyKeyboardProvider,
        WebAppUrlProvider = _webAppUrlProvider,
        HandleInput = _handler,
        Skippable = _skippable,
        Persistent = _persistent,
        ShowBack = _showBack,
        ShowMenu = _showMenu,
        Ordinal = _ordinal
    };
}
