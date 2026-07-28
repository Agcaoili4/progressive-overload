using ProgressiveOverload.Domain.Common;
using Shouldly;

namespace ProgressiveOverload.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();
        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_CarriesTheError()
    {
        var error = new Error("users.email_taken", "That email is already registered.");
        var result = Result.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void AccessingValueOnFailure_Throws()
    {
        var result = Result<int>.Failure(new Error("x", "y"));
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }
}
