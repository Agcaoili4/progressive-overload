using ProgressiveOverload.Domain.Auth;
using Shouldly;

namespace ProgressiveOverload.Domain.Tests.Auth;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private static RefreshToken AToken(Guid? familyId = null) =>
        RefreshToken.Issue(Guid.CreateVersion7(), "hash", Now, Lifetime, familyId);

    [Fact]
    public void Issue_StartsANewFamilyWhenNoneGiven()
    {
        var token = AToken();
        token.FamilyId.ShouldNotBe(Guid.Empty);
        token.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Issue_InheritsTheFamilyWhenRotating()
    {
        var family = Guid.CreateVersion7();
        AToken(family).FamilyId.ShouldBe(family);
    }

    [Fact]
    public void Redeem_SucceedsOnceAndMarksTheToken()
    {
        var token = AToken();

        token.Redeem(Now).IsSuccess.ShouldBeTrue();
        token.RedeemedAt.ShouldBe(Now);
        token.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Redeem_SecondTimeIsReuse()
    {
        var token = AToken();
        token.Redeem(Now);

        var result = token.Redeem(Now.AddMinutes(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.RefreshTokenReused);
    }

    [Fact]
    public void Redeem_AfterExpiryFails()
    {
        var token = AToken();
        var result = token.Redeem(Now + Lifetime + TimeSpan.FromSeconds(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.RefreshTokenExpired);
    }

    /*
        Redeem's revoked branch is covered by RefreshRotationTests instead. Revocation only
        ever happens as SQL (ExecuteUpdateAsync), so there is no domain-level way to reach
        that state, and the discarded unit test could only reach it through a domain method
        production never called.
    */
}
