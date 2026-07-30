using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Domain.Common;

/*
    All weights are stored canonically as decimal kilograms. A user's UnitPreference is a
    display setting. Most conversion happens in the web client, but the server generates the
    weekly recap email, which must show weights in the recipient's preferred unit — so the
    server needs this too.
*/
public static class WeightConversion
{
    /*
        One pound is exactly 0.45359237 kg by the international yard-and-pound agreement.
        Both directions derive from this single constant so they can never disagree with
        each other.
    */
    private const decimal KilogramsPerPound = 0.45359237m;

    public static decimal ToPounds(decimal kilograms) => kilograms / KilogramsPerPound;

    public static decimal ToKilograms(decimal pounds) => pounds * KilogramsPerPound;

    /*
        Imperial snaps to the nearest 0.5 lb; metric just rounds to a sane display
        precision. A lifter who enters 225 lb has it stored as 102.06 kg, and converting
        straight back gives 224.9868 lb - showing "224.99 lb" for a lift they know was 225
        reads as a bug. Lifters think in plate increments, so snapping to the nearest half
        pound restores the number they actually lifted while still representing
        micro-plate loads like 227.5 lb. Both branches round AwayFromZero: the .NET
        default (banker's rounding) would round a value like 0.25 to 0.0, which would
        surprise everyone.
    */
    public static decimal ForDisplay(decimal kilograms, UnitPreference units) => units switch
    {
        UnitPreference.Metric => Math.Round(kilograms, 1, MidpointRounding.AwayFromZero),
        UnitPreference.Imperial => Math.Round(ToPounds(kilograms) * 2m, 0, MidpointRounding.AwayFromZero) / 2m,
        _ => throw new ArgumentOutOfRangeException(nameof(units), units, null)
    };
}
