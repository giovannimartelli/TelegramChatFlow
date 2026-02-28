using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramChatFlow.Runtime;

// OK VISTO
//TODO: rimuovere le string hardcoded dei callback e sostituirle con costanti o enum

/// <summary>
/// Central orchestrator: Telegram update routing, per-user concurrency,
/// initialization, and inactivity management. Delegates logic to specialized services.
/// </summary>
public sealed class FlowEngine(
    ITelegramBotClient bot,
    ISessionStore store,
    IOptions<FlowBotOptions> options,
    MessageManager messages,
    FlowNavigator navigator,
    StepInputProcessor inputProcessor,
    ILogger<FlowEngine> logger)
{
    private readonly FlowBotOptions _options = options.Value;

    private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

    // ═══════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Startup initialization: cleans up orphan sessions and returns all users to the menu.
    /// </summary>
    public async Task InitializeAsync()
    {
        var sessions = await store.GetAllAsync();

        foreach (var session in sessions)
        {
            try
            {
                await CleanupSessionAsync(session);
                await store.SaveAsync(session);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error initializing session for chat {ChatId}", session.ChatId);
            }
        }
    }

    /// <summary>Handles a single Telegram update.</summary>
    public async Task HandleUpdateAsync(Update update)
    {
        var chatId = update.CallbackQuery?.Message?.Chat.Id ?? update.Message?.Chat.Id;

        if (chatId is null) return;
        if (!IsAuthorized(chatId.Value)) return;

        var semaphore = _locks.GetOrAdd(chatId.Value, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            var session = await store.GetAsync(chatId.Value)
                          ?? new FlowSession { ChatId = chatId.Value };
            session.LastActivity = DateTime.UtcNow;

            if (update.CallbackQuery is { } callback)
                await HandleCallbackAsync(session, callback);
            else if (update.Message is { } message)
                await HandleMessageAsync(session, message);

            await store.SaveAsync(session);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling update for chat {ChatId}", chatId.Value);
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
            var fresh = await store.GetAsync(session.ChatId);
            if (fresh == null || session.LastActivity >= cutoff) return;

            await navigator.ResetToMenuAsync(fresh);
            await store.SaveAsync(fresh);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling inactivity for chat {ChatId}", session.ChatId);
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
            await bot.AnswerCallbackQuery(callback.Id);
        }
        catch
        {
            /* non-fatal: the callback might be expired or already answered */
        }

        if (callback.Message?.MessageId != session.BotMessageId)
        {
            if(callback.Message != null)
                await messages.TryDeleteAsync(session.ChatId, callback.Message!.MessageId);
            await navigator.ResetToMenuAsync(session);
        }

        var data = callback.Data ?? "";

        switch (data)
        {
            case "nav:back":
                await navigator.GoBackAsync(session);
                return;
            case "nav:menu":
                await navigator.ResetToMenuAsync(session);
                return;
            case "nav:skip":
                await navigator.SkipStepAsync(session);
                return;
            case "nav:subdone":
                await navigator.CompleteCurrentFlowAsync(session);
                return;
        }

        if (data.StartsWith("flow:"))
        {
            await navigator.StartFlowAsync(session, data[5..]);
            return;
        }

        if (session.CurrentFlowId is not null)
            await inputProcessor.ProcessStepInputAsync(session, new UserInput { CallbackData = data });
    }

    // ═══════════════════════════════════════════════════════
    //  User message handling
    // ═══════════════════════════════════════════════════════

    private async Task HandleMessageAsync(FlowSession session, Message message)
    {
        if (message.Text == "/start")
        {
            await navigator.ResetToMenuAsync(session);
        }
        else if (session.CurrentFlowId is not null)
        {
            var input = StepInputProcessor.ExtractUserInput(message);
            await inputProcessor.ProcessStepInputAsync(session, input);
        }

        await messages.TryDeleteAsync(session.ChatId, message.MessageId);
    }

    // ═══════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════

    private bool IsAuthorized(long chatId) => _options.AllowedUsers.Count == 0 || _options.AllowedUsers.Contains(chatId);

    private async Task CleanupSessionAsync(FlowSession session)
    {
        await messages.CleanupAllFlowMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        session.ResetAfterFlow();
        session.TrackedMessageIds.Clear();
        session.PersistentMessageIds.Clear();
        session.HasReplyKeyboard = false;
    }
}
