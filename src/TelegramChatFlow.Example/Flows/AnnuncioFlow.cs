using System.Text;
using System.Text.Json.Serialization;
using TelegramChatFlow.Builder;
using TelegramChatFlow.Builder.Flow;

namespace TelegramChatFlow.Example.Flows;

/// <summary>
/// Complete flow for publishing a real estate listing.
/// Demonstrates: inline buttons, reply keyboard, text input with validation,
/// media input/output, Persistent, Skippable, GoTo, dynamic text, handler-launched sub-flow.
/// </summary>
public sealed class ListingFlow : FlowBase<ListingFlow.ListingData>
{
    public sealed class ListingData
    {
        public string? PropertyType { get; set; }
        public string? Area { get; set; }
        public string? Description { get; set; }
        public int? Price { get; set; }
        public List<PhotoInfo> PhotoList { get; } = [];
        public string? TempPhotoId { get; set; }
        public string? TempPhotoDescription { get; set; }
        public string? AttachmentId { get; set; }
        public string? AttachmentName { get; set; }
    }

    public sealed class PhotoInfo
    {
        public string FileId { get; set; } = "";
        public string Description { get; set; } = "";
        public string Tag { get; set; } = "";

        [JsonConstructor]
        public PhotoInfo() { }

        public PhotoInfo(string fileId, string description, string tag)
        {
            FileId = fileId;
            Description = description;
            Tag = tag;
        }
    }

    protected override string Id => "listing";
    protected override string MenuLabel => "🏠 New Listing";

