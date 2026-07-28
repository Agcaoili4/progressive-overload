using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Domain.Users;

public static class UserErrors
{
    public static readonly Error EmailRequired =
        new("users.email_required", "An email address is required.");

    public static readonly Error DisplayNameRequired =
        new("users.display_name_required", "A display name is required.");

    public static readonly Error DisplayNameTooLong =
        new("users.display_name_too_long", "Display name must be 30 characters or fewer.");

    public static readonly Error EmailAlreadyRegistered =
        new("users.email_already_registered", "That email is already registered.");

    public static readonly Error GoogleAlreadyLinked =
        new("users.google_already_linked", "This account is already linked to a different Google account.");

    public static readonly Error ImplausibleBodyweight =
        new("users.implausible_bodyweight", "Bodyweight must be between 20 kg and 500 kg.");

    public static readonly Error NotFound =
        new("users.not_found", "User not found.");
}
