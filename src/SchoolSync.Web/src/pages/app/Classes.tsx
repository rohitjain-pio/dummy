import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { useSession } from '@/auth/session';
import { Avatar, Button, Card, ConfirmDialog, EmptyState, ErrorBanner, LoadingState, Modal, PageHeader, SelectField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, ApiError, errorMessage } from '@/lib/api';
import { classLabel, useClasses, useStudents, useTeachers } from '@/lib/queries';
import { isLeadership } from '@/lib/roles';
import type { SchoolClass, Student } from '@/lib/types';

// UI-007 — Classes (API-026–028). Leadership: full CRUD; Teacher: read only — the View/Edit/Delete
// icons and "+ Add New Class" are not rendered at all for Teacher (BR-029), not merely disabled.
// Q-236: per-class attendance rate/sparkline have no API and are not shown (filed).
export function ClassesPage() {
  const role = useSession((s) => s.user?.role);
  const leader = isLeadership(role);
  const classes = useClasses();
  const students = useStudents();
  const teachers = useTeachers();
  const [editing, setEditing] = useState<SchoolClass | 'new' | null>(null);
  const [deleting, setDeleting] = useState<SchoolClass | null>(null);
  const [roster, setRoster] = useState<SchoolClass | null>(null);
  const qc = useQueryClient();

  const remove = useMutation({
    mutationFn: (c: SchoolClass) => api.del(`/classes/${c.id}`),
    onSuccess: () => { toast.success('Class deleted'); setDeleting(null); void qc.invalidateQueries({ queryKey: ['classes'] }); },
    onError: (e) => { toast.error(errorMessage(e)); setDeleting(null); }, // ERR-306 students still placed
  });

  const countFor = (id: string) => (students.data ?? []).filter((s) => s.classId === id && s.isActive).length;
  const teacherName = (id: string | null) => (id ? teachers.data?.find((t) => t.id === id)?.name : undefined);

  return (
    <>
      <PageHeader title="Classes" description="Every class and section in your school."
        actions={leader && <Button onClick={() => setEditing('new')}><Plus aria-hidden className="size-4" />Add New Class</Button>} />
      {classes.isPending && <LoadingState label="Loading classes" variant="cards" rows={6} />}
      {classes.isError && <ErrorBanner>{errorMessage(classes.error)}</ErrorBanner>}
      {classes.isSuccess && classes.data.length === 0 && <EmptyState title="No classes yet" description={leader ? 'Add your first class to get started.' : undefined} />}
      {classes.isSuccess && classes.data.length > 0 && (
        <ul className="grid gap-6 sm:grid-cols-2 xl:grid-cols-3">
          {classes.data.map((c) => {
            const tName = teacherName(c.classTeacherId);
            return (
              <Card as="li" key={c.id} className="p-5">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <h3 className="font-bold text-text-primary">{classLabel(c)}</h3>
                    <p className="mt-0.5 text-body-sm text-text-secondary">{students.isSuccess ? `${countFor(c.id)} students` : '—'}</p>
                  </div>
                  {leader && (
                    <div className="flex">
                      <Button variant="ghost" size="icon" aria-label={`View roster of ${classLabel(c)}`} onClick={() => setRoster(c)}><Eye aria-hidden className="size-4" /></Button>
                      <Button variant="ghost" size="icon" aria-label={`Edit ${classLabel(c)}`} onClick={() => setEditing(c)}><Pencil aria-hidden className="size-4" /></Button>
                      <Button variant="ghost" size="icon" aria-label={`Delete ${classLabel(c)}`} onClick={() => setDeleting(c)}><Trash2 aria-hidden className="size-4" /></Button>
                    </div>
                  )}
                </div>
                <div className="mt-4 flex items-center gap-2.5">
                  {tName ? <><Avatar name={tName} /><span className="text-body-sm text-text-primary">{tName}</span></>
                    : <span className="text-body-sm text-text-secondary">{c.classTeacherId ? 'Class teacher assigned' : 'No class teacher'}</span>}
                </div>
                <div className="mt-5 flex flex-wrap gap-2">
                  <Link to={`/dashboard/attendance?classId=${c.id}`} className="inline-flex h-8 items-center rounded-md bg-primary px-3 text-body-sm font-semibold text-white hover:bg-primary-hover">Take Attendance</Link>
                  <Button size="sm" variant="outline" onClick={() => setRoster(c)}>View Roster</Button>
                </div>
              </Card>
            );
          })}
        </ul>
      )}
      {editing && <ClassModal existing={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {roster && <RosterModal cls={roster} onClose={() => setRoster(null)} />}
      <ConfirmDialog open={!!deleting} onOpenChange={(o) => !o && setDeleting(null)} subject={deleting ? classLabel(deleting) : ''} loading={remove.isPending} onConfirm={() => deleting && remove.mutate(deleting)} />
    </>
  );
}

function ClassModal({ existing, onClose }: { existing: SchoolClass | null; onClose: () => void }) {
  const teachers = useTeachers();
  const qc = useQueryClient();
  const [name, setName] = useState(existing?.name ?? '');
  const [section, setSection] = useState(existing?.section ?? '');
  const [teacherId, setTeacherId] = useState(existing?.classTeacherId ?? '');
  const [error, setError] = useState<string>();
  const [fieldError, setFieldError] = useState<string>();
  const [saving, setSaving] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(undefined); setFieldError(undefined); setSaving(true);
    try {
      const saved = existing
        ? await api.put<SchoolClass>(`/classes/${existing.id}`, { name, section })
        : await api.post<SchoolClass>('/classes', { name, section });
      if ((teacherId || null) !== (existing?.classTeacherId ?? null)) {
        await api.put(`/classes/${saved.id}/class-teacher`, { teacherId: teacherId || null }); // FR-026
      }
      toast.success(existing ? 'Class updated' : 'Class created');
      void qc.invalidateQueries({ queryKey: ['classes'] });
      onClose();
    } catch (err) {
      if (err instanceof ApiError && err.code === 'ERR-305') setFieldError(err.message); // inline field error
      else setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={existing ? 'Edit class' : 'Add new class'}>
      <form onSubmit={submit} className="space-y-4">
        <ErrorBanner>{error}</ErrorBanner>
        <TextField label="Class name" required value={name} onChange={(e) => setName(e.target.value)} error={fieldError} />
        <TextField label="Section" required value={section} onChange={(e) => setSection(e.target.value)} />
        <SelectField label="Class teacher" value={teacherId} onChange={(e) => setTeacherId(e.target.value)}>
          <option value="">No class teacher</option>
          {(teachers.data ?? []).filter((t) => t.isActive).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </SelectField>
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={saving}>{existing ? 'Save changes' : 'Create class'}</Button>
        </div>
      </form>
    </Modal>
  );
}

function RosterModal({ cls, onClose }: { cls: SchoolClass; onClose: () => void }) {
  const roster = useQuery({ queryKey: ['roster', cls.id], queryFn: () => api.get<Student[]>(`/classes/${cls.id}/roster`) });
  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={`Roster — ${classLabel(cls)}`} size="lg">
      {roster.isPending && <LoadingState label="Loading roster" />}
      {roster.isError && <ErrorBanner>{errorMessage(roster.error)}</ErrorBanner>}
      {roster.isSuccess && (roster.data.length === 0
        ? <p className="text-body-sm text-text-secondary">This class has no students enrolled yet.</p>
        : (
          <ul className="divide-y divide-border-subtle">
            {roster.data.map((s) => (
              <li key={s.id} className="flex items-center justify-between gap-4 py-2.5">
                <span className="flex items-center gap-2.5"><Avatar name={s.name} /><span className="text-body-sm font-medium text-text-primary">{s.name}</span></span>
                <span className="text-caption text-text-secondary">Roll {s.rollNumber}</span>
              </li>
            ))}
          </ul>
        ))}
    </Modal>
  );
}
