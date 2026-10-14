import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { useSession } from '@/auth/session';
import { AreaChart, BarChart, DonutChart } from '@/components/charts';
import { Card, EmptyState, ErrorBanner, LoadingState, PageHeader, StatTile, StatusPill } from '@/components/ui';
import { api, ApiError, errorMessage } from '@/lib/api';
import { formatDate } from '@/lib/queries';
import { isLeadership } from '@/lib/roles';
import type { AnnouncementSummary, Dashboard, PlatformSummary } from '@/lib/types';

// UI-015 — Dashboard (API-048). Content varies by role; the same card/chart family at every scope.
// Dropped per spec: the mockup's "Upcoming Features" roadmap panel.
export function DashboardPage() {
  const user = useSession((s) => s.user)!;
  if (user.role === 'PlatformOperator') return <PlatformDashboard />;
  return <InstitutionDashboard />;
}

const pct = (v: number | undefined) => (v === undefined ? '—' : `${v}%`);

function InstitutionDashboard() {
  const user = useSession((s) => s.user)!;
  const q = useQuery({ queryKey: ['dashboard'], queryFn: () => api.get<Dashboard>('/dashboard') });

  if (q.isPending) return <><PageHeader title={`Welcome, ${user.username}`} /><LoadingState label="Loading dashboard" variant="tiles" rows={4} /><div className="mt-6"><LoadingState variant="cards" rows={2} /></div></>;
  if (q.isError) {
    const planGate = q.error instanceof ApiError && q.error.code === 'ERR-802';
    return (
      <>
        <PageHeader title={`Welcome, ${user.username}`} />
        <ErrorBanner>{planGate ? 'Parent access isn’t included in your school’s current plan. Please contact your school.' : errorMessage(q.error)}</ErrorBanner>
      </>
    );
  }
  const d = q.data;
  const heading = <PageHeader title={`Welcome, ${user.username}`} description={formatDate(d.today)}
    actions={isLeadership(user.role) || user.role === 'Teacher' ? (
      <>
        <Link to="/dashboard/attendance" className="inline-flex h-10 items-center rounded-md bg-primary px-4 text-body-sm font-semibold text-white hover:bg-primary-hover">Take Attendance</Link>
        <Link to="/dashboard/announcements?new=1" className="inline-flex h-10 items-center rounded-md border border-border bg-surface px-4 text-body-sm font-semibold text-text-primary hover:bg-background">New Announcement</Link>
      </>
    ) : undefined} />;

  if (d.variant === 'Student' || d.variant === 'Parent') return <>{heading}<PersonalVariant d={d} /></>;

  const trendLabels = (d.attendanceTrend ?? []).map((t) => t.date);
  const students = d.totalStudents ?? 0;
  const presentStudents = d.presentStudentsToday ?? 0;
  return (
    <>
      {heading}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {d.variant === 'Leadership' ? (
          <>
            <StatTile label="Total Teachers" value={d.totalTeachers ?? 0} />
            <StatTile label="Present Teachers" value={d.presentTeachersToday ?? 0} hint="Today" />
          </>
        ) : (
          <>
            <StatTile label="My Classes" value={d.totalClasses ?? 0} />
            <StatTile label="Average Attendance" value={pct(d.averageAttendancePercent)} hint="Last 7 days" />
          </>
        )}
        <StatTile label="Total Students" value={students} />
        <StatTile label="Present Students" value={presentStudents} hint="Today" />
      </div>
      <div className="mt-6 grid gap-6 xl:grid-cols-3">
        <Card className="p-6 xl:col-span-2">
          <h3 className="font-bold text-text-primary">Attendance Trend</h3>
          <AreaChart title="Attendance trend, last 7 days" labels={trendLabels}
            series={[
              { name: 'Students', values: (d.attendanceTrend ?? []).map((t) => t.studentAttendancePercent) },
              ...(d.variant === 'Leadership' ? [{ name: 'Teachers', values: (d.attendanceTrend ?? []).map((t) => t.teacherAttendancePercent) }] : []),
            ]} />
        </Card>
        <Card className="p-6">
          <h3 className="font-bold text-text-primary">Today&apos;s Attendance</h3>
          <DonutChart title="Today's student attendance" data={students === 0 ? [] : [{ label: 'Present', value: presentStudents }, { label: 'Not present or not marked', value: Math.max(0, students - presentStudents) }]} />
        </Card>
        <Card className="p-6 xl:col-span-2">
          <h3 className="font-bold text-text-primary">Students per Class</h3>
          <BarChart title="Students per class" data={(d.studentsPerClass ?? []).map((c) => ({ label: `${c.name} ${c.section}`, value: c.students }))} />
        </Card>
        <Card className="p-6">
          <h3 className="font-bold text-text-primary">Gender Split</h3>
          <DonutChart title="Student gender split" data={(d.genderSplit ?? []).map((g) => ({ label: g.gender, value: g.students }))} />
        </Card>
      </div>
      <AnnouncementLists recent={d.recentAnnouncements} upcoming={d.upcomingEvents} />
    </>
  );
}

