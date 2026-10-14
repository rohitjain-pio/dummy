import * as RadioGroup from '@radix-ui/react-radio-group';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Lock } from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';
import { AreaChart, DonutChart } from '@/components/charts';
import { Badge, Button, Card, DataTable, EmptyState, ErrorBanner, LoadingState, Modal, PageHeader, SelectField, StatTile, StatusPill, TextareaField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, errorMessage } from '@/lib/api';
import { cn } from '@/lib/cn';
import { classLabel, downloadCsv, formatDate, localDate, useClasses, useStudents } from '@/lib/queries';
import type { AttendanceOutcome, AttendanceRecord, CorrectionRequest, RosterRow } from '@/lib/types';

// UI-010 — Attendance (API-031–036). Leadership + Teacher. NFR-002: open roster → toggle/bulk-mark → save.
const OUTCOMES: AttendanceOutcome[] = ['Present', 'Absent', 'Late', 'Leave'];

/** Whole days between an attendance date and today (both yyyy-MM-dd). Server stays authoritative (ERR-401/402). */
export function daysAgo(date: string, today: string) {
  const toUtc = (s: string) => { const [y, m, d] = s.split('-').map(Number); return Date.UTC(y!, m! - 1, d!); };
  return Math.round((toUtc(today) - toUtc(date)) / 86_400_000);
}

export type RowState = 'editable' | 'request-correction' | 'pending' | 'locked';
export function rowState(record: AttendanceRecord, today: string): RowState {
  if (record.locked || daysAgo(record.attendanceDate, today) >= 30) return 'locked'; // day 30+: read-only
  if (record.correctionStatus === 'CorrectionPending') return 'pending';
  return daysAgo(record.attendanceDate, today) === 0 ? 'editable' : 'request-correction'; // day 0 direct; day 1–29 request
}

