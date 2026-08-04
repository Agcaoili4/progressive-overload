import { useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';

export default function SignIn() {
  const { login, register } = useAuth();
  const [mode, setMode] = useState<'in' | 'up'>('in');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (mode === 'in') await login(email, password);
      else await register(email, password, displayName);
    } catch (err) {
      /*
          The API deliberately returns one message for unknown email and wrong password, so
          it is shown verbatim rather than interpreted — guessing here would leak exactly
          what the server refuses to say.
      */
      setError(err instanceof ApiError ? err.message : 'Something went wrong');
    } finally {
      setBusy(false);
    }
  };

  const field = 'w-full rounded-xl border border-white/10 bg-white/[0.04] px-4 py-3 text-white placeholder:text-white/25 outline-none focus:border-lime-400/50';

  return (
    <div className="mx-auto flex h-[100dvh] max-w-md flex-col justify-center px-6 text-white">
      <h1 className="text-3xl font-extrabold tracking-tight">ProgressiveOverload</h1>
      <p className="mt-1 mb-8 text-sm text-white/40">
        {mode === 'in' ? 'Sign in to keep training.' : 'Create an account to start logging.'}
      </p>

      <form onSubmit={submit} className="space-y-3">
        {mode === 'up' && (
          <input className={field} placeholder="Display name" value={displayName}
                 onChange={(e) => setDisplayName(e.target.value)} required />
        )}
        <input className={field} type="email" autoComplete="email" placeholder="Email"
               value={email} onChange={(e) => setEmail(e.target.value)} required />
        <input className={field} type="password" placeholder="Password"
               autoComplete={mode === 'in' ? 'current-password' : 'new-password'}
               value={password} onChange={(e) => setPassword(e.target.value)} required />

        {error && (
          <p role="alert" className="rounded-xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-300">
            {error}
          </p>
        )}

        <button type="submit" disabled={busy}
                className="w-full rounded-2xl bg-lime-400 py-4 text-lg font-extrabold text-black active:scale-[0.98] disabled:opacity-50">
          {busy ? 'Working…' : mode === 'in' ? 'Sign in' : 'Create account'}
        </button>
      </form>

      <button onClick={() => { setMode(mode === 'in' ? 'up' : 'in'); setError(null); }}
              className="mt-6 text-sm font-semibold text-white/45 active:text-white/70">
        {mode === 'in' ? 'No account? Create one' : 'Already have an account? Sign in'}
      </button>

      {/* Passwords are NIST-style: 12 characters minimum, no composition rules. */}
      {mode === 'up' && <p className="mt-3 text-xs text-white/25">Minimum 12 characters.</p>}
    </div>
  );
}
