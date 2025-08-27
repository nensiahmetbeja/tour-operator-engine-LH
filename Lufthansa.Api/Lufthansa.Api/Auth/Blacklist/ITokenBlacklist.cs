namespace Lufthansa.Api.Auth.Blacklist;

public interface ITokenBlacklist
{
    Task BanAsync(string jti, TimeSpan ttl);
    Task<bool> IsBannedAsync(string jti);
}