using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;
using Shouldly;

namespace ProgressiveOverload.Domain.Tests.Common;

public class WeightConversionTests
{
    [Fact]
    public void RoundTrip_225Pounds_DisplaysAsExactly225()
    {
        // Simulates: user enters 225 lb, it's stored as decimal(7,2) kg, then displayed back.
        var storedKilograms = Math.Round(WeightConversion.ToKilograms(225m), 2, MidpointRounding.AwayFromZero);

        WeightConversion.ForDisplay(storedKilograms, UnitPreference.Imperial).ShouldBe(225.0m);
    }

    [Fact]
    public void RoundTrip_227point5Pounds_DisplaysAsExactly227point5()
    {
        var storedKilograms = Math.Round(WeightConversion.ToKilograms(227.5m), 2, MidpointRounding.AwayFromZero);

        WeightConversion.ForDisplay(storedKilograms, UnitPreference.Imperial).ShouldBe(227.5m);
    }

    [Fact]
    public void ForDisplay_Metric_ReturnsValueAsEntered()
    {
        WeightConversion.ForDisplay(102.5m, UnitPreference.Metric).ShouldBe(102.5m);
    }

    [Fact]
    public void ForDisplay_Metric_RoundsToOneDecimalPlace()
    {
        WeightConversion.ForDisplay(102.06m, UnitPreference.Metric).ShouldBe(102.1m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50.5)]
    [InlineData(102.06)]
    [InlineData(500)]
    public void ToPoundsAndToKilograms_AreExactInverses(decimal kilograms)
    {
        var roundTripped = WeightConversion.ToKilograms(WeightConversion.ToPounds(kilograms));

        roundTripped.ShouldBe(kilograms, 0.0000001m);
    }

    [Fact]
    public void ForDisplay_Imperial_MidpointRoundsAwayFromZero_NotToEven()
    {
        // 3.25 lb sits exactly halfway between the 3.0 and 3.5 lb display buckets.
        // Banker's rounding (the .NET default) would round 6.5 half-steps to the even
        // neighbour and land on 3.0; AwayFromZero must land on 3.5 instead.
        var kilogramsForExactly3point25Pounds = WeightConversion.ToKilograms(3.25m);

        WeightConversion.ForDisplay(kilogramsForExactly3point25Pounds, UnitPreference.Imperial).ShouldBe(3.5m);
    }

    [Fact]
    public void ToPounds_Zero_IsZero()
    {
        WeightConversion.ToPounds(0m).ShouldBe(0m);
    }

    [Fact]
    public void ToKilograms_Zero_IsZero()
    {
        WeightConversion.ToKilograms(0m).ShouldBe(0m);
    }
}
