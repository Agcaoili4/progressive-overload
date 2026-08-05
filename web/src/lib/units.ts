/*
    Mirrors Domain/Common/WeightConversion.cs. Two implementations of one rule can drift, and
    drift here shows up as personal records flickering months later — so the constant and the
    rounding behaviour are copied exactly, and unitVectors below is the shared case list both
    sides must agree on.
*/
export const KG_PER_LB = 0.45359237;

export type Units = 1 | 2; // 1 = Metric, 2 = Imperial — persisted numeric values.

export const toPounds = (kg: number) => kg / KG_PER_LB;
export const toKilograms = (lb: number) => lb * KG_PER_LB;

/* Math.round is half-up for positives, which matches .NET's AwayFromZero for our range. */
const round = (n: number, dp: number) => {
  const f = 10 ** dp;
  return Math.round(n * f) / f;
};

/*
    Imperial snaps to the nearest 0.5 lb. A lifter who entered 225 lb has it stored as
    102.06 kg; converting straight back gives 224.9868, and showing "224.99" for a lift they
    know was 225 reads as a bug.
*/
export const forDisplay = (kg: number, units: Units) =>
  units === 2 ? Math.round(toPounds(kg) * 2) / 2 : round(kg, 1);

/* Weight leaves the client in kilograms only — the API accepts nothing else. */
export const toStorageKg = (value: number, units: Units) =>
  round(units === 2 ? toKilograms(value) : value, 2);

export const unitLabel = (units: Units) => (units === 2 ? 'lb' : 'kg');

/* Cases both implementations must agree on. Kept beside the code that could drift. */
export const unitVectors = [
  { kg: 102.06, imperial: 225, metric: 102.1 },
  { kg: 100, imperial: 220.5, metric: 100 },
  { kg: 84.5, imperial: 186.5, metric: 84.5 },
];
