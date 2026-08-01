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

    /*
        SuccessRehashNeeded is surfaced rather than folded into Valid. Identity returns it
        when the stored hash used weaker parameters than the current hasher, and login is
        the only moment the plaintext exists to re-hash with — discarding the distinction
        means PBKDF2 parameters can never be raised for an account that already exists.
    */
    public PasswordVerification Verify(PasswordHash hash, string password) =>
        _inner.VerifyHashedPassword(null!, hash.Value, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Valid,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.ValidButNeedsRehash,
            _ => PasswordVerification.Failed
        };

    public PasswordVerification VerifyDummy(string password)
    {
        _inner.VerifyHashedPassword(null!, DummyHash, password);
        return PasswordVerification.Failed;
    }
}
