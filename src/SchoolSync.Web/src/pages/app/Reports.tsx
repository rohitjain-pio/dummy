import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileText, History } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { BarChart } from '@/components/charts';
import { Badge, Button, Card, EmptyState, ErrorBanner, LoadingState, Modal, PageHeader, SelectField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, errorMessage, idempotencyKey } from '@/lib/api';
import { classLabel, localDate, parseUtc, saveBlob, useClasses } from '@/lib/queries';
import type { AttendanceRecord, ReportRun, ReportType } from '@/lib/types';

// UI-017 — Reports (API-050–052). Leadership: all classes; Teacher: own classes (BR-051, server-enforced).
// Binding exclusion: NO "Student Satisfaction" / "Course Completion" gauges — omitted entirely.
// Q-236: attendance-vs-performance, grade-distribution and subject-engagement charts need performance
// data no API holds — not shown (filed). Formats: CSV only (Q-226; Excel/PDF not offered).
const REPORTS: { type: ReportType; title: string; body: string }[] = [
  { type: 'StudentPerformance', title: 'Student Performance', body: 'Per-student attendance summary for a date range.' },
  { type: 'ClassAttendance', title: 'Class Attendance', body: 'Day-by-day attendance for a class or the whole school.' },
  { type: 'CourseAnalytics', title: 'Course Analytics', body: 'Attendance across classes and sections.' },
  { type: 'TeacherPerformance', title: 'Teacher Performance', body: 'Staff attendance over a date range.' },
];
const TITLE = Object.fromEntries(REPORTS.map((r) => [r.type, r.title])) as Record<ReportType, string>;

export function ReportsPage() {
  const classes = useClasses();
  const runs = useQuery({ queryKey: ['reports'], queryFn: () => api.get<ReportRun[]>('/reports') });
  const [generating, setGenerating] = useState<ReportType | null>(null);
  const [historyFor, setHistoryFor] = useState<ReportType | null>(null);

  const perClass = useQueries({
    queries: (classes.data ?? []).map((c) => ({
      queryKey: ['attendance', c.id, 'history'],
      queryFn: () => api.get<AttendanceRecord[]>(`/attendance?classId=${c.id}`),
    })),
  });
  const byClass = (classes.data ?? []).map((c, i) => {
    const recs = (perClass[i]?.data ?? []).filter((r) => r.personType === 'Student');
    const present = recs.filter((r) => r.outcome === 'Present' || r.outcome === 'Late').length;
    return { label: `${c.name} ${c.section}`, value: recs.length ? Math.round((present / recs.length) * 100) : 0, has: recs.length > 0 };
  }).filter((d) => d.has);

  return (
    <>
      <PageHeader title="Reports" description="Generate and download attendance reports." />
      <Card className="mb-6 p-6">
        <h3 className="font-bold text-text-primary">Attendance by Class</h3>
        {classes.isPending ? <LoadingState rows={2} /> : <BarChart title="Attendance rate by class" unit="%" data={byClass.map(({ label, value }) => ({ label, value }))} />}
      </Card>
      <ul className="grid gap-6 sm:grid-cols-2 xl:grid-cols-4">
        {REPORTS.map((r) => (
          <Card as="li" key={r.type} className="flex flex-col p-5">
            <span className="grid size-10 place-items-center rounded-md bg-primary-subtle-bg text-primary"><FileText aria-hidden className="size-5" /></span>
            <h3 className="mt-4 font-bold text-text-primary">{r.title}</h3>
            <p className="mt-1 flex-1 text-body-sm text-text-secondary">{r.body}</p>
            <div className="mt-4 flex flex-wrap gap-2">
              <Button size="sm" aria-label={`Generate ${r.title} report`} onClick={() => setGenerating(r.type)}>Generate</Button>
              <Button size="sm" variant="outline" aria-label={`View history for ${r.title}`} onClick={() => setHistoryFor(r.type)}><History aria-hidden className="size-4" />View History</Button>
            </div>
          </Card>
        ))}
      </ul>
      {runs.isError && <div className="mt-6"><ErrorBanner>{errorMessage(runs.error)}</ErrorBanner></div>}
      {generating && <GenerateModal type={generating} onClose={() => setGenerating(null)} />}
      {historyFor && (
        <Modal open onOpenChange={(o) => !o && setHistoryFor(null)} title={`${TITLE[historyFor]} — history`} size="lg">
          <RunList runs={(runs.data ?? []).filter((r) => r.reportType === historyFor)} loading={runs.isPending} classes={classes.data ?? []} />
        </Modal>
      )}
    </>
  );
}

async function download(run: ReportRun) {
  try {
    const res = await api.get<Response>(`/reports/${run.id}/download`, { raw: true });
    saveBlob(await res.blob(), `${run.reportType}-${run.dateRange.from}-${run.dateRange.to}.csv`);
  } catch (e) {
    toast.error(errorMessage(e));
  }
}

