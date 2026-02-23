namespace TelegramChatFlow.Models.Input;

/// <summary>Media ricevuto dall'utente.</summary>
public record InputMedia(
    InputMediaType Type,
    string FileId,
    string? FileName = null,
    string? MimeType = null,
    long? FileSize = null);
