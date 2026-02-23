using TelegramChatFlow.Builder;

namespace TelegramChatFlow.Example.Flows;

/// <summary>
/// Flusso di esempio a 3 step: categoria → messaggio → conferma.
/// Dimostra l'uso di bottoni inline, input testuale, validazione e riepilogo.
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
                .Text("Che tipo di feedback vuoi lasciare?")
                .Buttons(
                    new InlineButton("🐛 Bug", "bug"),
                    new InlineButton("💡 Suggerimento", "suggestion"),
                    new InlineButton("⭐ Complimento", "praise"))
                .OnInput((ctx, input) =>
                {
                    ctx.Set("category", input.CallbackData);
                    return true;
                }))

            // ── Step 2: messaggio testuale ───────────────
            .Step("message", step => step
                .Text(ctx =>
                {
                    var label = FormatCategory(ctx.Get<string>("category"));
                    return $"Categoria: {label}\n\nScrivi il tuo messaggio:";
                })
                .ExpectText()
                .OnInput((ctx, input) =>
                {
                    if (string.IsNullOrWhiteSpace(input.Text))
                    {
                        ctx.ValidationError = "Il messaggio non può essere vuoto.";
                        return StepResult.Retry;
                    }
                    ctx.Set("message", input.Text);
                    return StepResult.Next;
                }))

            // ── Step 3: riepilogo e conferma ─────────────
            .Step("confirm", step => step
                .Text(ctx =>
                {
                    var label = FormatCategory(ctx.Get<string>("category"));
                    var msg = ctx.Get<string>("message");
                    return $"📋 Riepilogo\n\n" +
                           $"Categoria: {label}\n" +
                           $"Messaggio: {msg}\n\n" +
                           $"Confermi l'invio?";
                })
                .Buttons(
                    new InlineButton("✅ Conferma", "confirm"),
                    new InlineButton("❌ Annulla", "cancel"))
                .OnInput((ctx, input) =>
                {
                    if (input.CallbackData == "cancel")
                        return StepResult.Exit;
                    if (input.CallbackData == null)
                        return StepResult.Retry;
                    // Qui salveresti il feedback su DB, invieresti una notifica, ecc.
                    var cat = ctx.Get<string>("category");
                    var msg = ctx.Get<string>("message");
                    Console.WriteLine($"[Feedback ricevuto] Categoria: {cat} | Messaggio: {msg}");

                    return StepResult.Next;
                }));
    }

    private static string FormatCategory(string? code) => code switch
    {
        "bug" => "🐛 Bug",
        "suggestion" => "💡 Suggerimento",
        "praise" => "⭐ Complimento",
        _ => "❓ Altro"
    };
}
