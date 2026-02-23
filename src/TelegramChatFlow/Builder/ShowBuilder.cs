namespace TelegramChatFlow.Builder;

/// <summary>Builder per la configurazione del contenuto visivo di uno step.</summary>
public sealed class ShowBuilder
{
    private Func<FlowContext, Task<string>>? _text;
    private MediaType? _mediaType;
    private Func<FlowContext, string>? _mediaFileId;
    private Func<FlowContext, string>? _caption;

    // ── Text ──────────────────────────────────────────────

    public ShowBuilder HasText(string text)
    {
        _text = _ => Task.FromResult(text);
        return this;
    }

    public ShowBuilder HasText(Func<FlowContext, string> provider)
    {
        _text = ctx => Task.FromResult(provider(ctx));
        return this;
    }

    public ShowBuilder HasText(Func<FlowContext, Task<string>> provider)
    {
        _text = provider;
        return this;
    }

    // ── Media ─────────────────────────────────────────────

    public ShowBuilder HasPhoto(string fileId, string? caption = null) =>
        HasMedia(MediaType.Photo, _ => fileId, caption is null ? null : _ => caption);

    public ShowBuilder HasPhoto(Func<FlowContext, string> fileIdProvider, Func<FlowContext, string>? captionProvider = null) =>
        HasMedia(MediaType.Photo, fileIdProvider, captionProvider);

    public ShowBuilder HasVideo(string fileId, string? caption = null) =>
        HasMedia(MediaType.Video, _ => fileId, caption is null ? null : _ => caption);

    public ShowBuilder HasVideo(Func<FlowContext, string> fileIdProvider, Func<FlowContext, string>? captionProvider = null) =>
        HasMedia(MediaType.Video, fileIdProvider, captionProvider);

    public ShowBuilder HasDocument(string fileId, string? caption = null) =>
        HasMedia(MediaType.Document, _ => fileId, caption is null ? null : _ => caption);

    public ShowBuilder HasDocument(Func<FlowContext, string> fileIdProvider, Func<FlowContext, string>? captionProvider = null) =>
        HasMedia(MediaType.Document, fileIdProvider, captionProvider);

    public ShowBuilder HasAnimation(string fileId, string? caption = null) =>
        HasMedia(MediaType.Animation, _ => fileId, caption is null ? null : _ => caption);

    public ShowBuilder HasAnimation(Func<FlowContext, string> fileIdProvider, Func<FlowContext, string>? captionProvider = null) =>
        HasMedia(MediaType.Animation, fileIdProvider, captionProvider);

    private ShowBuilder HasMedia(MediaType type, Func<FlowContext, string> fileIdProvider, Func<FlowContext, string>? captionProvider)
    {
        _mediaType = type;
        _mediaFileId = fileIdProvider;
        _caption = captionProvider;
        return this;
    }

    // ── Factory ─────────────────────────────────────────────

    /// <summary>Crea una <see cref="ShowDefinition"/> inline tramite il builder.</summary>
    public static ShowDefinition Create(Action<ShowBuilder> configure)
    {
        var builder = new ShowBuilder();
        configure(builder);
        return builder.Build();
    }

    // ── Build ─────────────────────────────────────────────

    internal ShowDefinition Build()
    {
        var contentType = (_text, _mediaType) switch
        {
            ({ }, { }) => ShowContentType.TextWithMedia,
            (null, { }) => ShowContentType.Media,
            _           => ShowContentType.Text
        };

        return new ShowDefinition
        {
            ContentType  = contentType,
            Text         = _text ?? (_ => Task.FromResult("")),
            Media        = _mediaType,
            MediaFileId  = _mediaFileId,
            Caption      = _caption
        };
    }
}
