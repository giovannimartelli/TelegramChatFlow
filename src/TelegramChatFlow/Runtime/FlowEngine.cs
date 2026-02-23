using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Motore centrale del framework. Riceve gli update Telegram, gestisce lo stato delle
/// conversazioni, la navigazione tra step e sub-flow, e il rendering dei messaggi.
/// </summary>
public sealed class FlowEngine
{
    private readonly ITelegramBotClient _bot;
    private readonly ISessionStore _store;
    private readonly FlowBotOptions _options;
    private readonly MessageManager _messages;
    private readonly ILogger<FlowEngine> _logger;

    private readonly Dictionary<string, FlowDefinition> _rootFlows = new();
    private readonly Dictionary<string, FlowDefinition> _allFlows = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

    public FlowEngine(
        ITelegramBotClient bot,
        ISessionStore store,
        IOptions<FlowBotOptions> options,
        MessageManager messages,
        IReadOnlyList<FlowDefinition> flows,
        ILogger<FlowEngine> logger)
    {
        _bot = bot;
        _store = store;
        _options = options.Value;
        _messages = messages;
        _logger = logger;

        foreach (var def in flows)
        {
            _rootFlows[def.Id] = def;
            RegisterRecursive(def);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  API pubblica
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Inizializzazione all'avvio: pulisce le sessioni orfane e riporta tutti al menu.
    /// </summary>
    public async Task InitializeAsync()
    {
        var sessions = await _store.GetAllAsync();

        foreach (var session in sessions)
        {
            try
            {
                await CleanupSessionAsync(session);

                if (session.BotMessageId.HasValue)
                    await ShowMenuAsync(session);

                await _store.SaveAsync(session);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Errore inizializzazione sessione chat {ChatId}", session.ChatId);
            }
        }
    }

    /// <summary>Gestisce un singolo update Telegram.</summary>
    public async Task HandleUpdateAsync(Update update)
    {
        var chatId = update.CallbackQuery?.Message?.Chat.Id
                  ?? update.Message?.Chat.Id;

        if (chatId is null) return;
        if (!IsAuthorized(chatId.Value)) return;

        var semaphore = _locks.GetOrAdd(chatId.Value, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            var session = await _store.GetAsync(chatId.Value)
                       ?? new FlowSession { ChatId = chatId.Value };
            session.LastActivity = DateTime.UtcNow;

            if (update.CallbackQuery is { } callback)
                await HandleCallbackAsync(session, callback);
            else if (update.Message is { } message)
                await HandleMessageAsync(session, message);

            await _store.SaveAsync(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore gestione update per chat {ChatId}", chatId.Value);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>Gestisce il timeout per inattività di una sessione.</summary>
    public async Task HandleInactivityAsync(FlowSession session)
    {
        var semaphore = _locks.GetOrAdd(session.ChatId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            // Ri-carica la sessione per avere lo stato più recente
            var cutoff = DateTime.UtcNow - _options.InactivityTimeout;
            var fresh = await _store.GetAsync(session.ChatId);
            if (fresh?.CurrentFlowId is null || session.LastActivity >= cutoff) return;

            await ResetToMenuAsync(fresh);
            await _store.SaveAsync(fresh);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore cleanup inattività per chat {ChatId}", session.ChatId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Gestione callback query
    // ═══════════════════════════════════════════════════════

    private async Task HandleCallbackAsync(FlowSession session, CallbackQuery callback)
    {
        try { await _bot.AnswerCallbackQuery(callback.Id); }
        catch { /* non-fatale: il callback potrebbe essere scaduto o già risposto */ }

        // Callback da un messaggio che non corrisponde a quello attivo (es. dopo restart)
        if (callback.Message?.MessageId != session.BotMessageId)
        {
            // Se non abbiamo un messaggio attivo, mostra il menu (primo contatto o post-restart)
            if (session.BotMessageId is null)
                await ResetToMenuAsync(session);
            return;
        }

        var data = callback.Data ?? "";

        switch (data)
        {
            case "nav:back":  await GoBackAsync(session); return;
            case "nav:menu":  await ResetToMenuAsync(session); return;
            case "nav:skip":  await SkipStepAsync(session); return;
            case "nav:subdone": await CompleteCurrentFlowAsync(session); return;
        }

        if (data.StartsWith("flow:"))
        {
            await StartFlowAsync(session, data[5..]);
            return;
        }

        if (data.StartsWith("sub:"))
        {
            await StartSubFlowAsync(session, data[4..]);
            return;
        }

        // Callback specifico dello step corrente
        if (session.CurrentFlowId is not null)
            await ProcessStepInputAsync(session, new UserInput { CallbackData = data });
    }

    // ═══════════════════════════════════════════════════════
    //  Gestione messaggi utente
    // ═══════════════════════════════════════════════════════

    private async Task HandleMessageAsync(FlowSession session, Message message)
    {
        // /start → mostra menu (anche se in un flusso)
        if (message.Text == "/start")
        {
            await ResetToMenuAsync(session);
            await _messages.TryDeleteAsync(session.ChatId, message.MessageId);
            return;
        }

        // Input dell'utente all'interno di un flusso attivo
        if (session.CurrentFlowId is not null)
        {
            UserInput input;

            if (message.Document is { } doc)
                input = new UserInput { Document = doc, FileId = doc.FileId };
            else if (message.WebAppData is { } webApp)
                input = new UserInput { WebAppData = webApp.Data };
            else
                input = new UserInput { Text = message.Text };

            await ProcessStepInputAsync(session, input);
        }

        // Cancella sempre il messaggio dell'utente per mantenere la chat pulita
        await _messages.TryDeleteAsync(session.ChatId, message.MessageId);
    }

    // ═══════════════════════════════════════════════════════
    //  Elaborazione input di uno step
    // ═══════════════════════════════════════════════════════

    private async Task ProcessStepInputAsync(FlowSession session, UserInput input)
    {
        var flow = _allFlows.GetValueOrDefault(session.CurrentFlowId!);
        if (flow is null) { await ResetToMenuAsync(session); return; }

        if (session.CurrentStepIndex >= flow.Steps.Count) return;

        var step = flow.Steps[session.CurrentStepIndex];

        // Step display-only: nessun input da elaborare
        if (step.InputType == InputType.None) return;

        // Valida che il tipo di input corrisponda a quello atteso dallo step
        var mismatch = step.InputType switch
        {
            InputType.Text when input.Text is null => "Invia un messaggio di testo.",
            InputType.Document when input.Document is null => "Invia un documento.",
            InputType.InlineButtons when input.CallbackData is null => null, // ignora silenziosamente (es. testo su step a bottoni)
            _ => null
        };

        if (mismatch is not null)
        {
            await RenderStepAsync(session, step, mismatch);
            return;
        }

        // Ignora messaggi di testo su step che aspettano bottoni inline (nessun feedback)
        if (step.InputType == InputType.InlineButtons && input.CallbackData is null)
            return;

        // Snapshot dei dati prima che l'handler li modifichi (per il back)
        var dataSnapshot = new Dictionary<string, object?>(session.Data);

        var context = new FlowContext { Data = session.Data };

        StepResult result;
        try
        {
            result = await step.HandleInput(context, input);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nell'handler dello step {StepId} del flusso {FlowId}",
                step.Id, flow.Id);
            context.ValidationError = "Si è verificato un errore. Riprova.";
            result = StepResult.Retry;
        }

        session.Data = context.Data;

        switch (result)
        {
            case StepResult.NextResult:
                await AdvanceAsync(session, flow, dataSnapshot);
                break;
            case StepResult.RetryResult { Show: { } retryShow }:
                await RenderStepAsync(session, step, context.ValidationError, retryShow);
                break;
            case StepResult.RetryResult:
                await RenderStepAsync(session, step, context.ValidationError);
                break;
            case StepResult.GoToResult { StepId: var targetId }:
                await GoToStepAsync(session, flow, targetId, dataSnapshot);
                break;
            case StepResult.ExitResult:
                await ResetToMenuAsync(session);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Navigazione
    // ═══════════════════════════════════════════════════════

    private async Task AdvanceAsync(FlowSession session, FlowDefinition flow,
        Dictionary<string, object?>? dataSnapshot = null)
    {
        // Se lo step corrente è persistente, stacca il messaggio (resta visibile senza keyboard)
        var currentStep = flow.Steps.ElementAtOrDefault(session.CurrentStepIndex);
        if (currentStep?.Persistent == true)
            await _messages.DetachBotMessageAsync(session);

        session.StepHistory.Push(new StepHistoryEntry
        {
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot ?? new Dictionary<string, object?>(session.Data)
        });
        var next = session.CurrentStepIndex + 1;

        if (next < flow.Steps.Count)
        {
            session.CurrentStepIndex = next;
            await RenderStepAsync(session, flow.Steps[next]);
        }
        else if (flow.SubFlows is { Count: > 0 })
        {
            // Step completati, mostra menu dei sub-flow
            session.CurrentStepIndex = flow.Steps.Count;
            await ShowSubFlowMenuAsync(session, flow);
        }
        else
        {
            await CompleteCurrentFlowAsync(session);
        }
    }

    private async Task GoBackAsync(FlowSession session)
    {
        if (session.CurrentFlowId is null) return;

        // Tornando indietro, cancella eventuali messaggi persistenti accumulati
        await _messages.CleanupPersistentMessagesAsync(session);

        if (session.StepHistory.TryPop(out var entry))
        {
            var flow = _allFlows.GetValueOrDefault(session.CurrentFlowId);
            if (flow is null) { await ResetToMenuAsync(session); return; }

            session.CurrentStepIndex = entry.StepIndex;
            session.Data = entry.Data;
            await RenderStepAsync(session, flow.Steps[entry.StepIndex]);
        }
        else if (session.FlowStack.Count > 0)
        {
            // Nessuna history nel sub-flow corrente → torna al flusso genitore
            var frame = session.FlowStack.Pop();
            session.CurrentFlowId    = frame.FlowId;
            session.CurrentStepIndex = frame.StepIndex;
            session.Data             = frame.Data;
            session.StepHistory      = frame.StepHistory;

            var parent = _allFlows.GetValueOrDefault(frame.FlowId);
            if (parent?.SubFlows is { Count: > 0 })
                await ShowSubFlowMenuAsync(session, parent);
            else
                await ResetToMenuAsync(session);
        }
        else
        {
            // Nessuna history e nessuno stack → torna al menu
            await ResetToMenuAsync(session);
        }
    }

    private async Task GoToStepAsync(FlowSession session, FlowDefinition flow, string stepId,
        Dictionary<string, object?> dataSnapshot)
    {
        var targetIdx = -1;
        for (var i = 0; i < flow.Steps.Count; i++)
            if (flow.Steps[i].Id == stepId) { targetIdx = i; break; }

        if (targetIdx < 0) { await ResetToMenuAsync(session); return; }

        session.StepHistory.Push(new StepHistoryEntry
        {
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot
        });
        session.CurrentStepIndex = targetIdx;
        await RenderStepAsync(session, flow.Steps[targetIdx]);
    }

    private async Task SkipStepAsync(FlowSession session)
    {
        if (session.CurrentFlowId is null) return;

        var flow = _allFlows.GetValueOrDefault(session.CurrentFlowId);
        if (flow is null) return;

        var step = flow.Steps.ElementAtOrDefault(session.CurrentStepIndex);
        if (step?.Skippable == true)
            await AdvanceAsync(session, flow, new Dictionary<string, object?>(session.Data));
    }

    private async Task StartFlowAsync(FlowSession session, string flowId)
    {
        if (!_rootFlows.TryGetValue(flowId, out var flow)) return;

        session.CurrentFlowId = flowId;
        session.CurrentStepIndex = 0;
        session.Data = new();
        session.FlowStack = new();
        session.StepHistory = new();

        if (flow.Steps.Count > 0)
            await RenderStepAsync(session, flow.Steps[0]);
        else if (flow.SubFlows is { Count: > 0 })
            await ShowSubFlowMenuAsync(session, flow);
    }

    private async Task StartSubFlowAsync(FlowSession session, string subFlowId)
    {
        var current = _allFlows.GetValueOrDefault(session.CurrentFlowId!);
        var sub = current?.SubFlows?.FirstOrDefault(s => s.Id == subFlowId);
        if (sub is null) return;

        // Salva il frame corrente nello stack
        session.FlowStack.Push(new SubFlowFrame
        {
            FlowId      = session.CurrentFlowId!,
            StepIndex   = session.CurrentStepIndex,
            Data        = new Dictionary<string, object?>(session.Data),
            StepHistory = session.StepHistory
        });

        session.CurrentFlowId = subFlowId;
        session.CurrentStepIndex = 0;
        session.Data = new();
        session.StepHistory = new();

        if (sub.Steps.Count > 0)
            await RenderStepAsync(session, sub.Steps[0]);
        else if (sub.SubFlows is { Count: > 0 })
            await ShowSubFlowMenuAsync(session, sub);
    }

    private async Task CompleteCurrentFlowAsync(FlowSession session)
    {
        if (session.FlowStack.Count > 0)
        {
            // Torna al flusso genitore
            var frame = session.FlowStack.Pop();
            session.CurrentFlowId = frame.FlowId;
            session.CurrentStepIndex = frame.StepIndex;
            session.Data = frame.Data;
            session.StepHistory = frame.StepHistory;

            var parent = _allFlows.GetValueOrDefault(frame.FlowId);
            if (parent?.SubFlows is { Count: > 0 })
                await ShowSubFlowMenuAsync(session, parent);
            else if (parent is not null)
                await AdvanceAsync(session, parent);
            else
                await ResetToMenuAsync(session);
        }
        else
        {
            await ResetToMenuAsync(session);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Rendering
    // ═══════════════════════════════════════════════════════

    private async Task RenderStepAsync(
        FlowSession session, StepDefinition step, string? error = null,
        ShowDefinition? showOverride = null)
    {
        // Pulizia messaggi extra (es. reply keyboard) dello step precedente
        await _messages.CleanupTransientMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await _messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        var ctx = new FlowContext { Data = session.Data };
        var show = showOverride ?? step.Show;

        switch (show.ContentType)
        {
            case ShowContentType.Text:
            {
                var text = await show.Text!(ctx);
                if (error is not null) text += $"\n\n⚠️ {error}";

                if (step.InputType == InputType.ReplyKeyboard && step.ReplyKeyboardProvider is not null)
                {
                    // Reply keyboard: messaggio editato con bottoni di navigazione inline
                    // + messaggio aggiuntivo con la reply keyboard
                    var nav = BuildNavigationKeyboard(session, step);
                    await _messages.SendOrEditAsync(session, text, nav);

                    var buttons = await step.ReplyKeyboardProvider(ctx);
                    var rows = buttons.Select(b => new[] { new KeyboardButton(b) });
                    var replyMarkup = new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true, OneTimeKeyboard = true };
                    await _messages.SendReplyKeyboardAsync(session, "👇", replyMarkup);
                    session.HasReplyKeyboard = true;
                }
                else
                {
                    var markup = await BuildStepKeyboardAsync(session, step, ctx);
                    await _messages.SendOrEditAsync(session, text, markup);
                }
                break;
            }

            case ShowContentType.Media:
            {
                var fileId = show.MediaFileId!(ctx);
                var caption = show.Caption?.Invoke(ctx);
                if (error is not null) caption = (caption is null ? "" : caption + "\n\n") + $"⚠️ {error}";
                var markup = await BuildStepKeyboardAsync(session, step, ctx);
                await _messages.SendOrEditMediaAsync(session, show.Media!.Value, fileId, caption, markup);
                break;
            }

            case ShowContentType.TextWithMedia:
            {
                var text = await show.Text!(ctx);
                if (error is not null) text += $"\n\n⚠️ {error}";
                var markup = await BuildStepKeyboardAsync(session, step, ctx);
                await _messages.SendOrEditAsync(session, text, markup);

                var fileId = show.MediaFileId!(ctx);
                await _messages.SendTrackedMediaAsync(session, show.Media!.Value, fileId);
                break;
            }
        }
    }

    private async Task ShowSubFlowMenuAsync(FlowSession session, FlowDefinition flow)
    {
        await _messages.CleanupTransientMessagesAsync(session);

        var text = $"📂 {flow.Label}\n\nSeleziona un'opzione:";
        var rows = new List<List<InlineKeyboardButton>>();

        foreach (var sub in flow.SubFlows ?? [])
            rows.Add([InlineKeyboardButton.WithCallbackData(sub.Label, $"sub:{sub.Id}")]);

        var nav = new List<InlineKeyboardButton>();

        // "Indietro" per tornare all'ultimo step del flusso corrente (se ha step)
        if (flow.Steps.Count > 0)
            nav.Add(InlineKeyboardButton.WithCallbackData("◀️ Indietro", "nav:back"));
        // Oppure per tornare al flusso genitore (se siamo in un sub-flow senza step propri)
        else if (session.FlowStack.Count > 0)
            nav.Add(InlineKeyboardButton.WithCallbackData("◀️ Indietro", "nav:subdone"));

        nav.Add(InlineKeyboardButton.WithCallbackData("🏠 Menu", "nav:menu"));
        rows.Add(nav);

        await _messages.SendOrEditAsync(session, text, new InlineKeyboardMarkup(rows));
    }

    private async Task ShowMenuAsync(FlowSession session)
    {
        var rows = _rootFlows.Values
            .Select(f => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(f.Label, $"flow:{f.Id}")
            })
            .ToList();

        await _messages.SendOrEditAsync(session, _options.MainMenuText, new InlineKeyboardMarkup(rows));
    }

    private async Task ResetToMenuAsync(FlowSession session)
    {
        await _messages.CleanupAllFlowMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await _messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        session.CurrentFlowId = null;
        session.CurrentStepIndex = 0;
        session.Data = new();
        session.FlowStack = new();
        session.StepHistory = new();

        await ShowMenuAsync(session);
    }

    // ═══════════════════════════════════════════════════════
    //  Costruzione tastiere
    // ═══════════════════════════════════════════════════════

    private async Task<InlineKeyboardMarkup> BuildStepKeyboardAsync(
        FlowSession session, StepDefinition step, FlowContext ctx)
    {
        var rows = new List<List<InlineKeyboardButton>>();

        if (step.InputType == InputType.InlineButtons && step.ButtonsProvider is not null)
        {
            foreach (var btn in await step.ButtonsProvider(ctx))
                rows.Add([InlineKeyboardButton.WithCallbackData(btn.Text, btn.CallbackData)]);
        }
        else if (step.InputType == InputType.WebApp && step.WebAppUrlProvider is not null)
        {
            var url = await step.WebAppUrlProvider(ctx);
            rows.Add([InlineKeyboardButton.WithWebApp("🌐 Apri", new WebAppInfo { Url = url })]);
        }

        rows.Add(BuildNavigationRow(session, step));
        return new InlineKeyboardMarkup(rows);
    }

    private InlineKeyboardMarkup BuildNavigationKeyboard(FlowSession session, StepDefinition? step)
    {
        return new InlineKeyboardMarkup([BuildNavigationRow(session, step)]);
    }

    private List<InlineKeyboardButton> BuildNavigationRow(FlowSession session, StepDefinition? step)
    {
        var nav = new List<InlineKeyboardButton>();

        // "Indietro" appare solo se c'è qualcosa a cui tornare (history o stack)
        bool canGoBack = session.StepHistory.Count > 0 || session.FlowStack.Count > 0;
        if (canGoBack)
            nav.Add(InlineKeyboardButton.WithCallbackData("◀️ Indietro", "nav:back"));

        nav.Add(InlineKeyboardButton.WithCallbackData("🏠 Menu", "nav:menu"));

        if (step?.Skippable == true)
            nav.Add(InlineKeyboardButton.WithCallbackData("⏭ Salta", "nav:skip"));

        return nav;
    }

    // ═══════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════

    private bool IsAuthorized(long chatId) =>
        _options.AllowedUsers.Count == 0 || _options.AllowedUsers.Contains(chatId);

    private void CleanupSessionState(FlowSession session)
    {
        session.CurrentFlowId = null;
        session.CurrentStepIndex = 0;
        session.Data = new();
        session.FlowStack = new();
        session.StepHistory = new();
        session.TrackedMessageIds.Clear();
        session.PersistentMessageIds.Clear();
        session.HasReplyKeyboard = false;
    }

    private async Task CleanupSessionAsync(FlowSession session)
    {
        await _messages.CleanupAllFlowMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await _messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        CleanupSessionState(session);
    }

    private void RegisterRecursive(FlowDefinition flow)
    {
        _allFlows[flow.Id] = flow;
        if (flow.SubFlows is not null)
            foreach (var sub in flow.SubFlows)
                RegisterRecursive(sub);
    }
}
