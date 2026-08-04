# Logging flow — design

**Written:** 2026-08-02
**Status:** approved, not yet implemented
**Depends on:** `2026-07-27-the-loop-design.md` §3 scope, §6 data model, §8 API surface
**Blocked by:** the logging API does not exist yet (Milestone 2)

---

## 1. Context

Workout logging is the data source the competition runs on. Every scoring input — qualifying
sets, e1RM, progression — comes from this one flow, so it is the highest-traffic screen in
the product and the one users touch mid-set with one hand.

Four lifter interviews (`.superpowers/research/`, local only) inform the decisions below:

- **Greg** logs per-set weight and reps inside Athlean-X, because the tracker ships with the
  programme he follows. He described himself as not tracking weights; his screenshot showed
  four deadlift sets logged to the pound. Stated behaviour and real behaviour disagreed.
- **Joanna** records nothing. Memory only, and she deliberately varies intent — pushing,
  holding, or dialling weight back for volume, technique, holds and negatives.
- **Apollo** uses tracked.gg.
- **Isaac** uses Hevy, logging after every set specifically to keep raising intensity.

Three of four already log per-set in an app they like. **The competing product is Hevy**,
which is free and solves logging well: it prefills set count, weight and reps from the last
time an exercise was performed, completes a set in one tap, and starts a rest timer from
that same tap.

**Consequence, stated plainly:** logging friction is not a winnable differentiator. The bar
is "close enough that switching is viable", and the product wins — if it wins — on the
weekly result Hevy has no equivalent for.

---

## 2. Scope

**In:** start a session, add exercises, log sets, finish, review and edit history.

**Out:** lobby, board, weekly score, and every other competition surface. Those are reached
from elsewhere and never appear in the logging path (§4.2).

---

## 3. Decisions

### 3.1 Sessions start from a repeat, not a blank slate

`Today` offers *Start empty session* and a *Repeat your last session* card naming the
previous session and its exercises. Repeating pre-fills the exercise list; weights arrive via
the prefill rules in §3.3.

**Why.** Greg's logging lives inside Athlean-X because the programme comes with it. A repeat
affordance recovers most of that benefit by reading history already stored, with no new
aggregate, no template editor, and no sync surface.

**Rejected — full programme templates.** A new aggregate, new screens, and a direct fight with
Athlean-X on its own ground rather than on competition. Out of v1 scope per the-loop-design §3.

### 3.2 The logging screen shows personal progress only

Per set: e1RM (Epley, per §6 of the parent spec) and a marker when it beats the stored best
for that exercise. No points, no qualifying badges, no lobby standing.

**Validated against the incumbent.** Isaac's Hevy screenshots show exactly this pattern — a
medal on the individual set that earned it, typed by achievement: `1RM` on a 225 lb × 12
bench, `Weight` on a 280 lb chest fly. Per-set, typed, inline. Worth matching that shape
rather than inventing one, and worth carrying the type: "heaviest ever" and "best estimated
1RM" are different achievements and a lifter reads them differently.

**Why.** Every interviewee framed training as self-improvement in their own words — "one up
myself every day", "the best version of me", "push myself as hard as I can". Personal
progress serves that and is independent of how the competition thesis resolves.

**Why not live competition points.** Two reasons. It hard-couples the most-used screen to a
thesis still under test. And under the ≥80%-of-baseline qualifying rule, Joanna's deliberate
technique and volume work scores zero — rendering that as a visible `0 pts` tells a user the
product disapproves of training she chose on purpose, which contradicts product principle 3
("the scoring system must not degrade real training").

### 3.3 Set entry: explicit `(previous)`, not silent auto-fill

The exercise screen shows last time's performance as a reference line. The next-set row starts
empty, with a `(previous)` button that fills weight and reps in one tap. The button draws from
whichever prefill source applies below — the previous set once one exists in this session,
otherwise the last time the exercise was performed. It is hidden only when neither exists,
i.e. the first time a lifter ever performs that exercise.

```
BENCH PRESS                          ▾
last time · 80 kg × 9, 9, 8

  1    80 kg × 10   ✓
  2    80 kg ×  9   ✓   e1RM 104 ▲ best

  next set
  ┌───────────┬────────────┐
  │   — kg    │  — reps    │
  └───────────┴────────────┘
  ┌────────────────────────┐
  │  ⟲  PREVIOUS  80 × 9   │
  └────────────────────────┘
  ┌────────────────────────┐
  │        LOG SET         │
  └────────────────────────┘
```

Prefill sources, in order:

1. **Set 2+ of an exercise** — the previous set in this session.
2. **Set 1 of an exercise** — the last time that exercise was performed, whenever that was.
3. **Repeated session** — the exercise list from the previous session; values then follow 1–2.

**Why explicit rather than auto-filled.** An unchanged set costs two taps here where Hevy's
auto-fill costs one. Bought with that tap: nothing is ever recorded that the lifter did not
actively enter. The numbers are self-reported and feed a competition with no referee
(principle 1), so a value that appears because someone forgot to edit it is worse than a
value that took an extra tap.

**The repeated set is the common case, confirmed.** Isaac's logs show Triceps Pushdown at
70 lb × 10 three times and Crunch at 90 lb × 10 four times. Whole exercises are frequently a
single value repeated, which is what this affordance is for.

### 3.4 The tracker is local and sticky

Session state persists on the device, survives closing the app, and carries values forward
until the lifter changes them. The server copy is a sync target, not the source of truth
during a session.

