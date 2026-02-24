using TelegramChatFlow.Builder;
using TelegramChatFlow.Builder.Flow;

namespace TelegramChatFlow.Example.Flows;

/// <summary>
/// Example flow with 4 steps: category → message → confirmation → result.
/// Demonstrates the use of inline buttons, text input, validation, summary, and display-only steps.
/// </summary>
public sealed class FeedbackFlow : FlowBase<FeedbackFlow.FeedbackData>
{
    protected override string Id => "feedback";
    protected override string MenuLabel => "📝 Feedback";

    public sealed class FeedbackData
    {
        public string? Category { get; set; }
        public string? Message { get; set; }
        public bool Cancelled { get; set; }
    }

    protected override void Configure(FlowBuilder<FeedbackData> builder)
    {
        builder
            // ── Step 1: category selection ──────────────
            .Step("category", step => step
                .Show(s => s.HasText("What type of feedback do you want to leave?"))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("🐛 Bug", "bug"),
                        new InlineButton("💡 Suggestion", "suggestion"),
                        new InlineButton("⭐ Compliment", "praise"))
                    .OnInput((ctx, callbackData) =>
                    {
                        ctx.Data.Category = callbackData;
                    })))

            // ── Step 2: text message ───────────────
            .Step("message", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var label = FormatCategory(ctx.Data.Category);
                    return $"Category: {label}\n\nWrite your message:";
                }))
                .Input(i => i
                    .UsingText()
                    .OnInput((ctx, text) =>
                    {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            ctx.ValidationError = "The message cannot be empty.";
                            return StepResult.Retry;
                        }
                        ctx.Data.Message = text;
                        return StepResult.Next;
                    })))

            // ── Step 3: summary and confirmation ─────────────
            .Step("confirm", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var label = FormatCategory(ctx.Data.Category);
                    var msg = ctx.Data.Message;
                    return $"📋 Summary\n\n" +
                           $"Category: {label}\n" +
                           $"Message: {msg}\n\n" +
                           $"Confirm sending?";
                }))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("✅ Confirm", "confirm"),
                        new InlineButton("❌ Cancel", "cancel"))
                    .OnInput((ctx, callbackData) =>
                    {
                        if (callbackData == "cancel")
                        {
                            ctx.Data.Cancelled = true;
                            return StepResult.Next;
                        }
                        // Here you would save the feedback to DB, send a notification, etc.
                        Console.WriteLine($"[Feedback received] Category: {ctx.Data.Category} | Message: {ctx.Data.Message}");
                        return StepResult.Next;
                    })))

            // ── Step 4: result (display-only) ─────────
            .Step("result", step => step
                .Show(s => s.HasText(ctx => ctx.Data.Cancelled
                    ? "❌ Cancelled!"
                    : "✅ Feedback sent!")));
    }

    private static string FormatCategory(string? code) => code switch
    {
        "bug" => "🐛 Bug",
        "suggestion" => "💡 Suggestion",
        "praise" => "⭐ Compliment",
        _ => "❓ Other"
    };
}
