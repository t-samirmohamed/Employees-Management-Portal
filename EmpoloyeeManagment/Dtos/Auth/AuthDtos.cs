namespace EmpoloyeeManagment.Dtos.Auth;

public record SignupRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTime ExpiresAtUtc);
