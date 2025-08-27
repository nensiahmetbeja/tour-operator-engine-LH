namespace Lufthansa.Api.Auth;

public class InMemoryUserService : IUserService
{
    // demo users: to do with DB/Identity later
    private static readonly Dictionary<string,(string Password, string Role, Guid? TourOpId)> Users = new()
    {
        ["admin"] = ("admin123", "Admin", null),
        ["op1"]   = ("op123",   "TourOperator", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), // sample guid
    };

    public (bool ok, string role, Guid? tourOperatorId) ValidateUser(string username, string password)
    {
        if (Users.TryGetValue(username, out var u) && u.Password == password)
            return (true, u.Role, u.TourOpId);
        return (false, "", null);
    }
}