function RunList({ runs, loading, classes }: { runs: ReportRun[]; loading: boolean; classes: { id: string; name: string; section: string }[] }) {
  if (loading) return <LoadingState label="Loading report history" />;
  if (runs.length === 0) return <EmptyState title="No reports generated yet" />;
  return (
    <ul className="divide-y divide-border-subtle">
      {[...runs].sort((a, b) => b.requestedAtUtc.localeCompare(a.requestedAtUtc)).map((r) => {
        const cls = classes.find((c) => c.id === r.scope.classId);
        return (
          <li key={r.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
            <div className="text-body-sm">
              <p className="font-medium text-text-primary">{r.dateRange.from} → {r.dateRange.to} · {cls ? classLabel(cls) : 'Whole school'}</p>
              <p className="text-caption text-text-secondary">Requested {parseUtc(r.requestedAtUtc).toLocaleString('en-IN')}</p>
            </div>
            {r.status === 'Ready' && <Button size="sm" variant="outline" onClick={() => void download(r)}><Download aria-hidden className="size-4" />Download CSV</Button>}
            {r.status === 'Pending' && <Badge tone="warning">Generating…</Badge>}
            {r.status === 'Failed' && <Badge tone="neutral">Failed{r.failureReason ? `: ${r.failureReason}` : ''}</Badge>}
          </li>
        );
      })}
    </ul>
  );
}

function GenerateModal({ type, onClose }: { type: ReportType; onClose: () => void }) {
  const classes = useClasses();
  const qc = useQueryClient();
  const today = localDate();
  const [from, setFrom] = useState(today.slice(0, 8) + '01');
  const [to, setTo] = useState(today);
  const [classId, setClassId] = useState('');
  const [runId, setRunId] = useState<string>();
  const [error, setError] = useState<string>();
  const [key] = useState(idempotencyKey);

  const request = useMutation({
    mutationFn: () => api.post<{ reportRunId: string }>('/reports', { reportType: type, dateRange: { from, to }, scope: { classId: classId || null }, format: 'CSV' }, { headers: key }),
    onSuccess: (r) => setRunId(r.reportRunId),
    onError: (e) => setError(errorMessage(e)),
  });
  // Generation is asynchronous (202 + Hangfire) — poll the run until it is Ready or Failed.
  const run = useQuery({
    queryKey: ['report-run', runId],
    queryFn: () => api.get<ReportRun>(`/reports/${runId}`),
    enabled: !!runId,
    refetchInterval: (q) => (q.state.data?.status === 'Pending' || !q.state.data ? 1500 : false),
  });
  useEffect(() => {
    if (run.data?.status === 'Ready') { toast.success('Report ready'); void qc.invalidateQueries({ queryKey: ['reports'] }); }
    if (run.data?.status === 'Failed') void qc.invalidateQueries({ queryKey: ['reports'] });
  }, [run.data?.status, qc]);

  const busy = request.isPending || (!!runId && (!run.data || run.data.status === 'Pending'));

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title="Generate report">
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); setError(undefined); if (to < from) { setError('The end date must be on or after the start date.'); return; } request.mutate(); }}>
        <ErrorBanner>{error}</ErrorBanner>
        <TextField label="Report type" value={TITLE[type]} readOnly />
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label="From" type="date" required value={from} max={to} onChange={(e) => setFrom(e.target.value)} />
          <TextField label="To" type="date" required value={to} min={from} onChange={(e) => setTo(e.target.value)} />
        </div>
        <SelectField label="Scope" value={classId} onChange={(e) => setClassId(e.target.value)}>
          <option value="">Whole school</option>
          {(classes.data ?? []).map((c) => <option key={c.id} value={c.id}>{classLabel(c)}</option>)}
        </SelectField>
        <SelectField label="Format" value="CSV" onChange={() => undefined}><option value="CSV">CSV</option></SelectField>
        <div role="status" aria-live="polite" className="text-body-sm">
          {busy && runId && <span className="text-text-secondary">Generating your report…</span>}
          {run.data?.status === 'Ready' && <span className="font-semibold text-success-subtle-text">Your report is ready.</span>}
          {run.data?.status === 'Failed' && <span className="text-error">The report could not be generated{run.data.failureReason ? `: ${run.data.failureReason}` : '.'}</span>}
        </div>
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>Close</Button>
          {run.data?.status === 'Ready'
            ? <Button onClick={() => void download(run.data!)}><Download aria-hidden className="size-4" />Download CSV</Button>
            : <Button type="submit" loading={busy} disabled={!!runId && run.data?.status !== 'Failed'}>Generate</Button>}
        </div>
      </form>
    </Modal>
  );
}
