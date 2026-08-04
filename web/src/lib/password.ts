/*
    Mirrors RegisterValidator exactly: length is the only rule, 12 to 256 characters.

    Deliberately no composition requirements. The server's own comment explains why —
    demanding a symbol and a digit produces "Password1!" and nothing else — and NIST
    SP 800-63B drops them for that reason. So this module keeps two things apart:

      requirements — what the API will actually reject. Blocks submission.
      suggestions  — what genuinely makes a password harder to guess. Advisory only.

    Rendering an advisory item as a red requirement would misstate what the API does and
    would push people toward the weak-but-compliant passwords NIST warns about.
*/
export const MIN_LENGTH = 12;
export const MAX_LENGTH = 256;

export type Requirement = { id: string; label: string; met: boolean };
export type Strength = { score: 0 | 1 | 2 | 3 | 4; label: string; suggestions: string[] };

export function requirements(pw: string): Requirement[] {
  return [
    { id: 'len', label: `At least ${MIN_LENGTH} characters`, met: pw.length >= MIN_LENGTH },
    { id: 'max', label: `No more than ${MAX_LENGTH} characters`, met: pw.length <= MAX_LENGTH },
  ];
}

export const meetsRequirements = (pw: string) => requirements(pw).every((r) => r.met);

const classCount = (pw: string) =>
  [/[a-z]/, /[A-Z]/, /[0-9]/, /[^A-Za-z0-9]/].filter((re) => re.test(pw)).length;

/* A single repeated character, or a short block repeated to pad out the length. */
const isRepetitive = (pw: string) => {
  if (/^(.)\1+$/.test(pw)) return true;
  for (let n = 2; n <= 4; n++) {
    const unit = pw.slice(0, n);
    if (unit.length === n && unit.repeat(Math.ceil(pw.length / n)).startsWith(pw)) return true;
  }
  return false;
};

/* Runs like abcdefgh or 12345678, in either direction. */
const hasLongRun = (pw: string) => {
  let ascending = 1;
  let descending = 1;
  for (let i = 1; i < pw.length; i++) {
    const delta = pw.charCodeAt(i) - pw.charCodeAt(i - 1);
    ascending = delta === 1 ? ascending + 1 : 1;
    descending = delta === -1 ? descending + 1 : 1;
    if (ascending >= 6 || descending >= 6) return true;
  }
  return false;
};

/*
    Length dominates, which is the entire point of the NIST position. Variety adds a little;
    detectable structure takes a lot away. The 12-character floor already excludes most of
    what appears on leaked-password lists, so pattern detection earns more here than a
    bundled wordlist would.
*/
export function strength(pw: string): Strength {
  if (pw.length === 0) return { score: 0, label: '', suggestions: [] };
  if (pw.length < MIN_LENGTH) return { score: 0, label: 'Too short', suggestions: [] };

  let score = 1;
  if (pw.length >= 16) score += 1;
  if (pw.length >= 20) score += 1;
  if (classCount(pw) >= 3) score += 1;

  const suggestions: string[] = [];

  if (isRepetitive(pw)) {
    score = 1;
    suggestions.push('Avoid repeating the same characters');
  }
  if (hasLongRun(pw)) {
    score = Math.min(score, 1);
    suggestions.push('Avoid runs like 123456 or abcdef');
  }
  if (pw.length < 16) suggestions.push('Longer beats complicated — a memorable phrase works well');
  if (classCount(pw) < 2 && pw.length < 20) suggestions.push('Mixing in another kind of character helps');

  const clamped = Math.max(1, Math.min(4, score)) as Strength['score'];
  return {
    score: clamped,
    label: ['', 'Weak', 'Fair', 'Strong', 'Excellent'][clamped],
    suggestions,
  };
}