    protected override void Configure(FlowBuilder<ListingData> builder)
    {
        builder
            // ── Step 1: property type (inline buttons, HideBack) ──
            .Step("property-type", step => step
                .HideBack()
                .Show(s => s.HasText("What type of property do you want to publish?"))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("🏢 Apartment", "apartment"),
                        new InlineButton("🏡 Villa", "villa"),
                        new InlineButton("🏬 Office", "office"),
                        new InlineButton("🏪 Commercial Space", "commercial"))
                    .OnInput((ctx, value) =>
                    {
                        ctx.Data.PropertyType = value;
                    })))

            // ── Step 2: area (reply keyboard) ─────────────────────
            .Step("area", step => step
                .Show(s => s.HasText("What area is the property in?"))
                .Input(i => i
                    .UsingKeyboard("City Center", "Hills", "Suburbs", "Seaside", "Countryside")
                    .OnInput((ctx, area) =>
                    {
                        ctx.Data.Area = area;
                    })))

            // ── Step 3: description (text + min 20 char validation, dynamic text) ──
            .Step("description", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var type = FormatPropertyType(ctx.Data.PropertyType);
                    var area = ctx.Data.Area;
                    return $"Type: {type}\nArea: {area}\n\n" +
                           "Write a description of the property (minimum 20 characters):";
                }))
                .Input(i => i
                    .UsingText()
                    .OnInput((ctx, text) =>
                    {
                        if (text.Length < 20)
                        {
                            ctx.ValidationError = "The description must be at least 20 characters.";
                            return StepResult.Retry;
                        }
                        ctx.Data.Description = text;
                        return StepResult.Next;
                    })))

            // ── Step 4: price (text + numeric validation) ──────
            .Step("price", step => step
                .Show(s => s.HasText("Enter the price in euros (numbers only):"))
                .Input(i => i
                    .UsingText()
                    .OnInput((ctx, text) =>
                    {
                        if (!int.TryParse(text, out var price) || price < 1)
                        {
                            ctx.ValidationError = "Enter a valid price (positive integer).";
                            return StepResult.Retry;
                        }
                        ctx.Data.Price = price;
                        return StepResult.Next;
                    })))

            // ── Step 5: start photo details sub-flow ──────────────
            .Step("start-photos", step => step
                .Show(s => s.HasText("Let's add photos of the property"))
                .Input(i => i
                    .UsingButtons(new InlineButton("📸 Start", "go"))
                    .OnInput((ctx, cb) => StepResult.SubFlow("photo-details"))))

            // ── Step 6: confirm photos (photo list summary) ──
            .Step("confirm-photos", step => step
                .Show(s => s.HasText(ctx =>
                {
                    var list = ctx.Data.PhotoList;
                    var sb = new StringBuilder();
                    sb.AppendLine($"📸 {list.Count} photo(s) uploaded:\n");
                    for (var i = 0; i < list.Count; i++)
                        sb.AppendLine($"{i + 1}. {list[i].Description} [{list[i].Tag}]");
                    sb.AppendLine("\nAre you done?");
                    return sb.ToString();
                }))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("✅ Yes", "yes"),
                        new InlineButton("🔄 No, redo", "no"))
                    .OnInput((ctx, cb) => cb == "no"
                        ? StepResult.GoTo("start-photos")
                        : StepResult.Next)))

            // ── Step 7: optional attachment (media, Skippable) ────
            .Step("attachment", step => step
                .Skippable()
                .Show(s => s.HasText("Optionally, send a document or floor plan (or skip):"))
                .Input(i => i
                    .UsingMedia()
                    .OnInput((ctx, media) =>
                    {
                        ctx.Data.AttachmentId = media.FileId;
                        ctx.Data.AttachmentName = media.FileName ?? "document";
                    })))

            // ── Step 8: summary (dynamic text + media output + GoTo/Exit) ──
            .Step("summary", step => step
                .Show(s => s
                    .HasPhoto(
                        ctx => ctx.Data.PhotoList[0].FileId,
                        ctx =>
                        {
                            var type = FormatPropertyType(ctx.Data.PropertyType);
                            var area = ctx.Data.Area;
                            var desc = ctx.Data.Description;
                            var price = ctx.Data.Price;
                            var photoCount = ctx.Data.PhotoList.Count;
                            var attachment = ctx.Data.AttachmentName;

                            return $"📋 Listing Summary\n\n" +
                                   $"🏠 Type: {type}\n" +
                                   $"📍 Area: {area}\n" +
                                   $"📝 Description: {desc}\n" +
                                   $"💰 Price: {price:N0} €\n" +
                                   $"📸 Photos: {photoCount}\n" +
                                   $"📎 Attachment: {(attachment != null ? attachment : "none")}";
                        }))
                .Input(i => i
                    .UsingButtons(
                        new InlineButton("✅ Publish", "publish"),
                        new InlineButton("✏️ Edit Description", "edit"),
                        new InlineButton("❌ Cancel", "cancel"))
                    .OnInput((ctx, callback) => callback switch
                    {
                        "edit" => StepResult.GoTo("description"),
                        "cancel" => StepResult.Exit,
                        _ => StepResult.Next
                    })))

            // ── Step 9: publish confirmation (display-only) ────
            .Step("published", step => step
                .HideBack()
                .Show(s => s.HasText("✅ Listing published successfully!")))

            // ── Sub-flow ─────────────────────────────────────────
            .SubFlow(new PhotoDetailsFlow());
    }

    private static string FormatPropertyType(string? code) => code switch
    {
        "apartment" => "🏢 Apartment",
        "villa" => "🏡 Villa",
        "office" => "🏬 Office",
        "commercial" => "🏪 Commercial Space",
        _ => "❓ Other"
    };

    // ── Sub-flow: Photo with details (multi-photo loop) ──────────
    private sealed class PhotoDetailsFlow : FlowBase<ListingData>
    {
        protected override string Id => "photo-details";
        protected override string MenuLabel => "📸 Photo Details";

        protected override void Configure(FlowBuilder<ListingData> builder)
        {
            builder
                // Upload photo
                .Step("upload", step => step
                    .Persistent()
                    .Show(s => s.HasText("Send a photo of the property:"))
                    .Input(i => i
                        .UsingMedia()
                        .OnInput((ctx, media) =>
                        {
                            ctx.Data.TempPhotoId = media.FileId;
                        })))

                // Photo description
                .Step("photo-desc", step => step
                    .Show(s => s.HasText("Write a description for the photo:"))
                    .Input(i => i
                        .UsingText()
                        .OnInput((ctx, text) =>
                        {
                            ctx.Data.TempPhotoDescription = text;
                        })))

                // Photo tag + add to list
                .Step("photo-tag", step => step
                    .Show(s => s.HasText("Enter a tag for the photo (e.g. exterior, interior, garden):"))
                    .Input(i => i
                        .UsingText()
                        .OnInput((ctx, text) =>
                        {
                            var photo = new PhotoInfo(
                                ctx.Data.TempPhotoId!,
                                ctx.Data.TempPhotoDescription!,
                                text);
                            ctx.Data.PhotoList.Add(photo);
                        })))

                // Ask if user wants to add another photo
                .Step("add-photo", step => step
                    .Show(s => s.HasText(ctx =>
                    {
                        var count = ctx.Data.PhotoList.Count;
                        return $"📸 {count} photo(s) uploaded. Do you want to add another?";
                    }))
                    .Input(i => i
                        .UsingButtons(
                            new InlineButton("📸 Yes, another", "another"),
                            new InlineButton("✅ Done", "done"))
                        .OnInput((ctx, cb) => cb == "another"
                            ? StepResult.GoTo("upload")
                            : StepResult.Next)));
        }
    }
}