export function AttendancePage() {
  const [params, setParams] = useSearchParams();
  const classes = useClasses();
  const students = useStudents();
  const today = localDate();
  const classId = params.get('classId') ?? classes.data?.[0]?.id ?? '';
  const [date, setDate] = useState(today);
  const [filling, setFilling] = useState(false);
  const [correcting, setCorrecting] = useState<AttendanceRecord | null>(null);

  const dayRecords = useQuery({
    queryKey: ['attendance', classId, date],
    queryFn: () => api.get<AttendanceRecord[]>(`/attendance?classId=${classId}&date=${date}`),
    enabled: !!classId,
  });
  const history = useQuery({
    queryKey: ['attendance', classId, 'history'],
    queryFn: () => api.get<AttendanceRecord[]>(`/attendance?classId=${classId}`),
    enabled: !!classId,
  });
  const roster = useQuery({
    queryKey: ['roster-marking', classId, date],
    queryFn: () => api.get<RosterRow[]>(`/attendance/roster?classId=${classId}&date=${date}`),
    enabled: !!classId,
  });

  const student = (id: string) => students.data?.find((s) => s.id === id);
  const records = (dayRecords.data ?? []).filter((r) => r.personType === 'Student');
  const count = (o: AttendanceOutcome) => records.filter((r) => r.outcome === o).length;

  const trend = useMemo(() => {
    const byDate = new Map<string, { present: number; total: number }>();
    for (const r of history.data ?? []) {
      if (r.personType !== 'Student') continue;
      const e = byDate.get(r.attendanceDate) ?? { present: 0, total: 0 };
      e.total++;
      if (r.outcome === 'Present' || r.outcome === 'Late') e.present++;
      byDate.set(r.attendanceDate, e);
    }
    const dates = [...byDate.keys()].sort().slice(-14);
    return { labels: dates, values: dates.map((d) => Math.round((byDate.get(d)!.present / byDate.get(d)!.total) * 1000) / 10) };
  }, [history.data]);

  const selectedClass = classes.data?.find((c) => c.id === classId);

  return (
    <>
      <PageHeader title="Attendance" description="Mark and review the class register."
        actions={<Button disabled={!classId || date > today} onClick={() => setFilling(true)}>Fill Attendance</Button>} />
      <div className="mb-6 flex flex-wrap items-end gap-4">
        <SelectField className="min-w-0 basis-60" label="Class" value={classId} onChange={(e) => setParams({ classId: e.target.value })}>
          {(classes.data ?? []).length === 0 && <option value="">No classes</option>}
          {(classes.data ?? []).map((c) => <option key={c.id} value={c.id}>{classLabel(c)}</option>)}
        </SelectField>
        <TextField className="basis-44" label="Date" type="date" max={today} value={date} onChange={(e) => setDate(e.target.value || today)} />
      </div>

      {classes.isSuccess && classes.data.length === 0 && <EmptyState title="No classes yet" description="Attendance is taken per class." />}
      {classId && (
        <>
          {(dayRecords.isPending || roster.isPending) ? <LoadingState label="Loading attendance" variant="tiles" rows={4} /> : (
            <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
              <StatTile label="Roster" value={roster.data?.length ?? 0} />
              <StatTile label="Present" value={<span className="text-success">{count('Present')}</span>} />
              <StatTile label="Absent" value={<span className="text-error">{count('Absent')}</span>} />
              <StatTile label="Late / Leave" value={<span className="text-warning-text">{count('Late') + count('Leave')}</span>} />
            </div>
          )}
          <div className="mt-6 grid gap-6 lg:grid-cols-3">
            <Card className="p-6 lg:col-span-2"><h3 className="font-bold text-text-primary">Attendance Over Time</h3>
              <AreaChart title="Student attendance rate by date" labels={trend.labels} series={[{ name: 'Present or late', values: trend.values }]} />
            </Card>
            <Card className="p-6"><h3 className="font-bold text-text-primary">Breakdown — {formatDate(date)}</h3>
              <DonutChart title="Attendance breakdown for the selected date" data={records.length ? OUTCOMES.map((o) => ({ label: o, value: count(o) })) : []} />
            </Card>
          </div>

          <div className="mt-6 mb-3 flex items-center justify-between gap-3">
            <h3 className="font-bold text-text-primary">Records — {selectedClass ? classLabel(selectedClass) : ''}, {formatDate(date)}</h3>
            <Button variant="outline" size="sm" disabled={records.length === 0}
              onClick={() => downloadCsv(`attendance-${date}.csv`, ['Roll number', 'Name', 'Date', 'Status'], records.map((r) => [student(r.personId)?.rollNumber, student(r.personId)?.name, r.attendanceDate, r.outcome]))}>
              <Download aria-hidden className="size-4" />Export
            </Button>
          </div>
          {dayRecords.isError && <ErrorBanner>{errorMessage(dayRecords.error)}</ErrorBanner>}
          {roster.isSuccess && roster.data.length === 0 && <EmptyState title="This class has no students enrolled yet" />}
          {roster.isSuccess && roster.data.length > 0 && records.length === 0 && dayRecords.isSuccess && (
            <EmptyState title="No attendance recorded for this date" action={date <= today ? <Button onClick={() => setFilling(true)}>Fill Attendance</Button> : undefined} />
          )}
          {records.length > 0 && (
            <DataTable caption="Attendance records" rows={records} rowKey={(r) => r.id} columns={[
              { header: 'Roll No.', cell: (r) => student(r.personId)?.rollNumber ?? '—' },
              { header: 'Name', cell: (r) => student(r.personId)?.name ?? roster.data?.find((x) => x.studentId === r.personId)?.name ?? '—' },
              { header: 'Date', cell: (r) => formatDate(r.attendanceDate) },
              { header: 'Status', cell: (r) => <StatusPill outcome={r.outcome} /> },
              { header: 'Changes', cell: (r) => {
                const state = rowState(r, today);
                if (state === 'locked') return <span className="inline-flex items-center gap-1 rounded-full bg-border-subtle px-2.5 py-0.5 text-caption font-semibold text-neutral-subtle-text"><Lock aria-hidden className="size-3" />Locked</span>;
                if (state === 'pending') return <Badge tone="warning">Pending approval</Badge>;
                if (state === 'editable') return <Button size="sm" variant="ghost" onClick={() => setFilling(true)}>Edit</Button>;
                return <Button size="sm" variant="outline" onClick={() => setCorrecting(r)}>Request correction</Button>;
              } },
            ]} />
          )}
        </>
      )}
      {filling && roster.data && selectedClass && (
        <FillAttendanceModal classId={classId} className={classLabel(selectedClass)} date={date} roster={roster.data} onClose={() => setFilling(false)} />
      )}
      {correcting && <CorrectionModal record={correcting} name={student(correcting.personId)?.name ?? 'this student'} onClose={() => setCorrecting(null)} />}
    </>
  );
}

