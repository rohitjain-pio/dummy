import { act, cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAuthModal } from '@/auth/authActions';
import { useSession } from '@/auth/session';
import { AuthModal } from '@/features/auth/AuthModal';
import { mockApi, renderRoute, signIn, USER_ID } from '@/test/render';
import { AnnouncementsPage } from './app/Announcements';
import { AttendancePage } from './app/Attendance';
import { ClassesPage } from './app/Classes';
import { ReportsPage } from './app/Reports';
import { SettingsPage } from './app/Settings';
import { PricingPage } from './public/Pricing';

afterEach(() => {
  cleanup(); // unmount before the session is cleared below
  vi.unstubAllGlobals();
  useSession.setState({ status: 'anonymous', accessToken: null, user: null });
});

const CLASS = { id: 'c1', name: 'Grade 5', section: 'A', classTeacherId: null };
const ANNOUNCEMENT = { id: 'a1', title: 'Sports day', content: 'Friday', effectiveDate: '2026-09-14', authorId: USER_ID, createdAtUtc: '2026-09-14T06:00:00', isWithdrawn: false, withdrawnAtUtc: null };

describe('UI-005 auth modal', () => {
  it('Register role dropdown never offers PlatformOperator (Q-202)', async () => {
    mockApi({});
    useAuthModal.setState({ open: true, mode: 'register' });
    renderRoute(<AuthModal />);
    const options = within(await screen.findByLabelText(/Role/)).getAllByRole('option').map((o) => o.textContent);
    expect(options).toEqual(['Select role', 'Administrator', 'Principal', 'School In-charge', 'Teacher', 'Student', 'Parent']);
  });

  it('a failed login shows the generic banner, clears only the password, and keeps the modal open', async () => {
    mockApi({ 'POST /auth/login': () => new Response(JSON.stringify({ code: 'ERR-202', message: 'Your username or password is incorrect.' }), { status: 401 }) });
    useAuthModal.setState({ open: true, mode: 'login' });
    renderRoute(<AuthModal />);
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/Username/), 'jane');
    await user.type(screen.getByLabelText(/Password/), 'wrong-password');
    await user.click(screen.getByRole('button', { name: 'Login' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Your username or password is incorrect.');
    expect(screen.getByLabelText(/Username/)).toHaveValue('jane');
    expect(screen.getByLabelText(/Password/)).toHaveValue('');
  });

  it('register submits the id of the school chosen from the directory (Q-235)', async () => {
    const calls = mockApi({
      'GET /public/institutions': [{ id: 'inst-42', name: 'Green Meadows School', city: 'Jaipur', region: 'RJ' }],
      'POST /auth/register': { accessToken: 't', user: { id: 'u', username: 'jane', role: 'Parent', institutionId: 'inst-42' } },
    });
    useAuthModal.setState({ open: true, mode: 'register' });
    renderRoute(<AuthModal />);
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/Username/), 'jane');
    await user.type(screen.getByLabelText(/^Email/), 'jane@school.edu');
    await user.type(screen.getByLabelText(/^Password/), 'Correct-Horse9');
    await user.selectOptions(screen.getByLabelText(/Role/), 'Parent');
    await user.type(screen.getByRole('combobox', { name: /School/ }), 'Green');
    await user.click(await screen.findByRole('button', { name: /Green Meadows School/ }));
    await user.click(screen.getByRole('button', { name: 'Register' }));
    await waitFor(() => expect(calls.find((c) => c.path === '/auth/register')?.body).toMatchObject({ institutionId: 'inst-42', role: 'Parent' }));
  });
});

describe('UI-004 pricing (Q-233/Q-237)', () => {
  it('renders plan structure from the API, no price and no billing toggle', async () => {
    mockApi({ 'GET /public/plans': [
      { id: 'p3', name: 'Bespoke', learnerCapacity: null, storageAllowanceGb: 500, supportLevel: 'Dedicated', capabilities: ['AdvancedAnalytics', 'ParentAccess', 'CustomReporting'] },
      { id: 'p1', name: 'Entry', learnerCapacity: 200, storageAllowanceGb: 5, supportLevel: 'Email', capabilities: [] },
      { id: 'p2', name: 'Mid', learnerCapacity: 1000, storageAllowanceGb: 25, supportLevel: 'Priority', capabilities: ['ParentAccess'] },
    ] });
    renderRoute(<PricingPage />);
    const cards = await screen.findAllByRole('heading', { level: 2, name: /Entry|Mid|Bespoke/ });
    expect(cards.map((h) => h.textContent)).toEqual(['Entry', 'Mid', 'Bespoke']);
    expect(screen.getAllByText('Contact sales', { selector: 'p' })).toHaveLength(3);
    expect(document.body.textContent).not.toMatch(/₹|per month|per year|monthly|yearly/i);
    const row = screen.getByRole('rowheader', { name: 'Parent access' }).closest('tr')!;
    expect(within(row).getAllByText(/Included|Not included/).map((c) => c.textContent)).toEqual(['Not included', 'Included', 'Included']);
  });
});

