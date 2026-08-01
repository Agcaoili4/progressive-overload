namespace ProgressiveOverload.Application.Abstractions;

/*
    A wrapper rather than a bare string so Verify's two arguments cannot be transposed. Both
    used to be `string`: swapping them compiled cleanly, verified the password as though it
    were the stored hash, and rejected every login with no error anywhere to notice it.
*/
public readonly record struct PasswordHash(string Value);

/*
    Failed is the default value, so a default-initialised result denies access rather than
    granting it. ValidButNeedsRehash means the password is correct but was stored with weaker
    parameters than the hasher now uses — collapsing it into Valid makes those parameters
    impossible to raise for existing accounts.
*/
public enum PasswordVerification
{
    Failed,
    Valid,
    ValidButNeedsRehash
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(PasswordHash hash, string password);

    /*
        Performs a hash verification against a throwaway hash and always returns Failed.
        Called when no user matched, so that a failed login costs the same time whether
        or not the email exists — otherwise response timing reveals which emails are
        registered. Lives behind the port so Application never touches a hashing library.
    */
    PasswordVerification VerifyDummy(string password);
}
