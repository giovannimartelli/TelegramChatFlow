using System.Text;
using TelegramChatFlow.Builder;
using TelegramChatFlow.Builder.Flow;

namespace TelegramChatFlow.Example.Flows;

/// <summary>
/// Flusso completo per la pubblicazione di un annuncio immobiliare.
/// Dimostra: bottoni inline, reply keyboard, text input con validazione,
/// media input/output, Persistent, Skippable, GoTo, testo dinamico, sub-flow handler-launched.
/// </summary>
public sealed class AnnuncioFlow : FlowBase
{
    private record FotoInfo(string FileId, string Descrizione, string Tag);

    public override string Id => "annuncio";
    public override string MenuLabel => "🏠 Nuovo Annuncio";

    protected override void Configure(FlowBuilder builder)
    {
        builder
            // ── Step 1: tipo immobile (inline buttons, HideBack) ──
            .Step("tipo", step => step
                .HideBack()
                .Show(s => s.HasText("Che tipo di immobile vuoi pubblicare?"))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("🏢 Appartamento", "appartamento"),
                        new InlineButton("🏡 Villa", "villa"),
                        new InlineButton("🏬 Ufficio", "ufficio"),
                        new InlineButton("🏪 Locale Commerciale", "locale"))
                    .OnInput((ctx, tipo) =>
                    {
                        ctx.Set("tipo", tipo);
                    })))

            // ── Step 2: zona (reply keyboard) ─────────────────────
            .Step("zona", step => step
                .Show(s => s.HasText("In quale zona si trova l'immobile?"))
                .Input(i => i
                    .UsingKeyboard("Centro", "Collina", "Periferia", "Mare", "Campagna")
                    .OnInput((ctx, zona) =>
                    {
                        ctx.Set("zona", zona);
                    })))

            // ── Step 3: descrizione (text + validazione min 20 char, testo dinamico) ──
            .Step("descrizione", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var tipo = FormatTipo(ctx.Get<string>("tipo"));
                    var zona = ctx.Get<string>("zona");
                    return $"Tipo: {tipo}\nZona: {zona}\n\n" +
                           "Scrivi una descrizione dell'immobile (minimo 20 caratteri):";
                }))
                .Input(i => i
                    .UsingText()
                    .OnInput((ctx, text) =>
                    {
                        if (text.Length < 20)
                        {
                            ctx.ValidationError = "La descrizione deve essere di almeno 20 caratteri.";
                            return StepResult.Retry;
                        }
                        ctx.Set("descrizione", text);
                        return StepResult.Next;
                    })))

            // ── Step 4: prezzo (text + validazione numerica) ──────
            .Step("prezzo", step => step
                .Show(s => s.HasText("Inserisci il prezzo in euro (solo numeri):"))
                .Input(i => i
                    .UsingText()
                    .OnInput((ctx, text) =>
                    {
                        if (!int.TryParse(text, out var prezzo) || prezzo < 1)
                        {
                            ctx.ValidationError = "Inserisci un prezzo valido (numero intero maggiore di 0).";
                            return StepResult.Retry;
                        }
                        ctx.Set("prezzo", prezzo);
                        return StepResult.Next;
                    })))

            // ── Step 5: avvia sub-flow foto dettagli ──────────────
            .Step("avvia_foto", step => step
                .Show(s => s.HasText("Inseriamo la foto dell'immobile"))
                .Input(i => i
                    .UsingButtons(new InlineButton("📸 Inizia", "go"))
                    .OnInput((ctx, cb) => StepResult.SubFlow("foto_dettagli"))))

            // ── Step 6: conferma foto (riepilogo lista foto) ──
            .Step("conferma_foto", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var lista = ctx.Get<List<FotoInfo>>("foto_lista");
                    var sb = new StringBuilder();
                    sb.AppendLine($"📸 {lista.Count} foto caricate:\n");
                    for (var i = 0; i < lista.Count; i++)
                        sb.AppendLine($"{i + 1}. {lista[i].Descrizione} [{lista[i].Tag}]");
                    sb.AppendLine("\nSei a posto?");
                    return sb.ToString();
                }))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("✅ Sì", "si"),
                        new InlineButton("🔄 No, rifai", "no"))
                    .OnInput((ctx, cb) => cb == "no"
                        ? StepResult.GoTo("avvia_foto")
                        : StepResult.Next)))

            // ── Step 7: allegato opzionale (media, Skippable) ────
            .Step("allegato", step => step
                .Skippable()
                .Show(s => s.HasText("Se vuoi, invia un documento o una planimetria (oppure salta):"))
                .Input(i => i
                    .UsingMedia()
                    .OnInput((ctx, media) =>
                    {
                        ctx.Set("allegato_id", media.FileId);
                        ctx.Set("allegato_nome", media.FileName ?? "documento");
                    })))

            // ── Step 8: riepilogo (testo dinamico + media output + GoTo/Exit) ──
            .Step("riepilogo", step => step
                .Show(s => s
                    .HasPhoto(
                        ctx => ctx.Get<List<FotoInfo>>("foto_lista")[0].FileId,
                        ctx =>
                        {
                            var tipo = FormatTipo(ctx.Get<string>("tipo"));
                            var zona = ctx.Get<string>("zona");
                            var desc = ctx.Get<string>("descrizione");
                            var prezzo = ctx.Get<int>("prezzo");
                            var fotoCount = ctx.Get<List<FotoInfo>>("foto_lista").Count;
                            var allegato = ctx.TryGet<string>("allegato_nome");

                            return $"📋 Riepilogo Annuncio\n\n" +
                                   $"🏠 Tipo: {tipo}\n" +
                                   $"📍 Zona: {zona}\n" +
                                   $"📝 Descrizione: {desc}\n" +
                                   $"💰 Prezzo: {prezzo:N0} €\n" +
                                   $"📸 Foto: {fotoCount}\n" +
                                   $"📎 Allegato: {(allegato != null ? allegato : "nessuno")}";
                        }))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("✅ Pubblica", "pubblica"),
                        new InlineButton("✏️ Modifica Descrizione", "modifica"),
                        new InlineButton("❌ Annulla", "annulla"))
                    .OnInput((ctx, callback) => callback switch
                    {
                        "modifica" => StepResult.GoTo("descrizione"),
                        "annulla" => StepResult.Exit,
                        _ => StepResult.Next
                    })))

            // ── Step 9: conferma pubblicazione (display-only) ────
            .Step("pubblicato", step => step
                .HideBack()
                .Show(s => s.HasText("✅ Annuncio pubblicato con successo!")))

            // ── Sub-flow ─────────────────────────────────────────
            .SubFlow(new FotoDettagliFlow());
    }

    private static string FormatTipo(string? code) => code switch
    {
        "appartamento" => "🏢 Appartamento",
        "villa" => "🏡 Villa",
        "ufficio" => "🏬 Ufficio",
        "locale" => "🏪 Locale Commerciale",
        _ => "❓ Altro"
    };

    // ── Sub-flow: Foto con dettagli (loop multi-foto) ──────────
    private sealed class FotoDettagliFlow : FlowBase
    {
        public override string Id => "foto_dettagli";
        public override string MenuLabel => "📸 Foto Dettagli";

        protected override void Configure(FlowBuilder builder)
        {
            builder
                // Upload foto
                .Step("upload", step => step
                    .Persistent()
                    .Show(s => s.HasText("Invia la foto dell'immobile:"))
                    .Input(i => i
                        .UsingMedia()
                        .OnInput((ctx, media) =>
                        {
                            ctx.Set("_foto_tmp_id", media.FileId);
                        })))

                // Descrizione foto
                .Step("desc_foto", step => step
                    .Show(s => s.HasText("Scrivi una descrizione per la foto:"))
                    .Input(i => i
                        .UsingText()
                        .OnInput((ctx, text) =>
                        {
                            ctx.Set("_foto_tmp_desc", text);
                        })))

                // Tag foto + accumula nella lista
                .Step("tag_foto", step => step
                    .Show(s => s.HasText("Inserisci un tag per la foto (es. esterno, interno, giardino):"))
                    .Input(i => i
                        .UsingText()
                        .OnInput((ctx, text) =>
                        {
                            var foto = new FotoInfo(
                                ctx.Get<string>("_foto_tmp_id"),
                                ctx.Get<string>("_foto_tmp_desc"),
                                text);
                            var lista = ctx.TryGet<List<FotoInfo>>("foto_lista") ?? new List<FotoInfo>();
                            lista.Add(foto);
                            ctx.Set("foto_lista", lista);
                        })))

                // Chiedi se aggiungere un'altra foto
                .Step("altra_foto", step => step
                    .Show(s => s.HasText(ctx =>
                    {
                        var count = ctx.Get<List<FotoInfo>>("foto_lista").Count;
                        return $"📸 {count} foto caricata/e. Vuoi aggiungerne un'altra?";
                    }))
                    .Input(i => i
                        .UsingButtons(
                            new InlineButton("📸 Sì, un'altra", "altra"),
                            new InlineButton("✅ Ho finito", "fine"))
                        .OnInput((ctx, cb) => cb == "altra"
                            ? StepResult.GoTo("upload")
                            : StepResult.Next)));
        }
    }
}