**Why.** Matches the parent spec's client-heavy decision: offline sync is deferred, but a
local store now makes it an addition later rather than a rewrite.

### 3.5 Rest timer is in scope

Starts from the set-completion tap, duration per exercise, dismissible, disableable.

**Why.** Isaac logs after every set. In Hevy that completion tap and the rest timer are one
gesture. Omitting it leaves the loop visibly incomplete for exactly the user who does this
daily. This reverses an earlier YAGNI call made before the competitive research.

---

## 4. Screens

```
TODAY                          ACTIVE SESSION              EXERCISE

[+ Start empty session]        Upper Push · 24 min         see §3.3

Repeat your last               Bench Press      3 sets
┌──────────────────────┐       Overhead Press   2 sets
│ Upper Push · 3d ago  │       [ + Add exercise ]
│ Bench, OHP, Dips     │
│         [ Repeat → ] │       [ Finish session ]
└──────────────────────┘

Recent
  Legs · 5d ago
```

Also required: an **exercise picker** searching the ~60-item seeded catalogue with recents
first, and a **finish summary** listing what was done and any personal records set.

### 4.1 Editing history

Past sessions are viewable and editable. Edits correct training history but never rewrite a
finalized week — the parent spec's week-boundary rules govern, and the client surfaces the
distinct error code returned after finalization rather than failing silently.

### 4.2 No competition surface

Nothing in this flow links to a lobby or board. Reached from elsewhere in the app.

---

## 5. Data and state

**Client-owned UUIDv7** for the session and every set, generated at creation, per the parent
spec's sync contract. Requires a UUIDv7 generator in TypeScript — `uuid` v9+ provides one.
Not to be hand-rolled.

**Sync** is `PUT /workouts/sessions/{clientGeneratedId}` carrying the whole session, upserted
server-side. Idempotent under retry because the client owns the id.

**A failed push must never lose the session.** The session remains in the local store, retries,
and shows an unobtrusive unsaved marker. The parent spec's position applies directly: silent
data loss is unrecoverable trust damage.

**Bodyweight** is snapshotted onto the session at creation (parent spec §6). If the profile has
no bodyweight, prompt once, non-blocking — DOTS needs it, but a missing value must not prevent
logging a workout.

---

## 6. Units

The API speaks kilograms only, so the **client** converts and applies display rounding: metric
to one decimal, imperial snapped to the nearest 0.5 lb, away from zero.

**Risk: two implementations of one rule.** `Domain/Common/WeightConversion.cs` already does
this server-side for the weekly recap email. A second implementation in TypeScript can drift,
and drift here surfaces as personal records flickering months later.

**Mitigation.** Extract the existing C# test vectors into a shared JSON fixture and run both
suites against it. Cheap now, expensive to retrofit once real data exists.

---

## 7. Out of scope

RPE, supersets, drop and failure set types, per-exercise notes, plate calculator, programme
templates, offline sync, importing from other trackers, and any competition surface.

Each is defensible later. None is needed to find out whether a lifter will log here rather
than in Hevy.

**Drop sets are the weakest of these cuts and were reconsidered.** Isaac's screenshots show
them as his default structure, not an occasional flourish — every working set of weighted
pull-ups is followed by a drop at bodyweight. Kept out of the v1 logging UI because such a
set can still be recorded as an ordinary set, so nothing is lost from the training record;
only the label is missing. The label matters to *scoring*, not to capture, and that decision
is parked in §9 rather than pre-empted here.

---

## 8. Testing

- **Set-entry reducer** holds every prefill rule in §3.3 and is pure — unit-testable with no
  browser.
- **Unit conversion** runs against the shared vectors from §6, in both languages.
- **Sync failure** must be tested by forcing a failed `PUT` and asserting the session survives
  locally and retries. This is the data-loss path, so it gets a deliberate test rather than
  being assumed.
- Component tests for the exercise screen; integration against the real API once M2 lands.

---

## 9. Open risks

**Double-logging churn.** Isaac keeps Hevy or switches; for a period he does both. This is the
most likely reason a user drops off in week two, and no design decision here removes it — the
bet is that the weekly result is worth the switch.

**The competition thesis is unresolved.** Interviews stand at 2 yes, 2 no, one outstanding
against a pre-committed threshold of 3 of 5. Both yeses were pre-existing relationships — a
brother and a friend — which supports the private invite-code lobby but not a public board.
This design is deliberately independent of that outcome: the logging flow ships unchanged
whichever way it lands.

**Nobody churned for lack of a rival.** Across all four interviews, the causes were burnout
and environment. Isaac was already competing with a friend and still lost a week. Whatever
the weekly meet is for, the evidence does not yet show it is what stops people falling off.

**PARKED — bodyweight loads score zero.** Isaac logs weighted pull-up drop sets at 0 lb added
weight: bodyweight reps to failure, the hardest work in his session. Epley on a load of zero
gives an estimated 1RM of zero, so under the qualifying rule (a set's own e1RM must reach 80%
of baseline) every one of those sets is worth nothing — on the exact lift he competes over,
for the one clearly competitive user in the research.

Same failure class as Joanna's deliberate technique work and the same violated principle:
scoring must not degrade real training. It affects every bodyweight or assisted movement,
where the load that matters is the lifter and the weight field does not carry it.

**Not solvable in this flow.** It is a scoring-layer decision needed before Milestone 3 —
fold the session's bodyweight snapshot into the load, or exclude bodyweight movements from
scoring and say so plainly. Logging captures `0` faithfully either way, so this design does
not block on it. Recorded in full in `.superpowers/research/_TALLY.md`.
