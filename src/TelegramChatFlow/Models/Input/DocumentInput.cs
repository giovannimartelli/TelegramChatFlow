using Telegram.Bot.Types;

namespace TelegramChatFlow.Models.Input;

/// <summary>Input documento con file associato.</summary>
public record DocumentInput(Document Document, string FileId);
