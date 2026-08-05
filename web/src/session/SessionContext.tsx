import { createContext, useContext, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { v7 as uuidv7 } from 'uuid';

/*
    LOCAL ONLY. The logging API (PUT /workouts/sessions/{id}) is Milestone 2 and does not
    exist yet, so a session lives in memory and is lost on reload. Ids are already
    client-generated UUIDv7 as the sync contract requires, so wiring this to the server later
    is a matter of adding the call, not reshaping the data.
*/
export type WorkoutSet = { id: string; weightKg: number; reps: number; loggedAt: string };
export type Exercise = { id: string; name: string; sets: WorkoutSet[] };
export type Session = { id: string; name: string; startedAt: string; exercises: Exercise[] };

type Api = {
  session: Session | null;
  start: (name: string, exerciseNames: string[]) => void;
  finish: () => void;
  logSet: (exerciseId: string, weightKg: number, reps: number) => void;
};

const Ctx = createContext<Api | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);

  const value = useMemo<Api>(() => ({
    session,
    start: (name, exerciseNames) =>
      setSession({
        id: uuidv7(),
        name,
        startedAt: new Date().toISOString(),
        exercises: exerciseNames.map((n) => ({ id: uuidv7(), name: n, sets: [] })),
      }),
    finish: () => setSession(null),
    logSet: (exerciseId, weightKg, reps) =>
      setSession((s) =>
        s === null ? s : {
          ...s,
          exercises: s.exercises.map((ex) =>
            ex.id !== exerciseId ? ex : {
              ...ex,
              sets: [...ex.sets, { id: uuidv7(), weightKg, reps, loggedAt: new Date().toISOString() }],
            }),
        }),
  }), [session]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useSession() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useSession must be used inside SessionProvider');
  return ctx;
}
