using FactoryPlatform.Application.Users;

namespace FactoryPlatform.Application.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string Token, UserMeDto User);
