using System.Security.Claims;
using EmpoloyeeManagment.Dtos.Auth;
using EmpoloyeeManagment.Models;
using EmpoloyeeManagment.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace EmpoloyeeManagment.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/signup", SignupAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();

        return app;
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> SignupAsync(
        SignupRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var (token, expiresAtUtc) = tokenService.CreateToken(user, securityStamp);
        return TypedResults.Ok(new AuthResponse(token, expiresAtUtc));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return TypedResults.Unauthorized();
        }

        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var (token, expiresAtUtc) = tokenService.CreateToken(user, securityStamp);
        return TypedResults.Ok(new AuthResponse(token, expiresAtUtc));
    }

    private static async Task<Results<NoContent, NotFound>> LogoutAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        await userManager.UpdateSecurityStampAsync(user);
        return TypedResults.NoContent();
    }
}
