import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useSession } from '@/auth/session';
import { BarChart } from '@/components/charts';
import { Badge, Button, Card, EmptyState, ErrorBanner, LoadingState, Modal, PageHeader, SelectField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, ApiError, errorMessage } from '@/lib/api';
import { classLabel, useClasses, useSubjects, useTeachers } from '@/lib/queries';
import { isLeadership } from '@/lib/roles';
import type { DayOfWeekSchool, TimetablePeriod } from '@/lib/types';

// UI-016 — Schedule (API-053–055). Leadership maintains (BR-056); every other institution role views
// published periods only. Conflicts are blocking at save and ALL are reported together (ERR-701/702/703).
// Q-236: names of classes/subjects/teachers are only readable by the roles the API serves them to;
// the teaching-load chart is therefore leadership-only (filed).
const DAYS: DayOfWeekSchool[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export function SchedulePage() {
  const role = useSession((s) => s.user?.role);
  const leader = isLeadership(role);
  const classes = useClasses();
  const subjects = useSubjects();
  const teachers = useTeachers();
  const [classId, setClassId] = useState('');
  const [adding, setAdding] = useState(false);
  const qc = useQueryClient();
  const periods = useQuery({
    queryKey: ['timetable', classId],
    queryFn: () => api.get<TimetablePeriod[]>(`/timetable${classId ? `?classId=${classId}` : ''}`),
  });
  const publish = useMutation({
    mutationFn: (p: TimetablePeriod) => api.post(`/timetable/${p.id}/publish`),
    onSuccess: () => { toast.success('Period published'); void qc.invalidateQueries({ queryKey: ['timetable'] }); },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const name = {
    cls: (id: string) => { const c = classes.data?.find((x) => x.id === id); return c ? classLabel(c) : undefined; },
    subject: (id: string) => subjects.data?.find((x) => x.id === id)?.name,
    teacher: (id: string) => teachers.data?.find((x) => x.id === id)?.name,
  };

  const data = useMemo(() => periods.data ?? [], [periods.data]);
  const slots = useMemo(() => [...new Set(data.map((p) => p.timeSlot))].sort(), [data]);
  const load = useMemo(() => {
    const m = new Map<string, number>();
    data.forEach((p) => m.set(p.teacherId, (m.get(p.teacherId) ?? 0) + 1));
    return [...m.entries()].map(([id, n]) => ({ label: name.teacher(id) ?? 'Teacher', value: n }));
  }, [data, teachers.data]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <>
      <PageHeader title="Schedule" description={leader ? 'Build and publish the weekly timetable.' : 'Your published weekly timetable.'}
        actions={leader && <Button onClick={() => setAdding(true)}><Plus aria-hidden className="size-4" />Add Schedule</Button>} />
      {(classes.data?.length ?? 0) > 0 && (
        <SelectField className="mb-6 max-w-xs" label="Class" value={classId} onChange={(e) => setClassId(e.target.value)}>
          <option value="">All classes</option>
          {classes.data!.map((c) => <option key={c.id} value={c.id}>{classLabel(c)}</option>)}
        </SelectField>
      )}
      {leader && <Card className="mb-6 p-6"><h3 className="font-bold text-text-primary">Teaching Load by Staff</h3><BarChart title="Periods per teacher" data={load} /></Card>}
      {periods.isPending && <LoadingState label="Loading timetable" rows={5} />}
      {periods.isError && <ErrorBanner>{errorMessage(periods.error)}</ErrorBanner>}
      {periods.isSuccess && data.length === 0 && <EmptyState title="No periods scheduled yet" description={leader ? 'Add a period to start the timetable.' : undefined} />}
      {data.length > 0 && (
        <div tabIndex={0} role="region" aria-label="Weekly timetable by day and time slot" className="overflow-x-auto rounded-lg border border-border bg-surface shadow-card focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary">
          <table className="w-full min-w-[760px] text-body-sm">
            <caption className="sr-only">Weekly timetable by day and time slot</caption>
            <thead className="border-b border-border bg-background text-text-secondary">
              <tr><th scope="col" className="px-3 py-3 text-left font-semibold">Time</th>{DAYS.map((d) => <th key={d} scope="col" className="px-3 py-3 text-left font-semibold">{d}</th>)}</tr>
            </thead>
            <tbody className="divide-y divide-border-subtle">
              {slots.map((slot) => (
                <tr key={slot}>
                  <th scope="row" className="whitespace-nowrap px-3 py-3 text-left font-semibold text-text-primary">{slot}</th>
                  {DAYS.map((day) => (
                    <td key={day} className="px-2 py-2 align-top">
                      {data.filter((p) => p.dayOfWeek === day && p.timeSlot === slot).map((p) => (
                        <div key={p.id} className="mb-1.5 rounded-md bg-primary-subtle-bg px-2.5 py-2 text-caption text-primary-subtle-text">
                          <p className="font-semibold">{name.subject(p.subjectId) ?? 'Period'}</p>
                          {name.cls(p.classId) && <p>{name.cls(p.classId)}</p>}
                          {name.teacher(p.teacherId) && <p>{name.teacher(p.teacherId)}</p>}
                          <p>Room {p.room}</p>
                          {leader && (p.isPublished
                            ? <span className="mt-1 inline-block"><Badge tone="success">Published</Badge></span>
                            : <button type="button" className="mt-1 font-semibold underline" onClick={() => publish.mutate(p)}>Publish<span className="sr-only"> period {slot} {day}</span></button>)}
                        </div>
                      ))}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {adding && <PeriodModal defaultClassId={classId} onClose={() => setAdding(false)} />}
    </>
  );
}

function PeriodModal({ defaultClassId, onClose }: { defaultClassId: string; onClose: () => void }) {
  const classes = useClasses();
  const subjects = useSubjects();
  const teachers = useTeachers();
  const qc = useQueryClient();
  const [f, setF] = useState({ classId: defaultClassId, subjectId: '', teacherId: '', room: '', dayOfWeek: 'Monday', timeSlot: '' });
  const [errors, setErrors] = useState<string[]>([]);
  const save = useMutation({
    mutationFn: () => api.post('/timetable', f),
    onSuccess: () => { toast.success('Period added'); void qc.invalidateQueries({ queryKey: ['timetable'] }); onClose(); },
    onError: (e) => setErrors(e instanceof ApiError && e.errors.length ? e.errors.map((x) => x.message) : [errorMessage(e)]),
  });
  const set = (k: keyof typeof f) => (e: { target: { value: string } }) => setF({ ...f, [k]: e.target.value });
  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title="Add schedule" size="lg">
      <form className="grid gap-4 sm:grid-cols-2" onSubmit={(e: FormEvent) => { e.preventDefault(); setErrors([]); save.mutate(); }}>
        {errors.length > 0 && (
          <div role="alert" className="rounded-md border border-error bg-error-subtle-bg px-4 py-3 text-body-sm text-text-primary sm:col-span-2">
            <p className="font-semibold">This period can’t be saved:</p>
            <ul className="mt-1 list-disc pl-5">{errors.map((m) => <li key={m}>{m}</li>)}</ul>
          </div>
        )}
        <SelectField label="Class" required value={f.classId} onChange={set('classId')}>
          <option value="">Select class</option>{(classes.data ?? []).map((c) => <option key={c.id} value={c.id}>{classLabel(c)}</option>)}
        </SelectField>
        <SelectField label="Subject" required value={f.subjectId} onChange={set('subjectId')}>
          <option value="">Select subject</option>{(subjects.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </SelectField>
        <SelectField label="Teacher" required value={f.teacherId} onChange={set('teacherId')}>
          <option value="">Select teacher</option>{(teachers.data ?? []).filter((t) => t.isActive).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </SelectField>
        <TextField label="Room" required value={f.room} onChange={set('room')} />
        <SelectField label="Day" required value={f.dayOfWeek} onChange={set('dayOfWeek')}>{DAYS.map((d) => <option key={d}>{d}</option>)}</SelectField>
        <TextField label="Time slot" required placeholder="09:00-09:45" value={f.timeSlot} onChange={set('timeSlot')} hint="HH:mm-HH:mm" />
        <div className="flex justify-end gap-2 sm:col-span-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={save.isPending}>Add period</Button>
        </div>
      </form>
    </Modal>
  );
}