describe('UI-007 classes — BR-029 role gating', () => {
  it('Teacher sees no add/view/edit/delete icon actions at all, but keeps the quick actions', async () => {
    signIn('Teacher');
    mockApi({ 'GET /classes': [CLASS], 'GET /students': [] });
    renderRoute(<ClassesPage />);
    await screen.findByText('Grade 5 – A');
    expect(screen.queryByRole('button', { name: /Add New Class/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /Edit|Delete/ })).toBeNull();
    expect(screen.getByRole('link', { name: 'Take Attendance' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'View Roster' })).toBeInTheDocument();
  });

  it('Leadership delete goes through the destructive confirmation naming the class', async () => {
    signIn('Principal');
    const calls = mockApi({ 'GET /classes': [CLASS], 'GET /students': [], 'GET /teachers': [], 'DELETE /classes/c1': () => new Response(null, { status: 204 }) });
    renderRoute(<ClassesPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Delete Grade 5 – A' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('This action cannot be undone.');
    expect(calls.some((c) => c.method === 'DELETE')).toBe(false);
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(calls.some((c) => c.method === 'DELETE' && c.path === '/classes/c1')).toBe(true));
  });
});

describe('UI-011 announcements — BR-038', () => {
  it.each([['Teacher', true, false], ['Administrator', true, true], ['Student', false, false]] as const)('%s: post=%s withdraw=%s', async (role, canPost, canWithdraw) => {
    signIn(role);
    mockApi({ 'GET /announcements': [ANNOUNCEMENT] });
    renderRoute(<AnnouncementsPage />);
    await screen.findByText('Sports day', { selector: 'h3' });
    expect(!!screen.queryByRole('button', { name: /New Announcement/ })).toBe(canPost);
    expect(!!screen.queryByRole('button', { name: /Withdraw Sports day/ })).toBe(canWithdraw);
  });
});

describe('UI-010 attendance marking — BRL-034', () => {
  it('every unmarked roster row opens with no outcome selected (never pre-selected Absent) and saves only marked rows', async () => {
    signIn('Teacher');
    const calls = mockApi({
      'GET /classes': [CLASS],
      'GET /students': [],
      'GET /attendance': [],
      'GET /attendance/roster': [{ studentId: 's1', name: 'Asha', currentOutcome: null }, { studentId: 's2', name: 'Ravi', currentOutcome: null }],
      'PUT /attendance': [],
    });
    renderRoute(<AttendancePage />, '/dashboard/attendance?classId=c1', '/dashboard/attendance');
    fireEvent.click(await screen.findByRole('button', { name: 'Fill Attendance' }));
    const dialog = await screen.findByRole('dialog');
    const groups = within(dialog).getAllByRole('radiogroup');
    expect(groups).toHaveLength(2);
    for (const g of groups) expect(within(g).getAllByRole('radio').every((r) => r.getAttribute('aria-checked') === 'false')).toBe(true);

    fireEvent.click(within(groups[0]!).getByRole('radio', { name: 'Late' }));
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save attendance' }));
    await waitFor(() => expect(calls.find((c) => c.method === 'PUT')?.body).toMatchObject({ classId: 'c1', entries: [{ personId: 's1', personType: 'Student', outcome: 'Late' }] }));
  });
});

describe('UI-017 reports — binding exclusion', () => {
  it('never renders Student Satisfaction or Course Completion, and offers CSV only', async () => {
    signIn('Administrator');
    mockApi({ 'GET /classes': [CLASS], 'GET /reports': [], 'GET /attendance': [] });
    renderRoute(<ReportsPage />);
    await screen.findByRole('heading', { name: 'Student Performance' });
    expect(document.body.textContent).not.toMatch(/satisfaction|completion/i);
    fireEvent.click(screen.getByRole('button', { name: /Generate Class Attendance report/ }));
    const format = within(await screen.findByRole('dialog')).getByLabelText('Format');
    expect(within(format).getAllByRole('option').map((o) => o.textContent)).toEqual(['CSV']);
  });
});

describe('UI-018 settings — role-gated DOM', () => {
  it('non-leadership roles get the own-account screen with no configuration sections in the DOM', async () => {
    signIn('Teacher');
    const calls = mockApi({});
    renderRoute(<SettingsPage />);
    expect(await screen.findByText('test.teacher')).toBeInTheDocument();
    expect(screen.queryByText(/Configure|Notifications|Academic|System Information/)).toBeNull();
    expect(calls.some((c) => c.path.startsWith('/institution-config'))).toBe(false);
  });

  it('leadership configure dialog has no audit toggle (BR-059 unconditional)', async () => {
    signIn('Principal');
    mockApi({ 'GET /institution-config': { academicYear: '2026-27', timeZone: 'Asia/Kolkata', language: 'en', notifications: { email: true, sms: false, inPlatform: true }, academic: { gradingScale: 'A-F', courseCategories: [], assessmentTypes: [], reportCardFormat: 'Termly' }, isDefault: false } });
    renderRoute(<SettingsPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Configure Notifications/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByText(/audit/i, { selector: 'label, input' })).toBeNull();
    expect(within(dialog).getAllByRole('checkbox')).toHaveLength(3);
    await act(async () => undefined);
  });
});
