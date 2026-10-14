import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Eye, Pencil, Plus, RotateCcw, Search, Trash2 } from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { DonutChart } from '@/components/charts';
import { Avatar, Badge, Button, Card, ConfirmDialog, DataTable, EmptyState, ErrorBanner, LoadingState, Modal, PageHeader, SelectField, TextField } from '@/components/ui';
import { Pagination, usePaged } from '@/components/ui/Pagination';
import { toast } from '@/components/ui/toast';
import { emailError } from '@/features/auth/validation';
import { api, errorMessage } from '@/lib/api';
import { classLabel, downloadCsv, formatDate, localDate, useClasses, useSubjects, useTeachers } from '@/lib/queries';
import type { AttendanceRecord, Teacher } from '@/lib/types';

// UI-008 — Teachers (API-020–025). Leadership only.
// Q-236: the staff-by-subject chart and subject filter need a teacher↔subject listing the API does
// not expose — not shown (filed). The presence donut is built from today's staff attendance records.
export function TeachersPage() {
  const teachers = useTeachers();
  const today = localDate();
  const presence = useQuery({ queryKey: ['attendance', 'all', today], queryFn: () => api.get<AttendanceRecord[]>(`/attendance?date=${today}`) });
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Teacher | 'new' | null>(null);
  const [viewing, setViewing] = useState<Teacher | null>(null);
  const [removing, setRemoving] = useState<Teacher | null>(null);
  const qc = useQueryClient();

  const filtered = useMemo(() => {
    const s = search.trim().toLowerCase();
    return (teachers.data ?? []).filter((t) => !s || t.name.toLowerCase().includes(s) || t.contactEmail.toLowerCase().includes(s));
  }, [teachers.data, search]);
  const paged = usePaged(filtered);

  const setActive = useMutation({
    mutationFn: ({ t, active }: { t: Teacher; active: boolean }) => api.post(`/teachers/${t.id}/${active ? 'reinstate' : 'remove'}`),
    onSuccess: (_, v) => { toast.success(v.active ? 'Teacher reinstated' : 'Teacher removed'); setRemoving(null); void qc.invalidateQueries({ queryKey: ['teachers'] }); },
    onError: (e) => { toast.error(errorMessage(e)); setRemoving(null); }, // ERR-304 active assignments
  });

  const staffToday = (presence.data ?? []).filter((r) => r.personType === 'Teacher');
  const activeCount = (teachers.data ?? []).filter((t) => t.isActive).length;
  const presenceData = activeCount === 0 ? [] : [
    ...(['Present', 'Absent', 'Late', 'Leave'] as const).map((o) => ({ label: o, value: staffToday.filter((r) => r.outcome === o).length })),
    { label: 'Not marked', value: Math.max(0, activeCount - staffToday.length) },
  ];

  return (
    <>
      <PageHeader title="Teachers" description="Your teaching staff directory."
        actions={<Button onClick={() => setEditing('new')}><Plus aria-hidden className="size-4" />Add Teacher</Button>} />
      <div className="mb-6 grid gap-6 lg:grid-cols-2">
        <Card className="p-6"><h3 className="font-bold text-text-primary">Staff Presence Today</h3><DonutChart title="Staff presence today" data={presenceData} /></Card>
        <Card className="p-6">
          <h3 className="font-bold text-text-primary">Staff Status</h3>
          <DonutChart title="Staff status" data={teachers.data?.length ? [{ label: 'Active', value: activeCount }, { label: 'Removed', value: teachers.data.length - activeCount }] : []} />
        </Card>
      </div>
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <SearchBox label="Search teachers by name or email" value={search} onChange={(v) => { setSearch(v); paged.setPage(0); }} />
        <Button variant="outline" onClick={() => downloadCsv('teachers.csv', ['Name', 'Email', 'Phone', 'Gender', 'Address', 'Joining date', 'Status'],
          filtered.map((t) => [t.name, t.contactEmail, t.telephone, t.gender, t.address, t.joiningDate, t.isActive ? 'Active' : 'Removed']))} disabled={filtered.length === 0}>
          <Download aria-hidden className="size-4" />Export CSV
        </Button>
      </div>
      {teachers.isPending && <LoadingState label="Loading teachers" rows={6} />}
      {teachers.isError && <ErrorBanner>{errorMessage(teachers.error)}</ErrorBanner>}
      {teachers.isSuccess && filtered.length === 0 && <EmptyState title="No teachers found" description="Adjust your search or filters." />}
      {teachers.isSuccess && filtered.length > 0 && (
        <>
          <DataTable caption="Teachers" rows={paged.slice} rowKey={(t) => t.id} columns={[
            { header: 'Teacher', cell: (t) => <span className="flex items-center gap-2.5"><Avatar name={t.name} /><span><span className="block font-medium">{t.name}</span><span className="block text-caption text-text-secondary">{t.contactEmail}</span></span></span> },
            { header: 'Phone', cell: (t) => t.telephone },
            { header: 'Gender', cell: (t) => t.gender },
            { header: 'Address', cell: (t) => <span className="line-clamp-1">{t.address}</span> },
            { header: 'Status', cell: (t) => <Badge tone={t.isActive ? 'success' : 'neutral'}>{t.isActive ? 'Active' : 'Removed'}</Badge> },
            { header: 'Actions', cell: (t) => (
              <span className="flex">
                <Button variant="ghost" size="icon" aria-label={`View ${t.name}`} onClick={() => setViewing(t)}><Eye aria-hidden className="size-4" /></Button>
                <Button variant="ghost" size="icon" aria-label={`Edit ${t.name}`} onClick={() => setEditing(t)}><Pencil aria-hidden className="size-4" /></Button>
                {t.isActive
                  ? <Button variant="ghost" size="icon" aria-label={`Remove ${t.name}`} onClick={() => setRemoving(t)}><Trash2 aria-hidden className="size-4" /></Button>
                  : <Button variant="ghost" size="icon" aria-label={`Reinstate ${t.name}`} onClick={() => setActive.mutate({ t, active: true })}><RotateCcw aria-hidden className="size-4" /></Button>}
              </span>
            ) },
          ]} />
          <Pagination {...paged} noun="teachers" />
        </>
      )}
      {editing && <TeacherModal existing={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {viewing && <TeacherDetail teacher={viewing} onClose={() => setViewing(null)} />}
      <ConfirmDialog open={!!removing} onOpenChange={(o) => !o && setRemoving(null)} subject={removing?.name ?? ''} actionLabel="Remove"
        loading={setActive.isPending} onConfirm={() => removing && setActive.mutate({ t: removing, active: false })} />
    </>
  );
}

export function SearchBox({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  return (
    <label className="relative block min-w-0 flex-1 basis-60">
      <span className="sr-only">{label}</span>
      <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-text-secondary" />
      <input type="search" value={value} onChange={(e) => onChange(e.target.value)} placeholder={label}
        className="h-10 w-full rounded-md border border-border bg-surface pl-9 pr-3 text-body-sm text-text-primary placeholder:text-text-secondary outline-none focus:border-primary focus:ring-2 focus:ring-primary-subtle-bg-strong" />
    </label>
  );
}

function TeacherModal({ existing, onClose }: { existing: Teacher | null; onClose: () => void }) {
  const qc = useQueryClient();
  const [f, setF] = useState({
    name: existing?.name ?? '', contactEmail: existing?.contactEmail ?? '', telephone: existing?.telephone ?? '',
    gender: existing?.gender ?? '', address: existing?.address ?? '', joiningDate: existing?.joiningDate ?? '',
  });
  const [error, setError] = useState<string>();
  const [saving, setSaving] = useState(false);
  const [touched, setTouched] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setTouched(true);
    if (emailError(f.contactEmail)) return;
    setError(undefined); setSaving(true);
    try {
      if (existing) await api.put(`/teachers/${existing.id}`, f); else await api.post('/teachers', f);
      toast.success(existing ? 'Teacher updated' : 'Teacher added');
      void qc.invalidateQueries({ queryKey: ['teachers'] });
      onClose();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={existing ? 'Edit teacher' : 'Add teacher'} size="lg">
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2"><ErrorBanner>{error}</ErrorBanner></div>
        <TextField label="Full name" required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} />
        <TextField label="Email" type="email" required value={f.contactEmail} onBlur={() => setTouched(true)} error={touched ? emailError(f.contactEmail) : undefined} onChange={(e) => setF({ ...f, contactEmail: e.target.value })} />
        <TextField label="Phone" type="tel" required value={f.telephone} onChange={(e) => setF({ ...f, telephone: e.target.value })} />
        <SelectField label="Gender" required value={f.gender} onChange={(e) => setF({ ...f, gender: e.target.value })}>
          <option value="">Select gender</option><option>Female</option><option>Male</option><option>Other</option>
        </SelectField>
        <TextField className="sm:col-span-2" label="Address" required value={f.address} onChange={(e) => setF({ ...f, address: e.target.value })} />
        <TextField label="Joining date" type="date" required value={f.joiningDate} onChange={(e) => setF({ ...f, joiningDate: e.target.value })} />
        <div className="flex justify-end gap-2 pt-2 sm:col-span-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={saving}>{existing ? 'Save changes' : 'Add teacher'}</Button>
        </div>
      </form>
    </Modal>
  );
}

/** Detail view with the FR-022 subject-teacher-class assignment. */
function TeacherDetail({ teacher, onClose }: { teacher: Teacher; onClose: () => void }) {
  const subjects = useSubjects();
  const classes = useClasses();
  const [subjectId, setSubjectId] = useState('');
  const [classId, setClassId] = useState('');
  const assign = useMutation({
    mutationFn: () => api.post(`/subjects/${subjectId}/teacher-assignments`, { teacherId: teacher.id, classId }),
    onSuccess: () => { toast.success('Subject assigned'); setSubjectId(''); setClassId(''); },
    onError: (e) => toast.error(errorMessage(e)),
  });
  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={teacher.name} description={teacher.contactEmail} size="lg">
      <dl className="grid gap-4 text-body-sm sm:grid-cols-2">
        {[['Phone', teacher.telephone], ['Gender', teacher.gender], ['Address', teacher.address], ['Joining date', formatDate(teacher.joiningDate)], ['Status', teacher.isActive ? 'Active' : 'Removed']].map(([k, v]) => (
          <div key={k}><dt className="text-text-secondary">{k}</dt><dd className="font-medium text-text-primary">{v}</dd></div>
        ))}
      </dl>
      {teacher.isActive && (
        <form className="mt-6 border-t border-border-subtle pt-5" onSubmit={(e) => { e.preventDefault(); assign.mutate(); }}>
          <h3 className="font-bold text-text-primary">Assign a subject</h3>
          <div className="mt-3 grid gap-4 sm:grid-cols-2">
            <SelectField label="Subject" required value={subjectId} onChange={(e) => setSubjectId(e.target.value)}>
              <option value="">Select subject</option>
              {(subjects.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </SelectField>
            <SelectField label="Class" required value={classId} onChange={(e) => setClassId(e.target.value)}>
              <option value="">Select class</option>
              {(classes.data ?? []).map((c) => <option key={c.id} value={c.id}>{classLabel(c)}</option>)}
            </SelectField>
          </div>
          <div className="mt-4 flex justify-end"><Button type="submit" loading={assign.isPending}>Assign</Button></div>
        </form>
      )}
    </Modal>
  );
}
