import { describe, expect, it } from 'vitest';
import { emailError, passwordError } from './validation';

// Mirrors the server's PasswordPolicy (VAL-005 / CFG-001): ≥10 chars and ≥3 of 4 classes.
describe('live password-policy validation (UI-005)', () => {
  it.each(['Correct-Horse9', 'abcdefghI9', 'ABCDEFGHi!', 'abcdefgh9!'])('accepts %s', (p) => expect(passwordError(p)).toBeUndefined());
  it.each([
    ['short', 'Use at least 10 characters.'],
    ['Abcdefg9!', 'Use at least 10 characters.'], // 9 chars, 4 classes — length still wins
    ['abcdefghij', 'Use at least three'],
    ['abcdefghi9', 'Use at least three'], // 2 classes
  ])('rejects %s', (p, msg) => expect(passwordError(p)).toContain(msg));
  it('says nothing about an empty field (required handles that)', () => expect(passwordError('')).toBeUndefined());
});

describe('email format validation', () => {
  it.each(['jane@school.edu', 'a.b@c.co'])('accepts %s', (e) => expect(emailError(e)).toBeUndefined());
  it.each(['jane', 'jane@', 'jane@school', 'ja ne@school.edu'])('rejects %s', (e) => expect(emailError(e)).toBeDefined());
});
