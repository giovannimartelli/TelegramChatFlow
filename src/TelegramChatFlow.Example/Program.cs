using TelegramChatFlow;
using TelegramChatFlow.Example.Flows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddFlowFramework(options =>
    {
        options.BotToken = builder.Configuration["Bot:Token"] ?? throw new InvalidOperationException("Configure Bot:Token in appsettings.json");

        var allowedUsers = builder.Configuration.GetSection("Bot:AllowedUsers").Get<long[]>();
        if (allowedUsers is { Length: > 0 })
            options.AllowedUsers = [.. allowedUsers];

        options.MainMenuText = "📋 Main Menu\n\nSelect an option:";
        options.InactivityTimeout = TimeSpan.FromMinutes(5);
        options.WatchdogInterval = TimeSpan.FromSeconds(30);
    })
    .AddFlow<FeedbackFlow>()
    .AddFlow<ListingFlow>();

var app = builder.Build();
await app.RunAsync();