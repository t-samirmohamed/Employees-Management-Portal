using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Auth;

public record SignupRequest(string Email, string Password, string FirstName, string LastName, Gender Gender);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTime ExpiresAtUtc);
