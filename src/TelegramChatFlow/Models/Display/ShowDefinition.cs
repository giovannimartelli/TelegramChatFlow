namespace TelegramChatFlow.Models.Display;

/// <summary>Definizione del contenuto visivo di uno step.</summary>
public sealed class ShowDefinition
{
    public ShowContentType ContentType { get; init; }
    public Func<FlowContext, Task<string>>? Text { get; init; }
    public ShowMediaType? Media { get; init; }
    public Func<FlowContext, string>? MediaFileId { get; init; }
    public Func<FlowContext, string>? Caption { get; init; }
}
