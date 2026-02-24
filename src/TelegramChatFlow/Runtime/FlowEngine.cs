using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Central orchestrator: Telegram update routing, per-user concurrency,
/// initialization, and inactivity management. Delegates logic to specialized services.
/// </summary>
public sealed class FlowEngine
{
    private readonly ITelegramBotClient _bot;
    private readonly ISessionStore _store;
    private readonly FlowBotOptions _options;
    private readonly MessageManager _messages;
    private readonly FlowNavigator _navigator;
    private readonly StepInputProcessor _inputProcessor;
    private readonly StepRenderer _renderer;
    private readonly ILogger<FlowEngine> _logger;

    private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

    public FlowEngine(
        ITelegramBotClient bot,
        ISessionStore store,
        IOptions<FlowBotOptions> options,
        MessageManager messages,
        FlowNavigator navigator,
        StepInputProcessor inputProcessor,
        StepRenderer renderer,
        ILogger<FlowEngine> logger)
    {
        _bot = bot;
        _store = store;
        _options = options.Value;
        _messages = messages;
        _navigator = navigator;
        _inputProcessor = inputProcessor;
        _renderer = renderer;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Startup initialization: cleans up orphan sessions and returns all users to the menu.
    /// </summary>
    public async Task InitializeAsync()
    {
        var sessions = await _store.GetAllAsync();

        foreach (var session in sessions)
        {
            try
            {
                await CleanupSessionAsync(session);
                await _store.SaveAsync(session);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error initializing session for chat {ChatId}", session.ChatId);
            }
        }
    }

    /// <summary>Handles a single Telegram update.</summary>
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
            _logger.LogError(ex, "Error handling update for chat {ChatId}", chatId.Value);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>Handles the inactivity timeout for a session.</summary>
    public async Task HandleInactivityAsync(FlowSession session)
    {
        var semaphore = _locks.GetOrAdd(session.ChatId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            var cutoff = DateTime.UtcNow - _options.InactivityTimeout;
            var fresh = await _store.GetAsync(session.ChatId);
            if (fresh == null || session.LastActivity >= cutoff) return;

            await _navigator.ResetToMenuAsync(fresh);
            await _store.SaveAsync(fresh);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling inactivity for chat {ChatId}", session.ChatId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Callback query handling
    // ═══════════════════════════════════════════════════════

    private async Task HandleCallbackAsync(FlowSession session, CallbackQuery callback)
    {
        try
        {
            await _bot.AnswerCallbackQuery(callback.Id);
        }
        catch
        {
            /* non-fatal: the callback might be expired or already answered */
        }

        if (callback.Message?.MessageId != session.BotMessageId)
        {
            await _navigator.ResetToMenuAsync(session);
        }

        var data = callback.Data ?? "";

        switch (data)
        {
            case "nav:back":
                await _navigator.GoBackAsync(session);
                return;
            case "nav:menu":
                await _navigator.ResetToMenuAsync(session);
                return;
            case "nav:skip":
                await _navigator.SkipStepAsync(session);
                return;
            case "nav:subdone":
                await _navigator.CompleteCurrentFlowAsync(session);
                return;
        }

        if (data.StartsWith("flow:"))
        {
            await _navigator.StartFlowAsync(session, data[5..]);
            return;
        }

        if (session.CurrentFlowId is not null)
            await _inputProcessor.ProcessStepInputAsync(session, new UserInput { CallbackData = data });
    }

    // ═══════════════════════════════════════════════════════
    //  User message handling
    // ═══════════════════════════════════════════════════════

    private async Task HandleMessageAsync(FlowSession session, Message message)
    {
        if (message.Text == "/start")
        {
            await _navigator.ResetToMenuAsync(session);
            await _messages.TryDeleteAsync(session.ChatId, message.MessageId);
            return;
        }

        if (session.CurrentFlowId is not null)
        {
            var input = StepInputProcessor.ExtractUserInput(message);
            await _inputProcessor.ProcessStepInputAsync(session, input);
        }

        await _messages.TryDeleteAsync(session.ChatId, message.MessageId);
    }

    // ═══════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════

    private bool IsAuthorized(long chatId) =>
        _options.AllowedUsers.Count == 0 || _options.AllowedUsers.Contains(chatId);

    private async Task CleanupSessionAsync(FlowSession session)
    {
        await _messages.CleanupAllFlowMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await _messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        session.Reset();
        session.TrackedMessageIds.Clear();
        session.PersistentMessageIds.Clear();
        session.HasReplyKeyboard = false;
    }
}
