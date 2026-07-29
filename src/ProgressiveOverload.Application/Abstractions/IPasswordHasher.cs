namespace ProgressiveOverload.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);

    /*
        Performs a hash verification against a throwaway hash and always returns false.
        Called when no user matched, so that a failed login costs the same time whether
        or not the email exists — otherwise response timing reveals which emails are
        registered. Lives behind the port so Application never touches a hashing library.
    */
    bool VerifyDummy(string password);
}
