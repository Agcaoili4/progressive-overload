using Microsoft.AspNetCore.Identity;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Infrastructure.Auth;

public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    /*
        A real hash of a random string, computed once at startup. Verifying against it
        costs the same as verifying a genuine user's password.
    */
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(null!, Guid.NewGuid().ToString());

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(null!, hash, password) is
            PasswordVerificationResult.Success or
            PasswordVerificationResult.SuccessRehashNeeded;

    public bool VerifyDummy(string password)
    {
        _inner.VerifyHashedPassword(null!, DummyHash, password);
        return false;
    }
}