function FillAttendanceModal({ classId, className, date, roster, onClose }: { classId: string; className: string; date: string; roster: RosterRow[]; onClose: () => void }) {
  const qc = useQueryClient();
  // BRL-034: every row opens with its current outcome, or NOTHING selected — never pre-selected "Absent".
  const [marks, setMarks] = useState<Record<string, AttendanceOutcome | undefined>>(() => Object.fromEntries(roster.map((r) => [r.studentId, r.currentOutcome ?? undefined])));
  const [error, setError] = useState<string>();
  const save = useMutation({
    mutationFn: () => api.put('/attendance', {
      classId, date,
      entries: Object.entries(marks).filter(([, o]) => o).map(([personId, outcome]) => ({ personId, personType: 'Student', outcome })),
    }),
    onSuccess: () => { toast.success('Attendance saved'); void qc.invalidateQueries({ queryKey: ['attendance'] }); void qc.invalidateQueries({ queryKey: ['roster-marking'] }); onClose(); },
    onError: (e) => setError(errorMessage(e)), // ERR-401 locked · ERR-403 future date
  });
  const markAll = (o: AttendanceOutcome) => setMarks(Object.fromEntries(roster.map((r) => [r.studentId, o])));
  const marked = Object.values(marks).filter(Boolean).length;

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={`Fill attendance — ${className}`} description={formatDate(date)} size="lg">
      <form onSubmit={(e: FormEvent) => { e.preventDefault(); setError(undefined); save.mutate(); }}>
        <ErrorBanner>{error}</ErrorBanner>
        <div className="mb-4 flex flex-wrap gap-2">
          <Button size="sm" variant="secondary" onClick={() => markAll('Present')}>Mark all Present</Button>
          <Button size="sm" variant="outline" onClick={() => markAll('Absent')}>Mark all Absent</Button>
        </div>
        <ul className="divide-y divide-border-subtle">
          {roster.map((r) => {
            const labelId = `att-${r.studentId}`;
            return (
              <li key={r.studentId} className="flex flex-wrap items-center justify-between gap-3 py-2.5">
                <span id={labelId} className="text-body-sm font-medium text-text-primary">{r.name}</span>
                {/* one outcome (or none yet) per person per date — a radio group, not four checkboxes (BR-031) */}
                <RadioGroup.Root aria-labelledby={labelId} value={marks[r.studentId] ?? ''} onValueChange={(v) => setMarks({ ...marks, [r.studentId]: v as AttendanceOutcome })} className="flex gap-1.5">
                  {OUTCOMES.map((o) => (
                    <RadioGroup.Item key={o} value={o} aria-label={o}
                      className={cn('h-8 rounded-full border px-3 text-caption font-semibold outline-none focus-visible:ring-2 focus-visible:ring-primary',
                        marks[r.studentId] === o
                          ? { Present: 'border-success bg-success-subtle-bg text-success-subtle-text', Absent: 'border-error bg-error-subtle-bg text-error-subtle-text', Late: 'border-warning-text bg-warning-bg text-warning-text', Leave: 'border-info bg-info-subtle-bg text-info-subtle-text' }[o]
                          : 'border-border bg-surface text-text-secondary hover:bg-background')}>
                      {o}
                    </RadioGroup.Item>
                  ))}
                </RadioGroup.Root>
              </li>
            );
          })}
        </ul>
        <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
          <p className="text-body-sm text-text-secondary" aria-live="polite">{marked} of {roster.length} marked</p>
          <div className="flex gap-2">
            <Button variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={save.isPending} disabled={marked === 0}>Save attendance</Button>
          </div>
        </div>
      </form>
    </Modal>
  );
}

function CorrectionModal({ record, name, onClose }: { record: AttendanceRecord; name: string; onClose: () => void }) {
  const qc = useQueryClient();
  const [outcome, setOutcome] = useState<AttendanceOutcome | ''>('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string>();
  const submit = useMutation({
    mutationFn: () => api.post<CorrectionRequest>(`/attendance/${record.id}/correction-requests`, { requestedOutcome: outcome, reason }),
    onSuccess: (r) => {
      toast.success(r.status === 'AppliedDirectly' ? 'Attendance updated' : 'Correction requested — awaiting approval');
      void qc.invalidateQueries({ queryKey: ['attendance'] });
      onClose();
    },
    onError: (e) => setError(errorMessage(e)),
  });
  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title="Request correction" description={`${name} · ${formatDate(record.attendanceDate)} · currently ${record.outcome}`}>
      <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); setError(undefined); submit.mutate(); }}>
        <ErrorBanner>{error}</ErrorBanner>
        <SelectField label="Corrected status" required value={outcome} onChange={(e) => setOutcome(e.target.value as AttendanceOutcome)}>
          <option value="">Select status</option>
          {OUTCOMES.filter((o) => o !== record.outcome).map((o) => <option key={o}>{o}</option>)}
        </SelectField>
        <TextareaField label="Reason" required rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={submit.isPending}>Submit request</Button>
        </div>
      </form>
    </Modal>
  );
}
