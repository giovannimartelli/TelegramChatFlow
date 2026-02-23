using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Rendering degli step e menu principale.
/// Costruisce anche le tastiere inline e di navigazione.
/// </summary>
public sealed class StepRenderer
{
    private readonly MessageManager _messages;
    private readonly FlowBotOptions _options;
    private readonly FlowRegistry _registry;

    public StepRenderer(
        MessageManager messages,
        IOptions<FlowBotOptions> options,
        FlowRegistry registry)
    {
        _messages = messages;
        _options = options.Value;
        _registry = registry;
    }

    /// <summary>Renderizza uno step: testo/media + tastiera inline.</summary>
    public async Task RenderStepAsync(
        FlowSession session, StepDefinition step, string? error = null,
        ShowDefinition? showOverride = null)
    {
        await _messages.CleanupTransientMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await _messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        var ctx = new FlowContext { Data = session.Data };
        var show = showOverride ?? step.Show;

        switch (show.ContentType)
        {
            case ShowContentType.Text:
            {
                var text = await show.Text!(ctx);
                if (error is not null) text += $"\n\n⚠️ {error}";

                if (step.InputType == InputType.ReplyKeyboard && step.ReplyKeyboardProvider is not null)
                {
                    var nav = new InlineKeyboardMarkup([BuildNavigationRow(session, step)]);
                    await _messages.SendOrEditAsync(session, text, nav);

                    var buttons = await step.ReplyKeyboardProvider(ctx);
                    var rows = buttons.Select(b => new[] { new KeyboardButton(b) });
                    var replyMarkup = new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true, OneTimeKeyboard = true };
                    await _messages.SendReplyKeyboardAsync(session, "👇", replyMarkup);
                    session.HasReplyKeyboard = true;
                }
                else
                {
                    var markup = await BuildStepKeyboardAsync(session, step, ctx);
                    await _messages.SendOrEditAsync(session, text, markup);
                }
                break;
            }

            case ShowContentType.Media:
            {
                var fileId = show.MediaFileId!(ctx);
                var caption = show.Caption?.Invoke(ctx);
                if (error is not null) caption = (caption is null ? "" : caption + "\n\n") + $"⚠️ {error}";
                var markup = await BuildStepKeyboardAsync(session, step, ctx);
                await _messages.SendOrEditMediaAsync(session, show.Media!.Value, fileId, caption, markup);
                break;
            }

            case ShowContentType.TextWithMedia:
            {
                var text = await show.Text!(ctx);
                if (error is not null) text += $"\n\n⚠️ {error}";
                var markup = await BuildStepKeyboardAsync(session, step, ctx);
                await _messages.SendOrEditAsync(session, text, markup);

                var fileId = show.MediaFileId!(ctx);
                await _messages.SendTrackedMediaAsync(session, show.Media!.Value, fileId);
                break;
            }
        }
    }

    /// <summary>Mostra il menu principale con tutti i flussi root.</summary>
    public async Task ShowMenuAsync(FlowSession session)
    {
        var rows = _registry.RootFlows
            .Select(f => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(f.Label, $"flow:{f.Id}")
            })
            .ToList();

        await _messages.SendOrEditAsync(session, _options.MainMenuText, new InlineKeyboardMarkup(rows));
    }

    private async Task<InlineKeyboardMarkup> BuildStepKeyboardAsync(
        FlowSession session, StepDefinition step, FlowContext ctx)
    {
        var rows = new List<List<InlineKeyboardButton>>();

        if (step.InputType == InputType.InlineButtons && step.ButtonsProvider is not null)
        {
            foreach (var btn in await step.ButtonsProvider(ctx))
                rows.Add([InlineKeyboardButton.WithCallbackData(btn.Text, btn.CallbackData)]);
        }
        else if (step.InputType == InputType.WebApp && step.WebAppUrlProvider is not null)
        {
            var url = await step.WebAppUrlProvider(ctx);
            rows.Add([InlineKeyboardButton.WithWebApp("🌐 Apri", new WebAppInfo { Url = url })]);
        }

        rows.Add(BuildNavigationRow(session, step));
        return new InlineKeyboardMarkup(rows);
    }

    private List<InlineKeyboardButton> BuildNavigationRow(FlowSession session, StepDefinition? step)
    {
        var nav = new List<InlineKeyboardButton>();

        bool canGoBack = session.StepHistory.Count > 0 || session.FlowStack.Count > 0;
        if (canGoBack && step?.ShowBack != false)
            nav.Add(InlineKeyboardButton.WithCallbackData("◀️ Indietro", "nav:back"));

        if (step?.ShowMenu != false)
            nav.Add(InlineKeyboardButton.WithCallbackData("🏠 Menu", "nav:menu"));

        if (step?.Skippable == true)
            nav.Add(InlineKeyboardButton.WithCallbackData("⏭ Salta", "nav:skip"));

        return nav;
    }
}
