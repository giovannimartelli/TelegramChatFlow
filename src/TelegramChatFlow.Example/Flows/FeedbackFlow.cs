using TelegramChatFlow.Builder;
using TelegramChatFlow.Builder.Flow;

namespace TelegramChatFlow.Example.Flows;

/// <summary>
/// Flusso di esempio a 4 step: categoria → messaggio → conferma → risultato.
/// Dimostra l'uso di bottoni inline, input testuale, validazione, riepilogo e step display-only.
/// </summary>
public sealed class FeedbackFlow : FlowBase
{
    public override string Id => "feedback";
    public override string MenuLabel => "📝 Feedback";

    protected override void Configure(FlowBuilder builder)
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
                        ctx.Set("category", callbackData);
                    })))

            // ── Step 2: messaggio testuale ───────────────
            .Step("message", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var label = FormatCategory(ctx.Get<string>("category"));
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
                        ctx.Set("message", text);
                        return StepResult.Next;
                    })))

            // ── Step 3: riepilogo e conferma ─────────────
            .Step("confirm", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var label = FormatCategory(ctx.Get<string>("category"));
                    var msg = ctx.Get<string>("message");
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
                            ctx.Set("cancelled", true);
                            return StepResult.Next;
                        }
                        // Qui salveresti il feedback su DB, invieresti una notifica, ecc.
                        var cat = ctx.Get<string>("category");
                        var msg = ctx.Get<string>("message");
                        Console.WriteLine($"[Feedback ricevuto] Categoria: {cat} | Messaggio: {msg}");
                        return StepResult.Next;
                    })))

            // ── Step 4: risultato (display-only) ─────────
            .Step("result", step => step
                .Show(s => s.HasText(ctx => ctx.TryGet<bool>("cancelled")
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
