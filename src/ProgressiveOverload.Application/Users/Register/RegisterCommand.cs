namespace ProgressiveOverload.Application.Users.Register;

public sealed record RegisterCommand(string Email, string Password, string DisplayName);
