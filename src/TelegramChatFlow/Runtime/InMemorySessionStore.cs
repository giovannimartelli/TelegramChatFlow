using System.Collections.Concurrent;

namespace TelegramChatFlow.Runtime;

/// <summary>
/// Implementazione in-memory dello store di sessioni.
/// Adatta per sviluppo e test; sostituire con un'implementazione persistente per produzione.
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<long, FlowSession> _sessions = new();

    public Task<FlowSession?> GetAsync(long chatId)
    {
        _sessions.TryGetValue(chatId, out var session);
        return Task.FromResult(session);
    }

    public Task SaveAsync(FlowSession session)
    {
        _sessions[session.ChatId] = session;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FlowSession>> GetAllAsync()
    {
        IReadOnlyList<FlowSession> list = _sessions.Values.ToList();
        return Task.FromResult(list);
    }

    public Task DeleteAsync(long chatId)
    {
        _sessions.TryRemove(chatId, out _);
        return Task.CompletedTask;
    }
}
