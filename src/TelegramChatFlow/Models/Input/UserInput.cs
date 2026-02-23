using Telegram.Bot.Types;

namespace TelegramChatFlow.Models.Input;

/// <summary>Input ricevuto dall'utente.</summary>
public sealed class UserInput
{
    public string? Text { get; init; }
    public string? CallbackData { get; init; }
    public Document? Document { get; init; }
    public string? FileId { get; init; }
    public string? WebAppData { get; init; }
}
