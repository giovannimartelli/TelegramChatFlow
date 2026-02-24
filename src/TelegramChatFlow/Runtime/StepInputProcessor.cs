using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using InputMedia = TelegramChatFlow.Models.Input.InputMedia;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// User input validation and step handler execution.
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

    /// <summary>Processes the user's input for the current step.</summary>
    public async Task ProcessStepInputAsync(FlowSession session, UserInput input)
    {
        var flow = _registry.GetFlow(session.CurrentFlowId!);
        if (flow is null) { await _navigator.ResetToMenuAsync(session); return; }

        if (session.CurrentStepIndex >= flow.Steps.Count) return;

        var step = flow.Steps[session.CurrentStepIndex];

        // Display-only step: no input to process
        if (step.InputType == InputType.None) return;

        // Validate that the input type matches the type expected by the step
        var mismatch = step.InputType switch
        {
            InputType.Text when input.Text is null => "Please send a text message.",
            InputType.Media when input.Media is null => "Please send a media file.",
            _ => null
        };

        if (mismatch is not null)
        {
            await _renderer.RenderStepAsync(session, step, mismatch);
            return;
        }

        // Ignore text messages on steps expecting inline buttons (no feedback)
        if (step.InputType == InputType.InlineButtons && input.CallbackData is null)
            return;

        // Snapshot data before the handler modifies it (for back navigation)
        var dataSnapshot = flow.CloneData(session.Data!);

        var context = flow.CreateContext(session.Data!);

        StepResult result;
        try
        {
            result = await step.HandleInput(context, input);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in handler for step {StepId} of flow {FlowId}",
                step.Id, flow.Id);
            context.ValidationError = "An error occurred. Please try again.";
            result = StepResult.Retry;
        }

        session.Data = context.FlowData;

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
            case StepResult.SubFlowResult { SubFlowId: var subFlowId }:
                await _navigator.StartSubFlowAsync(session, subFlowId, dataSnapshot);
                break;
            case StepResult.ExitResult:
                await _navigator.ResetToMenuAsync(session);
                break;
        }
    }

    /// <summary>Extracts the user input from a Telegram Message object.</summary>
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
