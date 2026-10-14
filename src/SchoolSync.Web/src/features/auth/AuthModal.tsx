import { useQuery } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { login, register, useAuthModal } from '@/auth/authActions';
import { Button, ErrorBanner, FieldShell, Modal, SelectField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, ApiError, errorMessage } from '@/lib/api';
import { REGISTRABLE_ROLES, ROLE_LABEL, type Role } from '@/lib/roles';
import type { DirectoryEntry } from '@/lib/types';
import { emailError, passwordError } from './validation';

// UI-005 — Auth modal (login / register). Mounted over any public route.
export function AuthModal() {
  const { open, mode, setOpen, setMode } = useAuthModal();
  return (
    <Modal
      open={open}
      onOpenChange={setOpen}
      title={mode === 'login' ? 'Login' : 'Register'}
      description={mode === 'login' ? 'Sign in to your SchoolSync workspace' : 'Create your account against your institution'}
    >
      {/* key: toggling Login↔Register remounts the form, which resets it (UI-005 navigation rule) */}
      {mode === 'login'
        ? <LoginForm key="login" onSwitch={() => setMode('register')} onDone={() => setOpen(false)} />
        : <RegisterForm key="register" onSwitch={() => setMode('login')} onDone={() => setOpen(false)} />}
    </Modal>
  );
}

function LoginForm({ onSwitch, onDone }: { onSwitch: () => void; onDone: () => void }) {
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(undefined);
    setLoading(true);
    try {
      const user = await login(username, password);
      toast.success(`Signed in as ${user.username}`);
      onDone();
      navigate('/dashboard');
    } catch (err) {
      setError(errorMessage(err)); // ERR-202, deliberately generic
      setPassword(''); // password cleared, all other fields retained
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4" noValidate={false}>
      <ErrorBanner>{error}</ErrorBanner>
      <TextField label="Username" required autoComplete="username" value={username} onChange={(e) => setUsername(e.target.value)} />
      <TextField label="Password" required type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      <div className="flex flex-wrap items-center justify-between gap-2 text-body-sm">
        {/* Remember me: the refresh cookie already keeps the session for CFG-002's 14 days; this box has
            no server behaviour behind it (no FR). "Forgot password?" has no endpoint (Q-236 gap, filed). */}
        <label className="flex items-center gap-2 text-text-secondary"><input type="checkbox" className="size-4 accent-primary" /> Remember me</label>
        <span className="text-text-secondary">Forgot password? Contact your school administrator.</span>
      </div>
      <Button type="submit" className="w-full" size="lg" loading={loading}>Login</Button>
      <p className="text-center text-body-sm text-text-secondary">
        Don&apos;t have an account? <button type="button" onClick={onSwitch} className="font-semibold text-primary underline-offset-2 hover:underline">Register</button>
      </p>
    </form>
  );
}

function RegisterForm({ onSwitch, onDone }: { onSwitch: () => void; onDone: () => void }) {
  const navigate = useNavigate();
  const [form, setForm] = useState({ username: '', email: '', password: '', role: '' as Role | '' });
  const [schoolQuery, setSchoolQuery] = useState('');
  const [school, setSchool] = useState<DirectoryEntry | null>(null);
  const [touched, setTouched] = useState({ email: false, password: false });
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);

  const directory = useQuery({
    queryKey: ['public-institutions', schoolQuery],
    queryFn: () => api.get<DirectoryEntry[]>(`/public/institutions?query=${encodeURIComponent(schoolQuery)}`, { anonymous: true }),
    enabled: schoolQuery.trim().length >= 2 && !school,
  });

  const emailErr = touched.email || form.email ? emailError(form.email) : undefined;
  const passwordErr = touched.password || form.password ? passwordError(form.password) : undefined;

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(undefined);
    if (!school) {
      setError('Choose your school from the list.'); // unknown school
      return;
    }
    if (emailError(form.email) || passwordError(form.password) || !form.role) return;
    setLoading(true);
    try {
      await register({ username: form.username, email: form.email, password: form.password, role: form.role, institutionId: school.id });
      toast.success('Registration successful');
      onDone();
      navigate('/dashboard');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : errorMessage(err)); // ERR-203 / ERR-201 / ERR-901
      setForm((f) => ({ ...f, password: '' }));
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      <ErrorBanner>{error}</ErrorBanner>
      <TextField label="Username" required autoComplete="username" value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} />
      <TextField label="Email" required type="email" autoComplete="email" value={form.email} error={emailErr}
        onChange={(e) => setForm({ ...form, email: e.target.value })} onBlur={() => setTouched({ ...touched, email: true })} />
      <TextField label="Password" required type="password" autoComplete="new-password" value={form.password} error={passwordErr}
        hint="At least 10 characters, using three of: uppercase, lowercase, numbers, symbols."
        onChange={(e) => setForm({ ...form, password: e.target.value })} onBlur={() => setTouched({ ...touched, password: true })} />
      <SelectField label="Role" required value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as Role })}>
        <option value="">Select role</option>
        {REGISTRABLE_ROLES.map((r) => <option key={r} value={r}>{ROLE_LABEL[r]}</option>)}
      </SelectField>

      <FieldShell label="School" required hint={school ? `${school.city}, ${school.region}` : 'Type at least 2 letters to search'}>
        {({ id, describedBy }) => (
          <div className="relative">
            <input
              id={id}
              role="combobox"
              aria-expanded={!school && (directory.data?.length ?? 0) > 0}
              aria-controls={`${id}-list`}
              aria-autocomplete="list"
              aria-required
              aria-describedby={describedBy}
              required
              autoComplete="off"
              value={school ? school.name : schoolQuery}
              onChange={(e) => { setSchool(null); setSchoolQuery(e.target.value); }}
              placeholder="Search and select school"
              className="w-full rounded-md border border-border bg-surface px-3.5 py-2.5 text-body-sm text-text-primary placeholder:text-text-secondary outline-none focus:border-primary focus:ring-2 focus:ring-primary-subtle-bg-strong"
            />
            {!school && (directory.data?.length ?? 0) > 0 && (
              <ul id={`${id}-list`} role="listbox" className="absolute z-10 mt-1 max-h-48 w-full overflow-y-auto rounded-md border border-border bg-surface shadow-card">
                {directory.data!.map((d) => (
                  <li key={d.id} role="option" aria-selected={false}>
                    <button type="button" onClick={() => setSchool(d)} className="w-full px-3.5 py-2 text-left text-body-sm hover:bg-background focus:bg-background">
                      <span className="font-medium text-text-primary">{d.name}</span> <span className="text-text-secondary">· {d.city}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </FieldShell>

      <Button type="submit" className="w-full" size="lg" loading={loading}>Register</Button>
      <p className="text-center text-body-sm text-text-secondary">
        Already have an account? <button type="button" onClick={onSwitch} className="font-semibold text-primary underline-offset-2 hover:underline">Login</button>
      </p>
    </form>
  );
}
