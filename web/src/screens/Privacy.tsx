import { Link } from 'react-router-dom';

/*
    A factual description of what the code actually stores and transmits, so every line here
    is checkable against the repository. It is deliberately NOT a privacy policy: no
    retention periods, no legal bases, no claims about sale or sharing, because none of those
    are decided in code and inventing them would be worse than saying nothing.
*/
export default function Privacy() {
  const Section = ({ title, children }: { title: string; children: React.ReactNode }) => (
    <section className="mt-6">
      <h2 className="text-[10px] font-bold uppercase tracking-wider text-lime-300/70">{title}</h2>
      <div className="mt-2 space-y-2 text-[13px] leading-relaxed text-white/55">{children}</div>
    </section>
  );

  return (
    <div className="mx-auto min-h-[100dvh] max-w-md px-6 py-10 text-white">
      <Link to="/" className="text-sm font-semibold text-white/40 active:text-white/70">‹ Back</Link>

      <h1 className="mt-5 text-2xl font-extrabold tracking-tight">What this app collects</h1>

      <p className="mt-3 rounded-xl border border-amber-400/25 bg-amber-400/[0.06] px-4 py-3 text-[12px] leading-relaxed text-amber-200/80">
        This is a plain description of what the software stores and sends, accurate to the
        current code. It is not a legal privacy policy — it sets no retention periods and makes
        no commitments about sharing. One written and reviewed properly is needed before this
        is offered to the public.
      </p>

      <Section title="Account">
        <p>Your email address, so you can sign in and be told apart from other lifters.</p>
        <p>
          Your password is never stored. Only a PBKDF2 hash of it is kept, and the hash is
          re-computed with stronger settings when you sign in if the stored one is out of date.
        </p>
        <p>If you use Google sign-in, your Google account identifier and whether Google has verified that email.</p>
      </Section>

      <Section title="Profile">
        <p>Display name, and optionally a bio, your sex, your experience level, and whether you prefer kilograms or pounds.</p>
        <p>
          Sex and bodyweight are optional. They exist because comparing strength between people
          of different sizes is meaningless without them. You can leave both blank and still use
          the app.
        </p>
      </Section>

      <Section title="Bodyweight and training">
        <p>
          Each bodyweight you record is kept with its date, as a history rather than a single
          current value, because progress is the point.
        </p>
        <p>Workouts — exercises, weights and repetitions — will be stored once logging is connected to the server.</p>
      </Section>

      <Section title="Staying signed in">
        <p>
          A session token is held in a cookie your browser will not let JavaScript read, and the
          server stores only a SHA-256 hash of it. Using an old token invalidates the whole
          chain, which is how a stolen session is detected.
        </p>
        <p>The short-lived access token lives only in memory and is gone when you close the tab.</p>
      </Section>

      <Section title="Error reporting">
        <p>
          Crashes are sent to Sentry, a third-party error tracker, to find bugs. Request bodies
          are dropped entirely before sending, so email addresses and passwords from a failed
          sign-in do not leave the server. This was verified by capturing what the software
          actually transmits, not assumed from configuration.
        </p>
        <p>What is sent: the URL, the error and its stack, and the release version.</p>
      </Section>

      <Section title="Not collected">
        <p>No analytics, no advertising identifiers, no location, and no contact list.</p>
      </Section>

      <p className="mt-8 text-[11px] text-white/25">Last updated 4 August 2026.</p>
    </div>
  );
}
