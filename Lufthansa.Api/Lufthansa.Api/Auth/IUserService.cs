namespace Lufthansa.Api.Auth;

public interface IUserService
{
    // Return (exists, role, tourOperatorId?)
    (bool ok, string role, Guid? tourOperatorId) ValidateUser(string username, string password);
}