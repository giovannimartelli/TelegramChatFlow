using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Step and main menu rendering.
/// Also builds inline and navigation keyboards.
/// </summary>
public sealed class StepRenderer(
    MessageManager messages,
    IOptions<FlowBotOptions> options,
    FlowRegistry registry)
{
    private readonly FlowBotOptions _options = options.Value;

    /// <summary>Renders a step: text/media + inline keyboard.</summary>
    public async Task RenderStepAsync(FlowSession session, StepDefinition step, string? error = null, ShowDefinition? showOverride = null)
    {
        await messages.CleanupTransientMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        var flow = registry.GetFlow(session.CurrentFlowId!);
        var ctx = flow!.CreateContext(session.Data!);
        var show = showOverride ?? step.Show;

        switch (show.ContentType)
        {
            case ShowContentType.Text:
            {
                var text = await show.Text!(ctx);
                if (error is not null) text += $"\n\n⚠️ {error}";

                if (step is { InputType: InputType.ReplyKeyboard, ReplyKeyboardProvider: not null })
                {
                    var nav = new InlineKeyboardMarkup([BuildNavigationRow(session, step)]);
                    await messages.SendOrEditAsync(session, text, nav);

                    var buttons = await step.ReplyKeyboardProvider(ctx);
                    var rows = buttons.Select(b => new[] { new KeyboardButton(b) });
                    var replyMarkup = new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true, OneTimeKeyboard = true };
                    await messages.SendReplyKeyboardAsync(session, "👇", replyMarkup);
                    session.HasReplyKeyboard = true;
                }
                else
                {
                    var markup = await BuildStepKeyboardAsync(session, step, ctx);
                    await messages.SendOrEditAsync(session, text, markup);
                }

                break;
            }

            case ShowContentType.Media:
            {
                var fileId = show.MediaFileId!(ctx);
                var caption = show.Caption?.Invoke(ctx);
                if (error is not null) caption = (caption is null ? "" : caption + "\n\n") + $"⚠️ {error}";
                var markup = await BuildStepKeyboardAsync(session, step, ctx);
                await messages.SendOrEditMediaAsync(session, show.Media!.Value, fileId, caption, markup);
                break;
            }

            case ShowContentType.TextWithMedia:
            {
                var text = await show.Text!(ctx);
                if (error is not null) text += $"\n\n⚠️ {error}";
                var markup = await BuildStepKeyboardAsync(session, step, ctx);
                await messages.SendOrEditAsync(session, text, markup);

                var fileId = show.MediaFileId!(ctx);
                await messages.SendTrackedMediaAsync(session, show.Media!.Value, fileId);
                break;
            }
        }
    }

    /// <summary>Shows the main menu with all root flows.</summary>
    public async Task ShowMenuAsync(FlowSession session)
    {
        var rows = registry.RootFlows
            .Select(f => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(f.Label, $"flow:{f.Id}")
            })
            .ToList();

        await messages.SendOrEditAsync(session, _options.MainMenuText, new InlineKeyboardMarkup(rows));
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
            rows.Add([InlineKeyboardButton.WithWebApp("🌐 Open", new WebAppInfo { Url = url })]);
        }

        rows.Add(BuildNavigationRow(session, step));
        return new InlineKeyboardMarkup(rows);
    }

    private List<InlineKeyboardButton> BuildNavigationRow(FlowSession session, StepDefinition? step)
    {
        var nav = new List<InlineKeyboardButton>();

        bool canGoBack = session.StepHistory.Count > 0 || session.FlowStack.Count > 0;
        if (canGoBack && step?.ShowBack != false)
            nav.Add(InlineKeyboardButton.WithCallbackData("◀️ Back", "nav:back"));

        if (step?.ShowMenu != false)
            nav.Add(InlineKeyboardButton.WithCallbackData("🏠 Menu", "nav:menu"));

        if (step?.Skippable == true)
            nav.Add(InlineKeyboardButton.WithCallbackData("⏭ Skip", "nav:skip"));

        return nav;
    }
}