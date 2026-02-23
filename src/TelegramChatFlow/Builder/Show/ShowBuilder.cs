namespace TelegramChatFlow.Builder.Show;

/// <summary>Builder per la configurazione del contenuto visivo di uno step.</summary>
public sealed class ShowBuilder<TData> where TData : class, new()
{
    private Func<FlowContext, Task<string>>? _text;
    private ShowMediaType? _mediaType;
    private Func<FlowContext, string>? _mediaFileId;
    private Func<FlowContext, string>? _caption;

    // ── Text ──────────────────────────────────────────────

    public ShowBuilder<TData> HasText(string text)
    {
        _text = _ => Task.FromResult(text);
        return this;
    }

    public ShowBuilder<TData> HasText(Func<FlowContext<TData>, string> provider)
    {
        _text = ctx => Task.FromResult(provider((FlowContext<TData>)ctx));
        return this;
    }

    public ShowBuilder<TData> HasText(Func<FlowContext<TData>, Task<string>> provider)
    {
        _text = ctx => provider((FlowContext<TData>)ctx);
        return this;
    }

    // ── Media ─────────────────────────────────────────────

    public ShowBuilder<TData> HasPhoto(string fileId, string? caption = null) =>
        HasMedia(ShowMediaType.Photo, _ => fileId, caption is null ? null : _ => caption);

    public ShowBuilder<TData> HasPhoto(Func<FlowContext<TData>, string> fileIdProvider, Func<FlowContext<TData>, string>? captionProvider = null) =>
        HasMedia(ShowMediaType.Photo, ctx => fileIdProvider((FlowContext<TData>)ctx), captionProvider is null ? null : ctx => captionProvider((FlowContext<TData>)ctx));

    public ShowBuilder<TData> HasVideo(string fileId, string? caption = null) =>
        HasMedia(ShowMediaType.Video, _ => fileId, caption is null ? null : _ => caption);

    public ShowBuilder<TData> HasVideo(Func<FlowContext<TData>, string> fileIdProvider, Func<FlowContext<TData>, string>? captionProvider = null) =>
        HasMedia(ShowMediaType.Video, ctx => fileIdProvider((FlowContext<TData>)ctx), captionProvider is null ? null : ctx => captionProvider((FlowContext<TData>)ctx));

    public ShowBuilder<TData> HasDocument(string fileId, string? caption = null) =>
        HasMedia(ShowMediaType.Document, _ => fileId, caption is null ? null : _ => caption);

    public ShowBuilder<TData> HasDocument(Func<FlowContext<TData>, string> fileIdProvider, Func<FlowContext<TData>, string>? captionProvider = null) =>
        HasMedia(ShowMediaType.Document, ctx => fileIdProvider((FlowContext<TData>)ctx), captionProvider is null ? null : ctx => captionProvider((FlowContext<TData>)ctx));

    public ShowBuilder<TData> HasAnimation(string fileId, string? caption = null) =>
        HasMedia(ShowMediaType.Animation, _ => fileId, caption is null ? null : _ => caption);

    public ShowBuilder<TData> HasAnimation(Func<FlowContext<TData>, string> fileIdProvider, Func<FlowContext<TData>, string>? captionProvider = null) =>
        HasMedia(ShowMediaType.Animation, ctx => fileIdProvider((FlowContext<TData>)ctx), captionProvider is null ? null : ctx => captionProvider((FlowContext<TData>)ctx));

    private ShowBuilder<TData> HasMedia(ShowMediaType type, Func<FlowContext, string> fileIdProvider, Func<FlowContext, string>? captionProvider)
    {
        _mediaType = type;
        _mediaFileId = fileIdProvider;
        _caption = captionProvider;
        return this;
    }

    // ── Factory ─────────────────────────────────────────────

    /// <summary>Crea una <see cref="ShowDefinition"/> inline tramite il builder.</summary>
    public static ShowDefinition Create(Action<ShowBuilder<TData>> configure)
    {
        var builder = new ShowBuilder<TData>();
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
