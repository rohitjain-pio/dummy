import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Copy, Download, Eye, Pencil, Plus, RotateCcw, Trash2 } from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useSession } from '@/auth/session';
import { BarChart, DonutChart } from '@/components/charts';
import { Badge, Button, Card, ConfirmDialog, DataTable, EmptyState, ErrorBanner, LoadingState, Modal, PageHeader, SelectField, TextField } from '@/components/ui';
import { Pagination, usePaged } from '@/components/ui/Pagination';
import { toast } from '@/components/ui/toast';
import { emailError } from '@/features/auth/validation';
import { api, ApiError, errorMessage } from '@/lib/api';
import { classLabel, downloadCsv, formatDate, parseUtc, useClasses, useStudents } from '@/lib/queries';
import { isLeadership } from '@/lib/roles';
import type { ParentLinkCodeIssued, Student } from '@/lib/types';
import { SearchBox } from './Teachers';

// UI-009 — Students (API-011–019). Leadership: full CRUD; Teacher: view only (edit/withdraw not rendered).
// "Delete" is a withdrawal — student records are soft-deactivated, never removed (FR-015).
export function StudentsPage() {
  const role = useSession((s) => s.user?.role);
  const leader = isLeadership(role);
  const students = useStudents();
  const classes = useClasses();
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [classFilter, setClassFilter] = useState('');
  const [editing, setEditing] = useState<Student | 'new' | null>(null);
  const [viewing, setViewing] = useState<Student | null>(null);
  const [withdrawing, setWithdrawing] = useState<Student | null>(null);

  const className = (id: string | null) => {
    const c = classes.data?.find((x) => x.id === id);
    return c ? classLabel(c) : '—';
  };
  const filtered = useMemo(() => {
    const s = search.trim().toLowerCase();
    return (students.data ?? []).filter((st) =>
      (!classFilter || st.classId === classFilter) &&
      (!s || st.name.toLowerCase().includes(s) || st.email.toLowerCase().includes(s) || st.srNumber.toLowerCase().includes(s)));
  }, [students.data, search, classFilter]);
  const paged = usePaged(filtered);

  const setActive = useMutation({
    mutationFn: ({ s, active }: { s: Student; active: boolean }) => api.post(`/students/${s.id}/${active ? 'reactivate' : 'withdraw'}`),
    onSuccess: (_, v) => { toast.success(v.active ? 'Student reactivated' : 'Student withdrawn'); setWithdrawing(null); void qc.invalidateQueries({ queryKey: ['students'] }); },
    onError: (e) => { toast.error(errorMessage(e)); setWithdrawing(null); },
  });

  const all = students.data ?? [];
  const perClass = (classes.data ?? []).map((c) => ({ label: `${c.name} ${c.section}`, value: all.filter((s) => s.classId === c.id && s.isActive).length }));
  const active = all.filter((s) => s.isActive).length;

  return (
    <>
      <PageHeader title="Students" description="Student records, enrolment and guardian details."
        actions={leader && <Button onClick={() => setEditing('new')}><Plus aria-hidden className="size-4" />Add Student</Button>} />
      <div className="mb-6 grid gap-6 lg:grid-cols-3">
        <Card className="p-6 lg:col-span-2"><h3 className="font-bold text-text-primary">Enrolment by Class</h3><BarChart title="Active students by class" data={perClass} /></Card>
        <Card className="p-6"><h3 className="font-bold text-text-primary">Status Breakdown</h3><DonutChart title="Student status" data={all.length ? [{ label: 'Active', value: active }, { label: 'Withdrawn', value: all.length - active }] : []} /></Card>
      </div>
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <SearchBox label="Search by name, email or SR number" value={search} onChange={(v) => { setSearch(v); paged.setPage(0); }} />
        <label className="basis-48">
          <span className="sr-only">Filter by class</span>
          <select value={classFilter} onChange={(e) => { setClassFilter(e.target.value); paged.setPage(0); }} className="h-10 w-full rounded-md border border-border bg-surface px-3 text-body-sm text-text-primary">
            <option value="">All classes</option>
            {(classes.data ?? []).map((c) => <option key={c.id} value={c.id}>{classLabel(c)}</option>)}
          </select>
        </label>
        <Button variant="outline" disabled={filtered.length === 0} onClick={() => downloadCsv('students.csv', ['SR number', 'Roll number', 'Name', 'Email', 'Class', 'Date of birth', 'Gender', 'Status'],
          filtered.map((s) => [s.srNumber, s.rollNumber, s.name, s.email, className(s.classId), s.dateOfBirth, s.gender, s.isActive ? 'Active' : 'Withdrawn']))}>
          <Download aria-hidden className="size-4" />Export CSV
        </Button>
      </div>
      {students.isPending && <LoadingState label="Loading students" rows={6} />}
      {students.isError && <ErrorBanner>{errorMessage(students.error)}</ErrorBanner>}
      {students.isSuccess && filtered.length === 0 && <EmptyState title="No students found" description="Adjust your search or filters." />}
      {students.isSuccess && filtered.length > 0 && (
        <>
          <DataTable caption="Students" rows={paged.slice} rowKey={(s) => s.id} columns={[
            { header: 'SR No.', cell: (s) => s.srNumber },
            { header: 'Roll No.', cell: (s) => s.rollNumber },
            { header: 'Student', cell: (s) => <span><span className="block font-medium">{s.name}</span><span className="block text-caption text-text-secondary">{s.email}</span></span> },
            { header: 'Class', cell: (s) => className(s.classId) },
            { header: 'Date of birth', cell: (s) => formatDate(s.dateOfBirth) },
            { header: 'Gender', cell: (s) => s.gender },
            { header: 'Status', cell: (s) => <Badge tone={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Active' : 'Withdrawn'}</Badge> },
            { header: 'Actions', cell: (s) => (
              <span className="flex">
                <Button variant="ghost" size="icon" aria-label={`View ${s.name}`} onClick={() => setViewing(s)}><Eye aria-hidden className="size-4" /></Button>
                {leader && <Button variant="ghost" size="icon" aria-label={`Edit ${s.name}`} onClick={() => setEditing(s)}><Pencil aria-hidden className="size-4" /></Button>}
                {leader && (s.isActive
                  ? <Button variant="ghost" size="icon" aria-label={`Withdraw ${s.name}`} onClick={() => setWithdrawing(s)}><Trash2 aria-hidden className="size-4" /></Button>
                  : <Button variant="ghost" size="icon" aria-label={`Reactivate ${s.name}`} onClick={() => setActive.mutate({ s, active: true })}><RotateCcw aria-hidden className="size-4" /></Button>)}
              </span>
            ) },
          ]} />
          <Pagination {...paged} noun="students" />
        </>
      )}
      {editing && <StudentModal existing={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {viewing && <StudentDetail student={viewing} className={className(viewing.classId)} canManage={leader} onClose={() => setViewing(null)} />}
      <ConfirmDialog open={!!withdrawing} onOpenChange={(o) => !o && setWithdrawing(null)} subject={withdrawing?.name ?? ''} actionLabel="Withdraw"
        loading={setActive.isPending} onConfirm={() => withdrawing && setActive.mutate({ s: withdrawing, active: false })} />
    </>
  );
}

function StudentModal({ existing, onClose }: { existing: Student | null; onClose: () => void }) {
  const classes = useClasses();
  const qc = useQueryClient();
  const [f, setF] = useState({
    srNumber: existing?.srNumber ?? '', rollNumber: existing?.rollNumber ?? '', name: existing?.name ?? '', dateOfBirth: existing?.dateOfBirth ?? '',
    gender: existing?.gender ?? '', email: existing?.email ?? '', classId: existing?.classId ?? '',
    guardianFatherName: existing?.guardianFatherName ?? '', guardianMotherName: existing?.guardianMotherName ?? '',
    guardianPhone: existing?.guardianPhone ?? '', guardianContactEmail: existing?.guardianContactEmail ?? '',
  });
  const [error, setError] = useState<string>();
  const [saving, setSaving] = useState(false);
  const [touched, setTouched] = useState(false);
  const set = (k: keyof typeof f) => (e: { target: { value: string } }) => setF({ ...f, [k]: e.target.value });

  async function submit(e: FormEvent) {
    e.preventDefault();
    setTouched(true);
    if (emailError(f.email) || (f.guardianContactEmail && emailError(f.guardianContactEmail))) return;
    setError(undefined); setSaving(true);
    const body = { ...f, guardianFatherName: f.guardianFatherName || null, guardianMotherName: f.guardianMotherName || null, guardianPhone: f.guardianPhone || null, guardianContactEmail: f.guardianContactEmail || null };
    try {
      if (existing) await api.put(`/students/${existing.id}`, body); else await api.post('/students', body);
      toast.success(existing ? 'Student updated' : 'Student added');
      void qc.invalidateQueries({ queryKey: ['students'] });
      onClose();
    } catch (err) {
      // ERR-301 duplicate SR/roll · ERR-801 plan capacity
      setError(err instanceof ApiError && err.code === 'ERR-801' ? `${err.message}` : errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={existing ? 'Edit student' : 'Add student'} size="lg">
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2"><ErrorBanner>{error}</ErrorBanner></div>
        <TextField label="SR number" required value={f.srNumber} onChange={set('srNumber')} />
        <TextField label="Roll number" required value={f.rollNumber} onChange={set('rollNumber')} />
        <TextField label="Full name" required value={f.name} onChange={set('name')} />
        <TextField label="Date of birth" type="date" required value={f.dateOfBirth} onChange={set('dateOfBirth')} />
        <SelectField label="Gender" required value={f.gender} onChange={set('gender')}>
          <option value="">Select gender</option><option>Female</option><option>Male</option><option>Other</option>
        </SelectField>
        <TextField label="Email" type="email" required value={f.email} onBlur={() => setTouched(true)} error={touched ? emailError(f.email) : undefined} onChange={set('email')} />
        <SelectField className="sm:col-span-2" label="Class" required value={f.classId} onChange={set('classId')}>
          <option value="">Select class</option>
          {(classes.data ?? []).map((c) => <option key={c.id} value={c.id}>{classLabel(c)}</option>)}
        </SelectField>
        <fieldset className="grid gap-4 border-t border-border-subtle pt-4 sm:col-span-2 sm:grid-cols-2">
          <legend className="font-bold text-text-primary">Guardian details</legend>
          <TextField label="Father's name" value={f.guardianFatherName} onChange={set('guardianFatherName')} />
          <TextField label="Mother's name" value={f.guardianMotherName} onChange={set('guardianMotherName')} />
          <TextField label="Guardian phone" type="tel" value={f.guardianPhone} onChange={set('guardianPhone')} />
          <TextField label="Guardian email" type="email" value={f.guardianContactEmail} error={touched && f.guardianContactEmail ? emailError(f.guardianContactEmail) : undefined} onChange={set('guardianContactEmail')} />
        </fieldset>
        <div className="flex justify-end gap-2 pt-2 sm:col-span-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={saving}>{existing ? 'Save changes' : 'Add student'}</Button>
        </div>
      </form>
    </Modal>
  );
}

function StudentDetail({ student, className, canManage, onClose }: { student: Student; className: string; canManage: boolean; onClose: () => void }) {
  const [code, setCode] = useState<ParentLinkCodeIssued | null>(null);
  const generate = useMutation({
    mutationFn: async () => {
      // "regenerate" invalidates the prior code before issuing a new one (UI-009)
      if (code) await api.post(`/students/${student.id}/parent-link-codes/${code.codeId}/revoke`).catch(() => undefined);
      return api.post<ParentLinkCodeIssued>(`/students/${student.id}/parent-link-codes`);
    },
    onSuccess: (c) => setCode(c),
    onError: (e) => toast.error(errorMessage(e)),
  });

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={student.name} description={`SR ${student.srNumber} · Roll ${student.rollNumber}`} size="lg">
      <dl className="grid gap-4 text-body-sm sm:grid-cols-2">
        {[['Class', className], ['Email', student.email], ['Date of birth', formatDate(student.dateOfBirth)], ['Gender', student.gender], ['Status', student.isActive ? 'Active' : 'Withdrawn']].map(([k, v]) => (
          <div key={k}><dt className="text-text-secondary">{k}</dt><dd className="font-medium text-text-primary">{v}</dd></div>
        ))}
      </dl>
      {/* BR-021: the guardian block follows the row's own access rule — only rendered to roles that can see this row */}
      <h3 className="mt-6 border-t border-border-subtle pt-5 font-bold text-text-primary">Guardian</h3>
      <dl className="mt-3 grid gap-4 text-body-sm sm:grid-cols-2">
        {[["Father's name", student.guardianFatherName], ["Mother's name", student.guardianMotherName], ['Phone', student.guardianPhone], ['Email', student.guardianContactEmail]].map(([k, v]) => (
          <div key={k}><dt className="text-text-secondary">{k}</dt><dd className="font-medium text-text-primary">{v || '—'}</dd></div>
        ))}
      </dl>
      {canManage && student.isActive && (
        <div className="mt-6 border-t border-border-subtle pt-5">
          <h3 className="font-bold text-text-primary">Parent link code</h3>
          {code ? (
            <div className="mt-3 rounded-md bg-primary-subtle-bg p-4">
              <p className="text-caption text-primary-subtle-text">Shown once — share it with the parent now.</p>
              <p className="mt-1 flex items-center gap-3 font-mono text-h2 tracking-widest text-text-primary">
                {code.code}
                <Button size="icon" variant="ghost" aria-label="Copy code" onClick={() => { void navigator.clipboard?.writeText(code.code); toast.success('Code copied'); }}><Copy aria-hidden className="size-4" /></Button>
              </p>
              <p className="text-caption text-text-secondary">Expires {parseUtc(code.expiresAtUtc).toLocaleString('en-IN')}</p>
            </div>
          ) : <p className="mt-2 text-body-sm text-text-secondary">Generate a one-time code a parent can use to link to this student.</p>}
          <Button className="mt-3" variant={code ? 'outline' : 'primary'} loading={generate.isPending} onClick={() => generate.mutate()}>{code ? 'Regenerate code' : 'Generate parent link code'}</Button>
        </div>
      )}
    </Modal>
  );
}
