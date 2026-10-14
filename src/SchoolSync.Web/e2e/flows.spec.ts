import { expect, test } from '@playwright/test';
import { mockApi, USER_ID } from './mockApi';

test.describe('UI-006 app shell', () => {
  test('nav shows exactly the Teacher set, marks the active route, and a deep link outside it renders Unauthorized', async ({ page }) => {
    await mockApi(page, 'Teacher');
    await page.goto('/dashboard/classes');
    const nav = page.getByRole('navigation', { name: 'Primary' });
    await expect(nav.getByRole('link')).toHaveText(['Dashboard', 'Classes', 'Students', 'Attendance', 'Announcements', 'Meetings', 'Schedule', 'Reports', 'Settings']);
    await expect(nav.getByRole('link', { name: 'Classes' })).toHaveAttribute('aria-current', 'page');
    await page.goto('/dashboard/teachers');
    await expect(page.getByText('Your current role does not have permission to view this area.')).toBeVisible();
  });

  test('profile menu is a disclosure that closes on Escape and offers no role switcher', async ({ page }) => {
    await mockApi(page, 'Administrator');
    await page.goto('/dashboard');
    const trigger = page.getByRole('button', { name: /Profile menu/ });
    await trigger.click();
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await expect(page.getByRole('menu')).not.toContainText(/switch/i);
    await page.keyboard.press('Escape');
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');
  });

  test('below bp-lg the side nav is off-canvas and dismisses from the backdrop', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 900 });
    await mockApi(page, 'Administrator');
    await page.goto('/dashboard');
    const sidebar = page.locator('#app-sidebar');
    await expect(sidebar).not.toBeInViewport();
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await expect(sidebar).toBeInViewport();
    await page.mouse.click(740, 450);
    await expect(sidebar).not.toBeInViewport();
  });

  test('a signed-out deep link lands on Home with the login modal open', async ({ page }) => {
    await mockApi(page);
    await page.goto('/dashboard/students');
    await expect(page).toHaveURL('/');
    await expect(page.getByRole('dialog', { name: 'Login' })).toBeVisible();
  });
});

test.describe('UI-005 login', () => {
  test('success keeps the token out of web storage and script-readable cookies, then redirects to the dashboard', async ({ page }) => {
    await mockApi(page, undefined, {
      'POST /auth/login': (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ accessToken: 'jwt-secret', user: { id: USER_ID, username: 'jane', role: 'Principal', institutionId: 'inst-1' } }) }),
      'GET /dashboard': (route) => route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ variant: 'Leadership', today: '2026-09-14', recentAnnouncements: [], upcomingEvents: [], totalStudents: 0, totalTeachers: 0, totalClasses: 0, presentStudentsToday: 0, presentTeachersToday: 0, attendanceTrend: [], studentsPerClass: [], genderSplit: [] }),
      }),
    });
    await page.goto('/');
    await page.getByRole('button', { name: 'Login / Register' }).click();
    await page.getByLabel('Username').fill('jane');
    await page.getByLabel('Password').fill('Correct-Horse9');
    await page.getByRole('button', { name: 'Login', exact: true }).click();
    await expect(page).toHaveURL('/dashboard');
    await expect(page.getByTestId('toast').getByText('Signed in as jane')).toBeVisible();
    const stored = await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }) + document.cookie);
    expect(stored).not.toContain('jwt-secret');
    // brand-new institution: tiles show 0 and charts show an empty state rather than a broken axis
    await expect(page.getByText(/No data yet for/).first()).toBeVisible();
  });

  test('Escape closes the modal and focus stays trapped inside it while open', async ({ page }) => {
    await mockApi(page);
    await page.goto('/');
    await page.getByRole('button', { name: 'Login / Register' }).click();
    const dialog = page.getByRole('dialog');
    for (let i = 0; i < 12; i++) await page.keyboard.press('Tab');
    expect(await dialog.evaluate((d) => d.contains(document.activeElement))).toBe(true);
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
  });
});

test.describe('UI-014 video call states in a real browser (fake ACS, Q-234)', () => {
  test('connecting → connected → participant drop shows reconnecting → leave returns to rooms', async ({ page }) => {
    await mockApi(page, 'Teacher');
    await page.goto('/video-call/r1');
    await expect(page.getByTestId('tile-self')).toContainText('Connecting');
    await page.waitForFunction(() => !!(window as unknown as { __fakeCall?: unknown }).__fakeCall);
    await page.evaluate(() => {
      const f = (window as unknown as { __fakeCall: { setCallState(s: string): void; setParticipants(p: unknown[]): void } }).__fakeCall;
      f.setCallState('connected');
      f.setParticipants([{ id: 'p1', displayName: 'Anita', state: 'connected', muted: false }]);
    });
    await expect(page.getByText('LIVE')).toBeVisible();
    await page.evaluate(() => (window as unknown as { __fakeCall: { setParticipants(p: unknown[]): void } }).__fakeCall.setParticipants([]));
    await expect(page.getByTestId('tile-p1')).toHaveAttribute('data-state', 'reconnecting');
    await page.getByRole('button', { name: 'Leave Call' }).click();
    await expect(page).toHaveURL('/dashboard/meeting');
  });
});

test.describe('UI-003 contact', () => {
  test('on-blur email check is announced; a valid submit toasts and resets the form', async ({ page }) => {
    await mockApi(page);
    await page.goto('/contact');
    const email = page.getByLabel('Email Address');
    await email.fill('not-an-email');
    await email.blur();
    await expect(page.getByText('Enter a valid email address, like name@school.edu.')).toBeVisible();
    await page.getByLabel('Full Name').fill('Jane Doe');
    await email.fill('jane@school.edu');
    await page.getByLabel('Message').fill('We would like a demo.');
    await page.getByRole('button', { name: 'Send Message' }).click();
    await expect(page.getByText('Message sent — our team will respond within 24 hours.').last()).toBeVisible();
    await expect(page.getByLabel('Full Name')).toHaveValue('');
  });
});
