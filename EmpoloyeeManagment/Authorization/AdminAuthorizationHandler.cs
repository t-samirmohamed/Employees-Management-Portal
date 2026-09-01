using Microsoft.AspNetCore.Authorization;
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Authorization;

public class AdminAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.User.IsInRole(nameof(Role.Admin)))
        {
            foreach (var requirement in context.PendingRequirements.ToList())
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
