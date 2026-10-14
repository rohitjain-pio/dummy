import { describe, expect, it } from 'vitest';
import { ROLES, type Role } from '@/lib/roles';
import { canAccess, navFor } from './navConfig';

// CT-601 DoD — "Nav model matches 09-security-spec.md §2 exactly". The expected sets below are written
// out by hand from §2's permission matrix (not derived from navConfig), so a drift in either fails here.
const EXPECTED: Record<Role, string[]> = {
  // §2: PlatformOperator — rooms only among institution features; institution dashboard ❌ (platform summary instead)
  PlatformOperator: ['Dashboard', 'Meetings', 'Settings'],
  Administrator: ['Dashboard', 'Classes', 'Teachers', 'Students', 'Attendance', 'Announcements', 'Meetings', 'Schedule', 'Reports', 'Settings'],
  Principal: ['Dashboard', 'Classes', 'Teachers', 'Students', 'Attendance', 'Announcements', 'Meetings', 'Schedule', 'Reports', 'Settings'],
  SchoolInCharge: ['Dashboard', 'Classes', 'Teachers', 'Students', 'Attendance', 'Announcements', 'Meetings', 'Schedule', 'Reports', 'Settings'],
  // §2: master records read-only, attendance own class, announcements create, reports own classes, schedule read — no Teachers directory
  Teacher: ['Dashboard', 'Classes', 'Students', 'Attendance', 'Announcements', 'Meetings', 'Schedule', 'Reports', 'Settings'],
  // §2: no master records, no attendance recording, no reports; schedule read only; own dashboard variant
  Student: ['Dashboard', 'Announcements', 'Meetings', 'Schedule', 'Settings'],
  Parent: ['Dashboard', 'Announcements', 'Meetings', 'Schedule', 'Settings'],
};

describe('nav model (UI-006 / security spec §2)', () => {
  it.each(ROLES)('%s sees exactly its §2 set, in order', (role) => {
    expect(navFor(role).map((i) => i.label)).toEqual(EXPECTED[role]);
  });

  it('never offers Payment, Leave or a role switcher entry', () => {
    const labels = ROLES.flatMap((r) => navFor(r).map((i) => i.label.toLowerCase()));
    expect(labels).not.toContain('payment');
    expect(labels).not.toContain('leave');
    expect(labels.some((l) => l.includes('switch'))).toBe(false);
  });

  it.each([
    ['Student', '/dashboard/students'],
    ['Parent', '/dashboard/attendance'],
    ['Teacher', '/dashboard/teachers'],
    ['PlatformOperator', '/dashboard/announcements'],
    ['Student', '/dashboard/reports'],
  ] as [Role, string][])('a deep link outside the set is refused: %s → %s', (role, path) => {
    expect(canAccess(role, path)).toBe(false);
  });

  it('chat and video routes inherit Meetings access; unknown app paths are refused', () => {
    expect(canAccess('Parent', '/chat/abc')).toBe(true);
    expect(canAccess('PlatformOperator', '/video-call/abc')).toBe(true);
    expect(canAccess('Administrator', '/dashboard/payment')).toBe(false);
  });
});
