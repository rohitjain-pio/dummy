import { describe, expect, it } from 'vitest';
import type { AttendanceRecord } from '@/lib/types';
import { daysAgo, rowState } from './Attendance';

const record = (attendanceDate: string, extra: Partial<AttendanceRecord> = {}): AttendanceRecord => ({
  id: 'r', personId: 'p', personType: 'Student', attendanceDate, outcome: 'Absent', recordedByUserId: 'u', recordedAtUtc: '2026-09-14T06:00:00',
  correctionStatus: 'None', locked: false, ...extra,
});

// UI-010 correction/lock states — BRL FR-033/034/035, Q-214 (day 30 → locked).
describe('attendance row states', () => {
  const today = '2026-09-14';
  it('day 0 is a direct edit', () => expect(rowState(record('2026-09-14'), today)).toBe('editable'));
  it('day 1 needs a correction request', () => expect(rowState(record('2026-09-13'), today)).toBe('request-correction'));
  it('day 29 still needs a correction request', () => expect(rowState(record('2026-08-16'), today)).toBe('request-correction'));
  it('day 30 is locked — no toggle or request action', () => expect(rowState(record('2026-08-15'), today)).toBe('locked'));
  it('a server-locked row is locked whatever its age', () => expect(rowState(record('2026-09-13', { locked: true }), today)).toBe('locked'));
  it('a pending correction shows the pending badge', () => expect(rowState(record('2026-09-10', { correctionStatus: 'CorrectionPending' }), today)).toBe('pending'));
  it('day counting crosses month and DST-free boundaries exactly', () => {
    expect(daysAgo('2026-02-28', '2026-03-01')).toBe(1);
    expect(daysAgo('2026-10-24', '2026-10-26')).toBe(2);
  });
});
