import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Wheel from '../components/Wheel';
import type { WheelHandle } from '../components/Wheel';
import { useSession } from '../session/SessionContext';

/* Epley, matching the server so the number a lifter sees mid-set is the number stored. */
const e1rm = (w: number, r: number) => (w <= 0 ? 0 : w * (1 + r / 30));

export default function ExerciseScreen() {
  const { session, logSet, finish } = useSession();
  const nav = useNavigate();
  const [current, setCurrent] = useState(0);
  const [weight, setWeight] = useState(60);
  const [reps, setReps] = useState(8);
  const wRef = useRef<WheelHandle>(null);
  const rRef = useRef<WheelHandle>(null);

  if (!session) { nav('/'); return null; }
  const ex = session.exercises[current];
  const last = ex.sets[ex.sets.length - 1];
  const best = Math.max(0, ...ex.sets.map((s) => e1rm(s.weightKg, s.reps)));

  return (
    <div className="mx-auto flex h-[100dvh] max-w-md flex-col text-white">
      <header className="flex flex-none items-center gap-3 border-b border-white/10 px-4 py-2.5">
        <button onClick={() => nav('/')} aria-label="Back"
                className="flex h-10 w-10 flex-none items-center justify-center rounded-full bg-white/5 text-lg active:bg-white/10">‹</button>
        <div className="min-w-0 flex-1">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-white/40">
            {session.name} · {current + 1} of {session.exercises.length}
          </p>
          <h1 className="truncate text-lg font-extrabold leading-tight">{ex.name}</h1>
        </div>
        <button onClick={() => { finish(); nav('/'); }}
                className="flex-none rounded-full bg-white/5 px-3 py-2 text-xs font-bold text-white/60 active:bg-white/10">Finish</button>
      </header>

      <main className="min-h-0 flex-1 overflow-y-auto px-4 pt-3">
        <div className="mb-3 flex gap-2 overflow-x-auto pb-1">
          {session.exercises.map((e, i) => (
            <button key={e.id} onClick={() => setCurrent(i)}
                    className={`flex-none rounded-full px-3 py-1.5 text-[11px] font-bold ${
                      i === current ? 'bg-lime-400 text-black' : 'bg-white/5 text-white/50'}`}>
              {e.name}
            </button>
          ))}
        </div>

        <p className="mb-2 px-1 text-[10px] font-bold uppercase tracking-wider text-white/40">This session</p>

        {ex.sets.length === 0 && <p className="px-1 text-sm text-white/25">No sets yet.</p>}

        <div className="space-y-2 pb-4">
          {ex.sets.map((s, i) => {
            const isBest = e1rm(s.weightKg, s.reps) >= best && best > 0;
            return (
              <div key={s.id}
                   className={`flex items-center gap-3 rounded-xl border px-3 py-2.5 ${
                     isBest ? 'border-lime-400/25 bg-lime-400/[0.06]' : 'border-white/5 bg-white/[0.04]'}`}>
                <span className={`flex h-7 w-7 flex-none items-center justify-center rounded-full text-xs font-bold ${
                  isBest ? 'bg-lime-400/10 text-lime-300' : 'bg-white/5 text-white/50'}`}>{i + 1}</span>
                <p className="tnum text-xl font-extrabold">
                  {s.weightKg}<span className="ml-0.5 text-sm font-semibold text-white/40">kg</span>
                  <span className="mx-1.5 font-medium text-white/25">×</span>{s.reps}
                </p>
                {isBest && (
                  <span className="ml-auto flex-none rounded-full bg-lime-400 px-2 py-1 text-[10px] font-extrabold uppercase tracking-wide text-black">1RM</span>
                )}
              </div>
            );
          })}
        </div>
      </main>

      <section className="flex-none rounded-t-3xl border-t border-white/10 bg-[#141416] px-4 pb-[max(1rem,env(safe-area-inset-bottom))] pt-4">
        <div className="mb-3 grid grid-cols-2 gap-3">
          <Wheel ref={wRef} label="Weight · kg" min={0} max={1000} step={2.5} value={weight} onChange={setWeight} />
          <Wheel ref={rRef} label="Reps" min={1} max={100} step={1} value={reps} integer onChange={setReps} />
        </div>

        <p className="mb-2 text-center text-[10px] font-medium uppercase tracking-wider text-white/25">
          Double-tap a number to type it
        </p>

        <button disabled={!last}
                onClick={() => { if (last) { wRef.current?.set(last.weightKg); rRef.current?.set(last.reps); } }}
                className="mb-3 flex w-full items-center justify-center gap-2 rounded-xl border border-white/5 bg-white/5 py-2.5 text-sm font-bold text-white/60 active:bg-white/10 disabled:opacity-30">
          (previous) <span className="tnum text-white/35">{last ? `${last.weightKg}kg × ${last.reps}` : '—'}</span>
        </button>

        <button onClick={() => logSet(ex.id, weight, reps)}
                className="w-full rounded-2xl bg-lime-400 py-4 text-xl font-extrabold tracking-tight text-black active:scale-[0.98]">
          Log Set <span className="tnum font-bold opacity-60">· {weight}kg × {reps}</span>
        </button>
      </section>
    </div>
  );
}
