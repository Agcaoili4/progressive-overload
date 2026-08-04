# Superdesign brief — workout logging flow

Self-contained. Paste into Superdesign as-is. Full reasoning lives in
`2026-08-02-logging-flow-design.md`; this is the buildable summary.

---

## What this is

A mobile-first web app for logging strength-training workouts. **React + Tailwind CSS.**
The lifter uses it standing in a gym, one-handed, between sets, often with chalk or sweat on
their hands. Every screen below is used mid-workout except History.

The competitor is Hevy, which these users already have and like. This does not need to be
prettier than Hevy. It needs to be **fast enough that switching is not a downgrade**.

## Non-negotiable constraints

These come from the use context, not taste. Any visual direction must survive them.

- **One-handed, thumb-reachable.** Primary actions sit in the lower third. Nothing important
  in a top corner.
- **Large tap targets.** 44px minimum, more on the primary action. Sweaty hands miss.
- **No hover-dependent affordances.** Touch only. Nothing may be discoverable by hover.
- **Readable in bad light.** Gyms are either dim or floodlit. High contrast, no thin grey type
  for anything the lifter needs mid-set.
- **Numbers are the interface.** Weights and reps are the content; they should be the largest,
  clearest elements on the screen.
- **Dark mode is not optional** — phone in a dim gym at 6am.

## Screens

### 1. Today (entry point)

- Primary: **Start empty session**.
- **Repeat your last session** card — names the session, how long ago, and its exercises, with
  a Repeat action. This is the most-used path; give it weight.
- A short **Recent** list beneath.

### 2. Active session

- Session name and a running duration.
- List of exercises added so far, each showing its set count. Tapping one opens screen 3.
- **Add exercise** action.
- **Finish session** action.

### 3. Exercise (the important one)

The screen the product lives or dies on. Contains:

- Exercise name, with a way to switch/collapse.
- A **last time** reference line: e.g. `last time · 80 kg × 9, 9, 8`.
- **Completed sets**, numbered, each showing `weight × reps`. A set that set a personal record
  carries a small inline **badge, typed** — `1RM` or `Weight`. Badge sits on the set that
  earned it, not on the exercise.
- **Next-set row**: two fields, weight and reps, starting empty. Tapping a field opens a
  numeric keypad.
- A **`(previous)` button** that fills both fields in one tap from the previous set — or from
  the last time this exercise was performed, if this is the first set. Hidden only when
  neither exists.
- A **Log set** primary button.
- On logging a set, a **rest timer** starts. Visible, dismissible.

Whole exercises are often one value repeated — three sets of `70 lb × 10` is typical — so the
`(previous) → log` path must feel near-instant.

### 4. Exercise picker

Search over a ~60-exercise catalogue. Recently used at the top. Opens from Add exercise.

### 5. Finish summary

What was done, and any personal records set. This is the moment worth celebrating — it is the
only emotional beat in the flow.

### 6. History

List of past sessions, tappable to view and edit. Not used mid-workout; optimise for scanning.

## States to design, not just the happy path

- **Empty:** first ever session, no history, so no repeat card and no `(previous)`.
- **Unsaved:** a sync failure must show an unobtrusive marker. Never a blocking dialog, never
  silent. The lifter must never lose a session.
- **Rest timer running.**
- **PR earned** — the badge appearing.

## Deliberately absent

No competition, points, leaderboards or standings anywhere in this flow. They exist elsewhere
in the product and must not appear here. Also out: RPE, supersets, drop-set marking, notes,
plate calculator.

## Units

Weight displays in the lifter's preference, kg or lb. Both appear in real data — design the
number treatment so a 3-digit lb value (`225 lbs`) and a 1-decimal kg value (`102.5 kg`) both
sit comfortably without the layout shifting.
