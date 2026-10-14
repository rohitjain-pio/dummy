// UI-005 "live email-format and password-policy validation" — mirrors the server rules so the user
// hears about a problem before submitting; the server stays authoritative (ERR-201 / ERR-901).
export const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export const emailError = (value: string) => (value && !EMAIL_PATTERN.test(value) ? 'Enter a valid email address, like name@school.edu.' : undefined);

/** VAL-005 / CFG-001: at least 10 characters using at least 3 of upper, lower, digit, symbol (blocklist is server-side). */
export function passwordError(value: string): string | undefined {
  if (!value) return undefined;
  if (value.length < 10) return 'Use at least 10 characters.';
  const classes = [/\p{Lu}/u, /\p{Ll}/u, /\p{Nd}/u, /[^\p{L}\p{Nd}]/u].filter((r) => r.test(value)).length;
  if (classes < 3) return 'Use at least three of: uppercase letters, lowercase letters, numbers, symbols.';
  return undefined;
}
