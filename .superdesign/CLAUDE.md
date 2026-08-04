# Superdesign working directory

Claude Code runs here only when the Superdesign VS Code extension spawns it. That invocation is
`claude -p`: one shot, no second turn, no way for the user to answer anything.

## Never ask a clarifying question

A question ends the turn and the user sees an empty result — the extension cannot reply. When a
brief is ambiguous or larger than one sitting, choose and build. State the choice in one line at
the top of the response, then produce the work.

Scope rule when a brief covers several screens: build the single highest-value screen, three
variations, unless the brief names one. The remaining screens wait for the next prompt.

## Output

HTML files go in `design_iterations/`, one screen per file, Tailwind via CDN. Mockups only — this
directory holds design exploration, never application source.

## The repository rules do not apply here

The root `CLAUDE.md` governs the ProgressiveOverload .NET backend: C# architecture layering,
`decimal` weights, comment style, EF migrations. None of it constrains these standalone
HTML/Tailwind mockups. Ignore it while working in this directory.
