import { forwardRef, useCallback, useEffect, useImperativeHandle, useRef, useState } from 'react';

const ITEM = 52; /* must equal the item height below */

export type WheelHandle = { set: (v: number) => void };

type Props = {
  label: string;
  min: number;
  max: number;
  step: number;
  value: number;
  integer?: boolean;
  onChange: (v: number) => void;
};

const grid = (min: number, max: number, step: number) => {
  const out: number[] = [];
  for (let v = min; v <= max; v = +(v + step).toFixed(2)) out.push(v);
  return out;
};

/*
    A snap-scrolling picker with a typed-entry escape hatch. Flinging beats stepping for the
    jumps that actually happen in a gym — 60 to 225 is a flick, not sixty taps — and typing
    reaches the values the step grid cannot.
*/
const Wheel = forwardRef<WheelHandle, Props>(function Wheel(
  { label, min, max, step, value, integer, onChange }, ref,
) {
  const el = useRef<HTMLDivElement>(null);
  const [values, setValues] = useState(() => grid(min, max, step));
  const [index, setIndex] = useState(() => Math.max(0, grid(min, max, step).indexOf(value)));
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const lastTap = useRef(0);
  const programmatic = useRef(false);

  const place = useCallback((vals: number[], v: number) => {
    const i = vals.indexOf(v);
    if (i < 0 || !el.current) return;
    programmatic.current = true;
    el.current.scrollTop = i * ITEM;
    setIndex(i);
  }, []);

  /*
      A typed value off the grid — 83.75 on a 2.5 wheel — is inserted, never rounded to the
      nearest notch. Silently altering a number the lifter typed would defeat the point of
      letting them type it.
  */
  const set = useCallback((raw: number) => {
    const v = Math.min(max, Math.max(min, integer ? Math.round(raw) : raw));
    setValues((prev) => {
      const next = prev.includes(v) ? prev : [...grid(min, max, step), v].sort((a, b) => a - b);
      queueMicrotask(() => place(next, v));
      return next;
    });
    onChange(v);
  }, [min, max, step, integer, onChange, place]);

  useImperativeHandle(ref, () => ({ set }), [set]);

  useEffect(() => { place(values, value); /* initial position only */ // eslint-disable-next-line
  }, []);

  const onScroll = () => {
    if (!el.current) return;
    if (programmatic.current) { programmatic.current = false; return; }
    const i = Math.max(0, Math.min(values.length - 1, Math.round(el.current.scrollTop / ITEM)));
    if (i !== index) { setIndex(i); onChange(values[i]); }
  };

  const openEditor = () => { setDraft(String(values[index] ?? value)); setEditing(true); };

  const commit = (save: boolean) => {
    setEditing(false);
    const n = parseFloat(draft);
    if (save && !Number.isNaN(n)) set(n);
  };

  return (
    <div className="relative rounded-xl border border-white/5 bg-black/40 pt-1"
         onDoubleClick={openEditor}
         onTouchEnd={() => {
           const now = Date.now();
           if (now - lastTap.current < 320) openEditor();
           lastTap.current = now;
         }}>
      <p className="text-center text-[10px] font-bold uppercase tracking-wider text-white/40">{label}</p>

      <div className="pointer-events-none absolute inset-x-2 top-1/2 z-10 h-[52px] -translate-y-1/2 rounded-lg border-y border-lime-400/25 bg-lime-400/[0.04]" />

      <div ref={el} onScroll={onScroll} className={`wheel tnum ${editing ? 'opacity-25' : ''}`}>
        <div style={{ height: ITEM }} />
        {values.map((v, i) => (
          <div key={v} style={{ height: ITEM }}
               className={`flex snap-center items-center justify-center font-extrabold transition-[color,font-size] duration-100 ${
                 i === index ? 'text-[2.5rem] tracking-tight text-white' : 'text-[1.375rem] text-white/25'
               }`}>
            {v}
          </div>
        ))}
        <div style={{ height: ITEM }} />
      </div>

      {editing && (
        <input autoFocus inputMode={integer ? 'numeric' : 'decimal'} enterKeyHint="done"
               aria-label={`Type ${label}`}
               className="tnum absolute inset-x-2 top-1/2 z-20 h-[52px] w-[calc(100%-16px)] -translate-y-1/2 rounded-lg border-2 border-lime-400 bg-[#0a0a0a] text-center text-[2.5rem] font-extrabold tracking-tight text-white outline-none"
               value={draft}
               onChange={(e) => setDraft(e.target.value)}
               onBlur={() => commit(true)}
               onKeyDown={(e) => {
                 if (e.key === 'Enter') commit(true);
                 if (e.key === 'Escape') commit(false);
               }} />
      )}
    </div>
  );
});

export default Wheel;
