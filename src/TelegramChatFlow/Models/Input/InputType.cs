namespace TelegramChatFlow.Models.Input;

/// <summary>Tipo di input atteso da uno step.</summary>
public enum InputType
{
    /// <summary>Step display-only: nessun input atteso dall'utente.</summary>
    None = 0,
    InlineButtons,
    Text,
    Media,
    WebApp,
    ReplyKeyboard
}
