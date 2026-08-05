import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { useSession } from '../session/SessionContext';

const LAST = { name: 'Push Day A', exercises: ['Bench Press', 'Overhead Press', 'Dips'] };

export default function Today() {
  const { profile, logout } = useAuth();
  const { session, start } = useSession();
  const nav = useNavigate();

  const begin = (name: string, exercises: string[]) => { start(name, exercises); nav('/session'); };

  return (
    <div className="mx-auto flex h-[100dvh] max-w-md flex-col text-white">
      <header className="flex flex-none items-center gap-3 px-4 pb-2 pt-4">
        <div className="min-w-0 flex-1">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-white/40">
            {new Date().toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' })}
          </p>
          <h1 className="text-2xl font-extrabold leading-tight">Today</h1>
        </div>
        <button onClick={logout}
                className="flex h-10 items-center rounded-full bg-white/5 px-3 text-xs font-bold text-white/70 active:bg-white/10">
          Sign out
        </button>
      </header>

      <main className="min-h-0 flex-1 overflow-y-auto px-4 pb-6">
        <p className="mb-4 text-sm text-white/40">
          Signed in as <span className="font-semibold text-white/70">{profile?.displayName}</span>
        </p>

        {session && (
          <button onClick={() => nav('/session')}
                  className="mb-3 flex w-full items-center gap-3 rounded-2xl border border-lime-400/30 bg-lime-400/10 px-4 py-3 text-left">
            <span className="flex-1 text-[15px] font-bold">{session.name} in progress</span>
            <span className="text-xs font-bold text-lime-300">Resume →</span>
          </button>
        )}

        <section className="overflow-hidden rounded-3xl border border-lime-400/20 bg-gradient-to-b from-lime-400/[0.08] to-lime-400/[0.02]">
          <div className="px-5 pt-5">
            <p className="text-[10px] font-bold uppercase tracking-wider text-lime-300/70">Repeat your last</p>
            <div className="mt-1 flex items-baseline gap-2">
              <h2 className="text-2xl font-extrabold leading-tight">{LAST.name}</h2>
              <span className="text-[13px] font-medium text-white/35">3 days ago</span>
            </div>
            <div className="mt-3 flex flex-wrap gap-1.5">
              {LAST.exercises.map((e) => (
                <span key={e} className="rounded-full bg-white/[0.06] px-2.5 py-1 text-[11px] font-semibold text-white/55">{e}</span>
              ))}
            </div>
          </div>
          <div className="px-5 pb-5 pt-4">
            <button onClick={() => begin(LAST.name, LAST.exercises)}
                    className="w-full rounded-2xl bg-lime-400 py-4 text-lg font-extrabold tracking-tight text-black active:scale-[0.98]">
              Repeat this session
            </button>
          </div>
        </section>

        <button onClick={() => begin('New session', ['Bench Press'])}
                className="mt-3 flex w-full items-center justify-center gap-2 rounded-2xl border border-white/10 bg-white/[0.03] py-3.5 text-[15px] font-bold text-white/70 active:bg-white/[0.07]">
          + Start empty session
        </button>

        <div className="mt-6 rounded-2xl border border-amber-400/20 bg-amber-400/[0.05] px-4 py-3">
          <p className="text-[12px] leading-relaxed text-amber-200/70">
            Auth and profile are live against the API. Logging is local only — Milestone 2 has not
            built the workouts endpoints, so sessions vanish on reload.
          </p>
        </div>
      </main>
    </div>
  );
}
