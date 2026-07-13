using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, string securityStamp);
}
