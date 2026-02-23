using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using InputMedia = TelegramChatFlow.Models.Input.InputMedia;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Validazione dell'input utente ed esecuzione dell'handler dello step.
/// </summary>
public sealed class StepInputProcessor
{
    private readonly FlowRegistry _registry;
    private readonly FlowNavigator _navigator;
    private readonly StepRenderer _renderer;
    private readonly ILogger<StepInputProcessor> _logger;

    public StepInputProcessor(
        FlowRegistry registry,
        FlowNavigator navigator,
        StepRenderer renderer,
        ILogger<StepInputProcessor> logger)
    {
        _registry = registry;
        _navigator = navigator;
        _renderer = renderer;
        _logger = logger;
    }

    /// <summary>Elabora l'input dell'utente per lo step corrente.</summary>
    public async Task ProcessStepInputAsync(FlowSession session, UserInput input)
    {
        var flow = _registry.GetFlow(session.CurrentFlowId!);
        if (flow is null) { await _navigator.ResetToMenuAsync(session); return; }

        if (session.CurrentStepIndex >= flow.Steps.Count) return;

        var step = flow.Steps[session.CurrentStepIndex];

        // Step display-only: nessun input da elaborare
        if (step.InputType == InputType.None) return;

        // Valida che il tipo di input corrisponda a quello atteso dallo step
        var mismatch = step.InputType switch
        {
            InputType.Text when input.Text is null => "Invia un messaggio di testo.",
            InputType.Media when input.Media is null => "Invia un file multimediale.",
            InputType.InlineButtons when input.CallbackData is null => null,
            _ => null
        };

        if (mismatch is not null)
        {
            await _renderer.RenderStepAsync(session, step, mismatch);
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
                await _navigator.AdvanceAsync(session, flow, dataSnapshot);
                break;
            case StepResult.RetryResult { Show: { } retryShow }:
                await _renderer.RenderStepAsync(session, step, context.ValidationError, retryShow);
                break;
            case StepResult.RetryResult:
                await _renderer.RenderStepAsync(session, step, context.ValidationError);
                break;
            case StepResult.GoToResult { StepId: var targetId }:
                await _navigator.GoToStepAsync(session, flow, targetId, dataSnapshot);
                break;
            case StepResult.ExitResult:
                await _navigator.ResetToMenuAsync(session);
                break;
        }
    }

    /// <summary>Estrae l'input dall'oggetto Message di Telegram.</summary>
    public static UserInput ExtractUserInput(Message message)
    {
        if (message.Photo is { Length: > 0 } photos)
        {
            var photo = photos.OrderByDescending(t => t.Height * t.Width).First();
            return new UserInput { Media = new InputMedia(InputMediaType.Photo, photo.FileId, FileSize: photo.FileSize) };
        }
        if (message.Video is { } video)
            return new UserInput { Media = new InputMedia(InputMediaType.Video, video.FileId, video.FileName, video.MimeType, video.FileSize) };
        if (message.Audio is { } audio)
            return new UserInput { Media = new InputMedia(InputMediaType.Audio, audio.FileId, audio.FileName, audio.MimeType, audio.FileSize) };
        if (message.Voice is { } voice)
            return new UserInput { Media = new InputMedia(InputMediaType.Voice, voice.FileId, MimeType: voice.MimeType, FileSize: voice.FileSize) };
        if (message.VideoNote is { } videoNote)
            return new UserInput { Media = new InputMedia(InputMediaType.VideoNote, videoNote.FileId, FileSize: videoNote.FileSize) };
        if (message.Sticker is { } sticker)
            return new UserInput { Media = new InputMedia(InputMediaType.Sticker, sticker.FileId) };
        if (message.Animation is { } animation)
            return new UserInput { Media = new InputMedia(InputMediaType.Animation, animation.FileId, animation.FileName, animation.MimeType, animation.FileSize) };
        if (message.Document is { } doc)
            return new UserInput { Media = new InputMedia(InputMediaType.Document, doc.FileId, doc.FileName, doc.MimeType, doc.FileSize) };
        if (message.WebAppData is { } webApp)
            return new UserInput { WebAppData = webApp.Data };
        return new UserInput { Text = message.Text };
    }
}
