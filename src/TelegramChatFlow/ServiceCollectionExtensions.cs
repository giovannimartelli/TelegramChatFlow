using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramChatFlow.Builder;
using TelegramChatFlow.Runtime;

namespace TelegramChatFlow;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra il framework di flussi conversazionali nel container DI.
    /// </summary>
    public static FlowFrameworkBuilder AddFlowFramework(
        this IServiceCollection services,
        Action<FlowBotOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<FlowBotOptions>>();
            return new TelegramBotClient(opts.Value.BotToken);
        });

        services.AddSingleton<ISessionStore, InMemorySessionStore>();
        services.AddSingleton<MessageManager>();
        services.AddSingleton<IReadOnlyList<FlowDefinition>>(sp =>
            sp.GetServices<FlowBase>().Select(f => f.Build()).ToList());
        services.AddSingleton<FlowRegistry>();
        services.AddSingleton<StepRenderer>();
        services.AddSingleton<FlowNavigator>();
        services.AddSingleton<StepInputProcessor>();
        services.AddSingleton<FlowEngine>();
        services.AddSingleton<InactivityWatchdog>();
        services.AddHostedService<BotHostedService>();

        return new FlowFrameworkBuilder(services);
    }
}

/// <summary>
/// Builder restituito da <see cref="ServiceCollectionExtensions.AddFlowFramework"/>
/// per registrare flussi e personalizzare la configurazione.
/// </summary>
public sealed class FlowFrameworkBuilder
{
    private readonly IServiceCollection _services;

    internal FlowFrameworkBuilder(IServiceCollection services) => _services = services;

    /// <summary>Registra un flusso nel framework.</summary>
    public FlowFrameworkBuilder AddFlow<T>() where T : FlowBase
    {
        _services.AddSingleton<FlowBase, T>();
        return this;
    }

    /// <summary>Sostituisce lo store delle sessioni (default: in-memory).</summary>
    public FlowFrameworkBuilder UseSessionStore<T>() where T : class, ISessionStore
    {
        var existing = _services.FirstOrDefault(d => d.ServiceType == typeof(ISessionStore));
        if (existing is not null) _services.Remove(existing);
        _services.AddSingleton<ISessionStore, T>();
        return this;
    }
}
