using TelegramChatFlow.Builder;
using TelegramChatFlow.Builder.Flow;

namespace TelegramChatFlow.Example.Flows;

/// <summary>
/// Flusso di esempio a 4 step: categoria → messaggio → conferma → risultato.
/// Dimostra l'uso di bottoni inline, input testuale, validazione, riepilogo e step display-only.
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
            // ── Step 1: selezione categoria ──────────────
            .Step("category", step => step
                .Show(s => s.HasText("Che tipo di feedback vuoi lasciare?"))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("🐛 Bug", "bug"),
                        new InlineButton("💡 Suggerimento", "suggestion"),
                        new InlineButton("⭐ Complimento", "praise"))
                    .OnInput((ctx, callbackData) =>
                    {
                        ctx.Data.Category = callbackData;
                    })))

            // ── Step 2: messaggio testuale ───────────────
            .Step("message", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var label = FormatCategory(ctx.Data.Category);
                    return $"Categoria: {label}\n\nScrivi il tuo messaggio:";
                }))
                .Input(i => i
                    .UsingText()
                    .OnInput((ctx, text) =>
                    {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            ctx.ValidationError = "Il messaggio non può essere vuoto.";
                            return StepResult.Retry;
                        }
                        ctx.Data.Message = text;
                        return StepResult.Next;
                    })))

            // ── Step 3: riepilogo e conferma ─────────────
            .Step("confirm", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var label = FormatCategory(ctx.Data.Category);
                    var msg = ctx.Data.Message;
                    return $"📋 Riepilogo\n\n" +
                           $"Categoria: {label}\n" +
                           $"Messaggio: {msg}\n\n" +
                           $"Confermi l'invio?";
                }))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("✅ Conferma", "confirm"),
                        new InlineButton("❌ Annulla", "cancel"))
                    .OnInput((ctx, callbackData) =>
                    {
                        if (callbackData == "cancel")
                        {
                            ctx.Data.Cancelled = true;
                            return StepResult.Next;
                        }
                        // Qui salveresti il feedback su DB, invieresti una notifica, ecc.
                        Console.WriteLine($"[Feedback ricevuto] Categoria: {ctx.Data.Category} | Messaggio: {ctx.Data.Message}");
                        return StepResult.Next;
                    })))

            // ── Step 4: risultato (display-only) ─────────
            .Step("result", step => step
                .Show(s => s.HasText(ctx => ctx.Data.Cancelled
                    ? "❌ Oh nooo, hai annullato!"
                    : "✅ OOOOK MANDATO, feedback inviato!")));
    }

    private static string FormatCategory(string? code) => code switch
    {
        "bug" => "🐛 Bug",
        "suggestion" => "💡 Suggerimento",
        "praise" => "⭐ Complimento",
        _ => "❓ Altro"
    };
}
