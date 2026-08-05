import { useState } from 'react';
import { api, ApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { toStorageKg, unitLabel } from '../lib/units';
import type { Units } from '../lib/units';

/* Persisted numeric values — see Domain/Users/{Sex,ExperienceLevel,UnitPreference}.cs */
const SEXES = [
  { value: 1, label: 'Male' },
  { value: 2, label: 'Female' },
] as const;

const LEVELS = [
  { value: 1, label: 'Beginner', hint: 'New to lifting' },
  { value: 2, label: 'Novice', hint: 'Under a year' },
  { value: 3, label: 'Intermediate', hint: 'A few years in' },
  { value: 4, label: 'Advanced', hint: 'Long-term, structured' },
] as const;

const MAX_BIO = 500;
const MIN_KG = 20;
const MAX_KG = 500;

export default function Onboarding() {
  const { profile, token, refreshProfile, completeOnboarding } = useAuth();

  const [units, setUnits] = useState<Units>(1);
  const [weight, setWeight] = useState('');
  const [sex, setSex] = useState<number | null>(null);
  const [level, setLevel] = useState<number | null>(null);
  const [bio, setBio] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const weightNumber = weight.trim() === '' ? null : Number(weight);
  const weightKg = weightNumber === null || Number.isNaN(weightNumber)
    ? null
    : toStorageKg(weightNumber, units);

  const weightInvalid =
    weightKg !== null && (Number.isNaN(weightKg) || weightKg < MIN_KG || weightKg > MAX_KG);

  const save = async (skip: boolean) => {
    const accessToken = token();
    if (!accessToken || !profile) return;

    setBusy(true);
    setError(null);
    try {
      if (!skip) {
        /*
            PATCH /me replaces rather than merges, and the validator requires a non-empty
            DisplayName and a valid Units on every call — so both are always sent, even
            though this screen does not edit the name.
        */
        await api.updateProfile(accessToken, {
          displayName: profile.displayName,
          bio: bio.trim() === '' ? null : bio.trim(),
          sex,
          experienceLevel: level,
          units,
        });

        /* Bodyweight is a separate time series, not a profile field. */
        if (weightKg !== null && !weightInvalid) {
          await api.recordBodyweight(accessToken, weightKg);
        }
      } else {
        /* Even when skipping, keep the unit choice — every later screen reads it. */
        await api.updateProfile(accessToken, {
          displayName: profile.displayName,
          bio: profile.bio,
          sex: profile.sex,
          experienceLevel: profile.experienceLevel,
          units,
        });
      }

      await refreshProfile();
      completeOnboarding();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not save. Please try again.');
    } finally {
      setBusy(false);
    }
  };

  const chip = (active: boolean) =>
    `rounded-xl border px-3 py-2.5 text-left transition ${
      active ? 'border-lime-400 bg-lime-400/10' : 'border-white/10 bg-white/[0.03] active:bg-white/[0.07]'
    }`;

  return (
    <div className="mx-auto flex min-h-[100dvh] max-w-md flex-col px-6 py-10 text-white">
      <p className="text-[10px] font-bold uppercase tracking-wider text-lime-300/70">Step 1 of 1</p>
      <h1 className="mt-1 text-2xl font-extrabold tracking-tight">A few details</h1>
      <p className="mt-1 mb-7 text-sm text-white/40">
        Bodyweight and sex are what make strength comparable across people. All of this can be
        changed later, and you can skip it now.
      </p>

      <div className="space-y-6">
        <div>
          <p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-white/40">Units</p>
          <div className="grid grid-cols-2 gap-2">
            {([1, 2] as const).map((u) => (
              <button key={u} type="button" onClick={() => setUnits(u)} className={chip(units === u)}>
                <span className="text-sm font-bold">{u === 1 ? 'Metric' : 'Imperial'}</span>
                <span className="ml-1 text-xs text-white/35">({unitLabel(u)})</span>
              </button>
            ))}
          </div>
        </div>

        <div>
          <label htmlFor="bw" className="mb-2 block text-[10px] font-bold uppercase tracking-wider text-white/40">
            Bodyweight <span className="text-white/25">— optional</span>
          </label>
          <div className="relative">
            <input
              id="bw"
              inputMode="decimal"
              placeholder={units === 1 ? 'e.g. 84.5' : 'e.g. 186'}
              value={weight}
              onChange={(e) => setWeight(e.target.value)}
              className="w-full rounded-xl border border-white/10 bg-white/[0.04] px-4 py-3 pr-14 text-white placeholder:text-white/25 outline-none focus:border-lime-400/50"
            />
            <span className="absolute right-4 top-1/2 -translate-y-1/2 text-sm font-bold text-white/30">
              {unitLabel(units)}
            </span>
          </div>
          {weightInvalid && (
            <p className="mt-1.5 text-[12px] text-red-300">
              Enter a weight between {MIN_KG} and {MAX_KG} kg
              {units === 2 ? ` (about ${Math.round(MIN_KG / 0.45359237)}–${Math.round(MAX_KG / 0.45359237)} lb)` : ''}.
            </p>
          )}
          {weightKg !== null && !weightInvalid && units === 2 && (
            <p className="mt-1.5 text-[12px] text-white/30">Stored as {weightKg} kg.</p>
          )}
        </div>

        <div>
          <p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-white/40">
            Sex <span className="text-white/25">— optional, used for relative strength</span>
          </p>
          <div className="grid grid-cols-2 gap-2">
            {SEXES.map((s) => (
              <button key={s.value} type="button" onClick={() => setSex(sex === s.value ? null : s.value)}
                      className={chip(sex === s.value)}>
                <span className="text-sm font-bold">{s.label}</span>
              </button>
            ))}
          </div>
        </div>

        <div>
          <p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-white/40">
            Experience <span className="text-white/25">— optional</span>
          </p>
          <div className="grid grid-cols-2 gap-2">
            {LEVELS.map((l) => (
              <button key={l.value} type="button" onClick={() => setLevel(level === l.value ? null : l.value)}
                      className={chip(level === l.value)}>
                <span className="block text-sm font-bold">{l.label}</span>
                <span className="block text-[11px] text-white/35">{l.hint}</span>
              </button>
            ))}
          </div>
        </div>

        <div>
          <label htmlFor="bio" className="mb-2 block text-[10px] font-bold uppercase tracking-wider text-white/40">
            Bio <span className="text-white/25">— optional</span>
          </label>
          <textarea
            id="bio"
            rows={3}
            maxLength={MAX_BIO}
            placeholder="What are you training for?"
            value={bio}
            onChange={(e) => setBio(e.target.value)}
            className="w-full resize-none rounded-xl border border-white/10 bg-white/[0.04] px-4 py-3 text-white placeholder:text-white/25 outline-none focus:border-lime-400/50"
          />
          <p className="mt-1 text-right text-[11px] text-white/25">{bio.length}/{MAX_BIO}</p>
        </div>
      </div>

      {error && (
        <p role="alert" className="mt-5 rounded-xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-300">
          {error}
        </p>
      )}

      <div className="mt-8 space-y-3">
        <button
          onClick={() => void save(false)}
          disabled={busy || weightInvalid}
          className="w-full rounded-2xl bg-lime-400 py-4 text-lg font-extrabold text-black transition active:scale-[0.98] disabled:opacity-40"
        >
          {busy ? 'Saving…' : 'Save and continue'}
        </button>
        <button
          onClick={() => void save(true)}
          disabled={busy}
          className="w-full py-2 text-sm font-semibold text-white/40 active:text-white/70 disabled:opacity-40"
        >
          Skip for now
        </button>
      </div>
    </div>
  );
}
