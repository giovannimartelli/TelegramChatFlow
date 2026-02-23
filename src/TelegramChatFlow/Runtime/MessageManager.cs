using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Gestisce l'invio, la modifica e la cancellazione dei messaggi Telegram.
/// Mantiene la chat pulita editando il messaggio esistente dove possibile.
/// </summary>
public sealed class MessageManager
{
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<MessageManager> _logger;

    public MessageManager(ITelegramBotClient bot, ILogger<MessageManager> logger)
    {
        _bot = bot;
        _logger = logger;
    }

    /// <summary>
    /// Invia un nuovo messaggio o modifica quello esistente (il messaggio "persistente" del bot).
    /// </summary>
    public async Task SendOrEditAsync(FlowSession session, string text, InlineKeyboardMarkup? markup)
    {
        if (session.BotMessageId.HasValue)
        {
            try
            {
                await _bot.EditMessageText(
                    session.ChatId,
                    session.BotMessageId.Value,
                    text,
                    replyMarkup: markup);
                return;
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified"))
            {
                return; // contenuto identico, nessuna azione
            }
            catch (ApiRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Impossibile editare messaggio {MsgId} per chat {ChatId}, ne invio uno nuovo",
                    session.BotMessageId, session.ChatId);
            }
        }

        await SendNewBotMessageAsync(session, text, markup);
    }

    /// <summary>Invia un messaggio aggiuntivo con reply keyboard (tracciato per la pulizia).</summary>
    public async Task SendReplyKeyboardAsync(FlowSession session, string text, ReplyKeyboardMarkup markup)
    {
        var msg = await _bot.SendMessage(session.ChatId, text, replyMarkup: markup);
        session.TrackedMessageIds.Add(msg.MessageId);
    }

    /// <summary>Rimuove la reply keyboard inviando e cancellando un messaggio di servizio.</summary>
    public async Task RemoveReplyKeyboardAsync(long chatId)
    {
        try
        {
            var msg = await _bot.SendMessage(chatId, "\u200B", replyMarkup: new ReplyKeyboardRemove());
            await TryDeleteAsync(chatId, msg.MessageId);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Impossibile rimuovere reply keyboard per chat {ChatId}", chatId);
        }
    }

    /// <summary>Cancella un messaggio ignorando eventuali errori.</summary>
    public async Task TryDeleteAsync(long chatId, int messageId)
    {
        try
        {
            await _bot.DeleteMessage(chatId, messageId);
        }
        catch (ApiRequestException)
        {
            // Il messaggio potrebbe essere già cancellato o troppo vecchio
        }
    }

    /// <summary>Cancella i messaggi transitori (reply keyboard, ecc.) ma NON quelli persistenti.</summary>
    public async Task CleanupTransientMessagesAsync(FlowSession session)
    {
        foreach (var msgId in session.TrackedMessageIds)
            await TryDeleteAsync(session.ChatId, msgId);

        session.TrackedMessageIds.Clear();
    }

    /// <summary>Cancella i messaggi persistenti degli step marcati Persistent.</summary>
    public async Task CleanupPersistentMessagesAsync(FlowSession session)
    {
        foreach (var msgId in session.PersistentMessageIds)
            await TryDeleteAsync(session.ChatId, msgId);

        session.PersistentMessageIds.Clear();
    }

    /// <summary>Cancella tutti i messaggi del flusso (transitori + persistenti).</summary>
    public async Task CleanupAllFlowMessagesAsync(FlowSession session)
    {
        await CleanupTransientMessagesAsync(session);
        await CleanupPersistentMessagesAsync(session);
    }

    /// <summary>
    /// "Stacca" il messaggio corrente del bot: rimuove la tastiera inline,
    /// lo sposta tra i messaggi persistenti e annulla BotMessageId.
    /// Il prossimo SendOrEditAsync invierà un messaggio nuovo.
    /// </summary>
    public async Task DetachBotMessageAsync(FlowSession session)
    {
        if (!session.BotMessageId.HasValue) return;

        // Rimuovi la inline keyboard dal messaggio staccato
        try
        {
            await _bot.EditMessageReplyMarkup(
                session.ChatId,
                session.BotMessageId.Value,
                replyMarkup: new InlineKeyboardMarkup(Array.Empty<InlineKeyboardButton[]>()));
        }
        catch (ApiRequestException) { /* messaggio potrebbe non essere più editabile */ }

        session.PersistentMessageIds.Add(session.BotMessageId.Value);
        session.BotMessageId = null;
    }

    private async Task SendNewBotMessageAsync(FlowSession session, string text, InlineKeyboardMarkup? markup)
    {
        var msg = await _bot.SendMessage(session.ChatId, text, replyMarkup: markup);

        if (session.BotMessageId.HasValue)
            await TryDeleteAsync(session.ChatId, session.BotMessageId.Value);

        session.BotMessageId = msg.MessageId;
    }
}
