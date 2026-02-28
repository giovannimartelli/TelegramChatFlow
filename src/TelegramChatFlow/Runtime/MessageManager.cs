using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using InputMedia = Telegram.Bot.Types.InputMedia;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Manages sending, editing, and deleting Telegram messages.
/// Keeps the chat clean by editing the existing message where possible.
/// </summary>
public sealed class MessageManager(ITelegramBotClient bot, ILogger<MessageManager> logger)
{
    /// <summary>
    /// Sends a new message or edits the existing one (the bot's active message).
    /// </summary>
    public async Task SendOrEditAsync(FlowSession session, string text, InlineKeyboardMarkup? markup)
    {
        if (session.BotMessageId.HasValue)
        {
            var edited = await TryEditAsync(() => bot.EditMessageText(
                session.ChatId,
                session.BotMessageId.Value,
                text,
                replyMarkup: markup));
            if (edited) return;
        }

        await ReplaceBotMessageAsync(session,
            bot.SendMessage(session.ChatId, text, replyMarkup: markup));
    }

    /// <summary>Sends an additional message with reply keyboard (tracked for cleanup).</summary>
    public async Task SendReplyKeyboardAsync(FlowSession session, string text, ReplyKeyboardMarkup markup)
    {
        var msg = await bot.SendMessage(session.ChatId, text, replyMarkup: markup);
        session.TrackedMessageIds.Add(msg.MessageId);
    }

    /// <summary>Removes the reply keyboard by sending and deleting a service message.</summary>
    public async Task RemoveReplyKeyboardAsync(long chatId)
    {
        try
        {
            var msg = await bot.SendMessage(chatId, "\u200B", replyMarkup: new ReplyKeyboardRemove());
            await TryDeleteAsync(chatId, msg.MessageId);
        }
        catch (ApiRequestException ex)
        {
            logger.LogWarning(ex, "Failed to remove reply keyboard for chat {ChatId}", chatId);
        }
    }

    /// <summary>Deletes a message ignoring any errors.</summary>
    public async Task TryDeleteAsync(long chatId, int messageId)
    {
        try
        {
            await bot.DeleteMessage(chatId, messageId);
        }
        catch (ApiRequestException)
        {
            // The message might already be deleted or too old
        }
    }

    /// <summary>Deletes transient messages (reply keyboard, etc.) but NOT persistent ones.</summary>
    public async Task CleanupTransientMessagesAsync(FlowSession session)
    {
        foreach (var msgId in session.TrackedMessageIds)
            await TryDeleteAsync(session.ChatId, msgId);

        session.TrackedMessageIds.Clear();
    }

    /// <summary>Deletes persistent messages from steps marked as Persistent.</summary>
    public async Task CleanupPersistentMessagesAsync(FlowSession session)
    {
        foreach (var msgId in session.PersistentMessageIds)
            await TryDeleteAsync(session.ChatId, msgId);

        session.PersistentMessageIds.Clear();
    }

    /// <summary>Deletes all flow messages (transient + persistent).</summary>
    public async Task CleanupAllFlowMessagesAsync(FlowSession session)
    {
        await CleanupTransientMessagesAsync(session);
        await CleanupPersistentMessagesAsync(session);
    }

    /// <summary>
    /// Detaches the current bot message: removes the inline keyboard,
    /// moves it to persistent messages, and clears BotMessageId.
    /// The next SendOrEditAsync call will send a new message.
    /// </summary>
    public async Task DetachPersistentMessageAsync(FlowSession session)
    {
        if (session.BotMessageId == null) return;

        // Remove the inline keyboard from the detached message
        try
        {
            await bot.EditMessageReplyMarkup(
                session.ChatId,
                session.BotMessageId.Value,
                replyMarkup: null);
        }
        catch (ApiRequestException) { /* message might no longer be editable */ }

        session.PersistentMessageIds.Add(session.BotMessageId.Value);
        session.BotMessageId = null;
    }

    /// <summary>
    /// Sends or edits the active bot message with media.
    /// </summary>
    public async Task SendOrEditMediaAsync(FlowSession session, ShowMediaType showMediaType, string fileId, string? caption, InlineKeyboardMarkup? markup)
    {
        if (session.BotMessageId.HasValue)
        {
            var edited = await TryEditAsync(() => bot.EditMessageMedia(
                session.ChatId,
                session.BotMessageId.Value,
                ToInputMedia(showMediaType, fileId, caption),
                replyMarkup: markup));
            if (edited) return;
        }

        await ReplaceBotMessageAsync(session,
            SendMediaMessageAsync(session.ChatId, showMediaType, fileId, caption, markup));
    }

    /// <summary>Sends an additional media message (without markup), tracked for cleanup.</summary>
    public async Task SendTrackedMediaAsync(FlowSession session, ShowMediaType showMediaType, string fileId)
    {
        var msg = await SendMediaMessageAsync(session.ChatId, showMediaType, fileId, caption: null, markup: null);
        session.TrackedMessageIds.Add(msg.MessageId);
    }

    private async Task<bool> TryEditAsync(Func<Task> editAction)
    {
        try
        {
            await editAction();
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified"))
        {
            return true; // identical content, no action needed
        }
        catch (ApiRequestException ex)
        {
            logger.LogWarning(ex, "Failed to edit message, sending a new one");
            return false;
        }
    }

    private async Task ReplaceBotMessageAsync(FlowSession session, Task<Message> sendTask)
    {
        var msg = await sendTask;

        if (session.BotMessageId.HasValue)
            await TryDeleteAsync(session.ChatId, session.BotMessageId.Value);

        session.BotMessageId = msg.MessageId;
    }

    private Task<Message> SendMediaMessageAsync(
        long chatId, ShowMediaType showMediaType, string fileId, string? caption, InlineKeyboardMarkup? markup) =>
        showMediaType switch
        {
            ShowMediaType.Photo     => bot.SendPhoto(chatId, InputFile.FromFileId(fileId), caption: caption, replyMarkup: markup),
            ShowMediaType.Video     => bot.SendVideo(chatId, InputFile.FromFileId(fileId), caption: caption, replyMarkup: markup),
            ShowMediaType.Document  => bot.SendDocument(chatId, InputFile.FromFileId(fileId), caption: caption, replyMarkup: markup),
            ShowMediaType.Animation => bot.SendAnimation(chatId, InputFile.FromFileId(fileId), caption: caption, replyMarkup: markup),
            _ => throw new ArgumentOutOfRangeException(nameof(showMediaType))
        };

    private static InputMedia ToInputMedia(ShowMediaType showMediaType, string fileId, string? caption) =>
        showMediaType switch
        {
            ShowMediaType.Photo     => new InputMediaPhoto(InputFile.FromFileId(fileId))     { Caption = caption },
            ShowMediaType.Video     => new InputMediaVideo(InputFile.FromFileId(fileId))     { Caption = caption },
            ShowMediaType.Document  => new InputMediaDocument(InputFile.FromFileId(fileId))  { Caption = caption },
            ShowMediaType.Animation => new InputMediaAnimation(InputFile.FromFileId(fileId)) { Caption = caption },
            _ => throw new ArgumentOutOfRangeException(nameof(showMediaType))
        };
}