function PersonalVariant({ d }: { d: Dashboard }) {
  const children = d.children ?? [];
  const [selected, setSelected] = useState(0);
  const child = children[selected];
  return (
    <>
      {d.variant === 'Parent' && children.length > 1 && (
        <div role="tablist" aria-label="Choose child" className="mb-4 flex flex-wrap gap-2">
          {children.map((c, i) => (
            <button key={c.studentId} role="tab" aria-selected={i === selected} onClick={() => setSelected(i)}
              className={i === selected ? 'h-9 rounded-full bg-primary px-4 text-body-sm font-semibold text-white' : 'h-9 rounded-full border border-border bg-surface px-4 text-body-sm font-semibold text-text-primary'}>
              {c.name}
            </button>
          ))}
        </div>
      )}
      {!child ? (
        <EmptyState title={d.variant === 'Parent' ? 'No linked children yet' : 'Your student record isn’t linked yet'}
          description={d.variant === 'Parent' ? 'Ask your school for a parent link code to connect your child’s records.' : 'Your school will link your account to your student record.'} />
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <StatTile label={d.variant === 'Parent' ? `${child.name} — attendance` : 'My attendance'} value={pct(child.attendancePercent)} />
          <Card className="px-4 py-4">
            <p className="text-micro-label uppercase text-text-secondary">Today</p>
            <p className="mt-3">{child.todayOutcome ? <StatusPill outcome={child.todayOutcome} /> : <span className="text-body-sm text-text-secondary">Not marked yet</span>}</p>
          </Card>
        </div>
      )}
      <AnnouncementLists recent={d.recentAnnouncements} upcoming={d.upcomingEvents} />
    </>
  );
}

function AnnouncementLists({ recent, upcoming }: { recent: AnnouncementSummary[]; upcoming: AnnouncementSummary[] }) {
  const list = (items: AnnouncementSummary[], empty: string) =>
    items.length === 0 ? <p className="mt-3 text-body-sm text-text-secondary">{empty}</p> : (
      <ul className="mt-3 divide-y divide-border-subtle">
        {items.map((a) => (
          <li key={a.id} className="flex items-center justify-between gap-4 py-2.5">
            <Link to="/dashboard/announcements" className="text-body-sm font-medium text-text-primary hover:text-primary">{a.title}</Link>
            <span className="shrink-0 text-caption text-text-secondary">{formatDate(a.effectiveDate)}</span>
          </li>
        ))}
      </ul>
    );
  return (
    <div className="mt-6 grid gap-6 lg:grid-cols-2">
      <Card className="p-6"><h3 className="font-bold text-text-primary">Recent Announcements</h3>{list(recent, 'No announcements yet.')}</Card>
      <Card className="p-6"><h3 className="font-bold text-text-primary">Upcoming Events</h3>{list(upcoming, 'Nothing scheduled.')}</Card>
    </div>
  );
}

function PlatformDashboard() {
  const q = useQuery({ queryKey: ['platform-summary'], queryFn: () => api.get<PlatformSummary>('/platform/summary', { anonymous: true }) });
  return (
    <>
      <PageHeader title="Platform overview" description="Figures across every institution on SchoolSync." />
      {q.isPending && <LoadingState label="Loading platform summary" variant="tiles" rows={4} />}
      {q.isError && <ErrorBanner>{errorMessage(q.error)}</ErrorBanner>}
      {q.isSuccess && (
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatTile label="Institutions" value={q.data.institutions} />
          <StatTile label="Students" value={q.data.students} />
          <StatTile label="Teachers" value={q.data.teachers} />
          <StatTile label="Classes" value={q.data.classes} />
        </div>
      )}
    </>
  );
}
