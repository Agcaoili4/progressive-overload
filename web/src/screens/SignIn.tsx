import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { MIN_LENGTH, meetsRequirements, requirements, strength } from '../lib/password';

const MAX_DISPLAY_NAME = 30; /* User.MaxDisplayNameLength */

export default function SignIn() {
  const { login, register } = useAuth();
  const [mode, setMode] = useState<'in' | 'up'>('in');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  /* Switching mode clears everything: a half-typed sign-in should not bleed into sign-up. */
  const switchMode = () => {
    setMode(mode === 'in' ? 'up' : 'in');
    setEmail('');
    setPassword('');
    setConfirmPassword('');
    setDisplayName('');
    setShowPassword(false);
    setError(null);
  };

  const reqs = requirements(password);
  const { score, label, suggestions } = strength(password);
  const signingUp = mode === 'up';

  const passwordsMatch = password !== '' && confirmPassword === password;
  /* Only complain once they have actually typed something to compare. */
  const showMismatch = signingUp && confirmPassword !== '' && !passwordsMatch;

  const canSubmit =
    !busy &&
    email.trim() !== '' &&
    password !== '' &&
    (!signingUp || (displayName.trim() !== '' && meetsRequirements(password) && passwordsMatch));

  const submit = async () => {
    if (!canSubmit) return;
    setBusy(true);
    setError(null);
    try {
      if (signingUp) await register(email.trim(), password, displayName.trim());
      else await login(email.trim(), password);
    } catch (err) {
      /*
          Shown verbatim. Unknown email and wrong password deliberately return the same
          message, so interpreting it here would leak exactly what the server refuses to say.
      */
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.');
    } finally {
      setBusy(false);
    }
  };

  const field =
    'w-full rounded-xl border border-white/10 bg-white/[0.04] px-4 py-3 text-white placeholder:text-white/25 outline-none focus:border-lime-400/50';

  const barColour = ['', 'bg-red-400', 'bg-amber-400', 'bg-lime-400', 'bg-lime-300'][score];

  return (
    <div className="mx-auto flex min-h-[100dvh] max-w-md flex-col justify-center px-6 py-10 text-white">
      <h1 className="text-3xl font-extrabold tracking-tight">ProgressiveOverload</h1>
      <p className="mt-1 mb-8 text-sm text-white/40">
        {signingUp ? 'Create an account to start logging.' : 'Sign in to keep training.'}
      </p>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void submit();
        }}
        className="space-y-3"
        noValidate
      >
        {signingUp && (
          <div>
            <input
              className={field}
              placeholder="Display name"
              autoComplete="nickname"
              maxLength={MAX_DISPLAY_NAME}
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
            <p className="mt-1 px-1 text-right text-[11px] text-white/25">
              {displayName.length}/{MAX_DISPLAY_NAME}
            </p>
          </div>
        )}

        <input
          className={field}
          type="email"
          inputMode="email"
          autoComplete="email"
          placeholder="Email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />

        <div className="relative">
          <input
            className={`${field} pr-16`}
            type={showPassword ? 'text' : 'password'}
            autoComplete={signingUp ? 'new-password' : 'current-password'}
            placeholder="Password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <button
            type="button"
            onClick={() => setShowPassword(!showPassword)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-xs font-bold text-white/40 active:text-white/70"
          >
            {showPassword ? 'Hide' : 'Show'}
          </button>
        </div>

        {/*
            Sign-up only. On sign-in the rules are whatever they were when the account was
            made, so grading an existing password would be noise at best and wrong at worst.
        */}
        {signingUp && password !== '' && (
          <div className="rounded-xl border border-white/10 bg-white/[0.02] px-4 py-3">
            <div className="flex items-center gap-3">
              <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-white/10">
                <div
                  className={`h-full rounded-full transition-all ${barColour}`}
                  style={{ width: `${(score / 4) * 100}%` }}
                />
              </div>
              <span className="w-16 text-right text-[11px] font-bold text-white/50">
                {password.length < MIN_LENGTH ? 'Too short' : label}
              </span>
            </div>

            <ul className="mt-3 space-y-1.5">
              {reqs.map((r) => (
                <li key={r.id} className="flex items-center gap-2 text-[13px]">
                  <span
                    aria-hidden
                    className={`flex h-4 w-4 flex-none items-center justify-center rounded-full text-[10px] font-bold ${
                      r.met ? 'bg-lime-400 text-black' : 'bg-white/10 text-white/40'
                    }`}
                  >
                    {r.met ? '✓' : ''}
                  </span>
                  <span className={r.met ? 'text-white/60' : 'text-white/40'}>{r.label}</span>
                  <span className="sr-only">{r.met ? 'met' : 'not met'}</span>
                </li>
              ))}
            </ul>

            {/*
                Advisory, never blocking. The API enforces length only — presenting these as
                requirements would misstate what it does and nudge people toward the
                weak-but-compliant passwords the rule exists to avoid.
            */}
            {suggestions.length > 0 && (
              <div className="mt-3 border-t border-white/[0.07] pt-2.5">
                <p className="text-[10px] font-bold uppercase tracking-wider text-white/30">
                  Optional — makes it stronger
                </p>
                <ul className="mt-1.5 space-y-1">
                  {suggestions.map((s) => (
                    <li key={s} className="flex gap-2 text-[12px] text-white/40">
                      <span aria-hidden>·</span>
                      <span>{s}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}

        {/*
            A typo in a password nobody can read costs an account recovery, so it is confirmed
            before the account exists. Sign-in has nothing to confirm against.
        */}
        {signingUp && (
          <div>
            <div className="relative">
              <input
                className={`${field} pr-10 ${showMismatch ? 'border-red-500/50' : ''}`}
                type={showPassword ? 'text' : 'password'}
                autoComplete="new-password"
                placeholder="Re-type password"
                aria-invalid={showMismatch}
                aria-describedby="confirm-state"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
              />
              {passwordsMatch && (
                <span
                  aria-hidden
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-sm font-bold text-lime-400"
                >
                  ✓
                </span>
              )}
            </div>

            <p id="confirm-state" role={showMismatch ? 'alert' : undefined} className="mt-1.5 px-1 text-[12px]">
              {showMismatch && <span className="text-red-300">Passwords do not match</span>}
              {passwordsMatch && <span className="text-lime-400/70">Passwords match</span>}
            </p>
          </div>
        )}

        {error && (
          <p
            role="alert"
            className="rounded-xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-300"
          >
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={!canSubmit}
          className="w-full rounded-2xl bg-lime-400 py-4 text-lg font-extrabold text-black transition active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-40"
        >
          {busy ? 'Working…' : signingUp ? 'Create account' : 'Sign in'}
        </button>
      </form>

      <button
        onClick={switchMode}
        className="mt-6 text-sm font-semibold text-white/45 active:text-white/70"
      >
        {signingUp ? 'Already have an account? Sign in' : 'No account? Create one'}
      </button>

      <p className="mt-8 text-[11px] leading-relaxed text-white/25">
        By continuing you agree to how this app handles your data, including the bodyweight and
        training information it stores.{' '}
        <Link to="/privacy" className="font-semibold text-white/45 underline underline-offset-2">
          What we collect
        </Link>
      </p>
    </div>
  );
}
