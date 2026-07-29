using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using ProgressiveOverload.Application.Abstractions;

namespace ProgressiveOverload.Infrastructure.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            // Both claim types are checked because ASP.NET's default inbound claim mapping
            // rewrites "sub" to ClaimTypes.NameIdentifier unless that mapping is disabled —
            // reading only one of them produces an intermittently null user depending on
            // whether the mapping is active, which is a miserable bug to chase.
            var value = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
