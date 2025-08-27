using System.Collections.Concurrent;

namespace Lufthansa.Api.Auth.Blacklist;

public class InMemoryTokenBlacklist : ITokenBlacklist
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _banned = new();

    public Task BanAsync(string jti, TimeSpan ttl)
    {
        _banned[jti] = DateTimeOffset.UtcNow.Add(ttl);
        return Task.CompletedTask;
    }

    public Task<bool> IsBannedAsync(string jti)
    {
        if (_banned.TryGetValue(jti, out var until))
        {
            if (DateTimeOffset.UtcNow <= until) return Task.FromResult(true);
            _banned.TryRemove(jti, out _);
        }
        return Task.FromResult(false);
    }
}