using ProgressiveOverload.Domain.Users;

using Shouldly;

namespace ProgressiveOverload.Domain.Tests.Users;

// Class that houses methods that has the logic for certain areas.
public class UserTests
{
    private static User AValidUser() =>
        User.CreateWithPassword("lifter@example.com", "hash", "Jansen").Value;

    [Fact]
    public void CreateWithPassword_NormalisesEmailToLowercase()
    {
        var user = User.CreateWithPassword("Lifter@Example.COM", "hash", "Jansen").Value;
        user.Email.ShouldBe("lifter@example.com");
    }

    [Fact]
    public void CreateWithPassword_AssignsTimeOrderedId()
    {
        var first = AValidUser();
        var second = AValidUser();
        second.Id.CompareTo(first.Id).ShouldNotBe(0);
        first.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void CreateWithPassword_RejectsBlankDisplayName()
    {
        var result = User.CreateWithPassword("a@b.com", "hash", "   ");
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.DisplayNameRequired);
    }

    [Fact]
    public void CreateFromGoogle_HasNoPasswordHash()
    {
        var user = User.CreateFromGoogle("a@b.com", "google-sub-123", "Jansen").Value;
        user.PasswordHash.ShouldBeNull();
        user.GoogleSubject.ShouldBe("google-sub-123");
    }

    [Fact]
    public void LinkGoogleAccount_FailsIfAlreadyLinkedToDifferentSubject()
    {
        var user = AValidUser();
        user.LinkGoogleAccount("sub-1").IsSuccess.ShouldBeTrue();

        var result = user.LinkGoogleAccount("sub-2");
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.GoogleAlreadyLinked);
    }

    [Fact]
    public void RecordBodyweight_RejectsImplausibleValues()
    {
        var user = AValidUser();
        user.RecordBodyweight(19m, DateTimeOffset.UtcNow).IsFailure.ShouldBeTrue();
        user.RecordBodyweight(501m, DateTimeOffset.UtcNow).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void RecordBodyweight_UpdatesCurrentBodyweight()
    {
        var user = AValidUser();
        var entry = user.RecordBodyweight(84.5m, DateTimeOffset.UtcNow).Value;

        entry.WeightKg.ShouldBe(84.5m);
        user.CurrentBodyweightKg.ShouldBe(84.5m);
    }

    [Fact]
    public void RecordBodyweight_OlderEntryDoesNotOverwriteCurrent()
    {
        var user = AValidUser();
        var now = DateTimeOffset.UtcNow;

        user.RecordBodyweight(84.5m, now);
        user.RecordBodyweight(90m, now.AddDays(-30));

        user.CurrentBodyweightKg.ShouldBe(84.5m);
    }
}
