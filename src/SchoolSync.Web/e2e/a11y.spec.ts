import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { mockApi, type Role } from './mockApi';

// CT-601–604 gate: "a11y scan AA" on every screen, verified at bp-sm/bp-md/bp-lg/bp-xl (NFR-005, NFR-025).
const BREAKPOINTS = [
  { name: 'bp-sm', width: 640, height: 900 },
  { name: 'bp-md', width: 768, height: 1000 },
  { name: 'bp-lg', width: 1024, height: 900 },
  { name: 'bp-xl', width: 1280, height: 900 },
];

interface Screen { name: string; path: string; role?: Role; ready: (page: Page) => Promise<unknown>; open?: (page: Page) => Promise<unknown> }

const heading = (name: string | RegExp) => (page: Page) => expect(page.getByRole('heading', { name }).first()).toBeVisible();
const openLogin = async (p: Page) => { await p.getByRole('button', { name: 'Login / Register' }).click(); await expect(p.getByRole('dialog')).toBeVisible(); };

const SCREENS: Screen[] = [
  { name: 'UI-001 Home', path: '/', ready: heading('Streamline Your School Management') },
  { name: 'UI-002 About', path: '/about', ready: heading('Empowering Education Through Innovation') },
  { name: 'UI-003 Contact', path: '/contact', ready: heading('Get in Touch') },
  { name: 'UI-004 Pricing', path: '/pricing', ready: heading('Compare Features') },
  { name: 'UI-005 Login modal', path: '/', ready: heading('Streamline Your School Management'), open: openLogin },
  { name: 'UI-005 Register modal', path: '/', ready: heading('Streamline Your School Management'), open: async (p) => { await openLogin(p); await p.getByRole('button', { name: 'Register' }).click(); await expect(p.getByLabel('Role')).toBeVisible(); } },
  { name: 'UI-006/015 Dashboard (leadership)', path: '/dashboard', role: 'Principal', ready: heading('Attendance Trend') },
  { name: 'UI-015 Dashboard (teacher)', path: '/dashboard', role: 'Teacher', ready: heading('Students per Class') },
  { name: 'UI-015 Dashboard (student)', path: '/dashboard', role: 'Student', ready: heading('Recent Announcements') },
  { name: 'UI-015 Dashboard (parent)', path: '/dashboard', role: 'Parent', ready: (p) => expect(p.getByRole('tab', { name: 'Student 2' })).toBeVisible() },
  { name: 'UI-015 Dashboard (platform operator)', path: '/dashboard', role: 'PlatformOperator', ready: heading('Platform overview') },
  { name: 'UI-007 Classes', path: '/dashboard/classes', role: 'Administrator', ready: heading('Grade 5 – A') },
  { name: 'UI-008 Teachers', path: '/dashboard/teachers', role: 'Administrator', ready: (p) => expect(p.getByRole('table', { name: 'Teachers' })).toBeVisible() },
  { name: 'UI-009 Students', path: '/dashboard/students', role: 'Administrator', ready: (p) => expect(p.getByRole('table', { name: 'Students', exact: true })).toBeVisible() },
  {
    name: 'UI-009 Student detail', path: '/dashboard/students', role: 'Administrator', ready: (p) => expect(p.getByRole('table', { name: 'Students', exact: true })).toBeVisible(),
    open: async (p) => { await p.getByRole('button', { name: 'View Student 1', exact: true }).click(); await expect(p.getByRole('dialog')).toBeVisible(); },
  },
  { name: 'UI-010 Attendance', path: '/dashboard/attendance', role: 'Teacher', ready: (p) => expect(p.getByRole('table', { name: 'Attendance records' })).toBeVisible() },
  {
    name: 'UI-010 Fill attendance', path: '/dashboard/attendance', role: 'Teacher', ready: (p) => expect(p.getByRole('table', { name: 'Attendance records' })).toBeVisible(),
    open: async (p) => { await p.getByRole('button', { name: 'Fill Attendance' }).first().click(); await expect(p.getByRole('radiogroup').first()).toBeVisible(); },
  },
  { name: 'UI-011 Announcements', path: '/dashboard/announcements', role: 'Administrator', ready: heading('Sports day on Friday') },
  { name: 'UI-012 Meetings', path: '/dashboard/meeting', role: 'Student', ready: heading('Staff room') },
  { name: 'UI-013 Chat', path: '/chat/r1', role: 'Student', ready: (p) => expect(p.getByText('Good morning, everyone.')).toBeVisible() },
  { name: 'UI-014 Video call', path: '/video-call/r1', role: 'Student', ready: (p) => expect(p.getByTestId('tile-self')).toBeVisible() },
  { name: 'UI-016 Schedule', path: '/dashboard/schedule', role: 'Administrator', ready: (p) => expect(p.getByRole('table', { name: /Weekly timetable/ })).toBeVisible() },
  { name: 'UI-017 Reports', path: '/dashboard/reports', role: 'Teacher', ready: heading('Class Attendance') },
  { name: 'UI-018 Settings (leadership)', path: '/dashboard/settings', role: 'SchoolInCharge', ready: heading('System Information') },
  { name: 'UI-018 Settings (student)', path: '/dashboard/settings', role: 'Student', ready: (p) => expect(p.getByText('demo.student', { exact: true }).first()).toBeVisible() },
  { name: 'UI-019 Unauthorized', path: '/dashboard/teachers', role: 'Student', ready: heading('Access restricted') },
  { name: 'UI-020 Not found', path: '/no-such-page', ready: heading('Page not found') },
];

for (const bp of BREAKPOINTS) {
  test.describe(`${bp.name} (${bp.width}px)`, () => {
    for (const s of SCREENS) {
      test(`${s.name} — WCAG 2.1 AA, no horizontal overflow`, async ({ page }) => {
        await page.setViewportSize({ width: bp.width, height: bp.height });
        await mockApi(page, s.role);
        await page.goto(s.path);
        await s.ready(page);
        if (s.open) await s.open(page);
        await page.waitForTimeout(300); // let transitions settle before measuring contrast

        const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
        const summary = results.violations.map((v) => `${v.id} (${v.impact}): ${v.nodes.slice(0, 3).map((n) => n.target.join(' ')).join(' | ')}`);
        expect(summary, 'axe violations').toEqual([]);

        // NFR-025: the page itself never scrolls sideways (tables scroll inside their own container)
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        expect(overflow).toBeLessThanOrEqual(0);
      });
    }
  });
}
