import { useQuery } from '@tanstack/react-query';
import { useSession } from '@/auth/session';
import { api } from './api';
import { isLeadership, LEADERSHIP_AND_TEACHER } from './roles';
import type { SchoolClass, Student, Subject, Teacher } from './types';

// Reference lists shared across screens. Each is only requested by roles the server will serve it
// to (e.g. GET /teachers is leadership-only) so no screen triggers a guaranteed 403.
export function useClasses() {
  const role = useSession((s) => s.user?.role);
  return useQuery({ queryKey: ['classes'], queryFn: () => api.get<SchoolClass[]>('/classes'), enabled: !!role && LEADERSHIP_AND_TEACHER.includes(role) });
}
export function useStudents() {
  const role = useSession((s) => s.user?.role);
  return useQuery({ queryKey: ['students'], queryFn: () => api.get<Student[]>('/students'), enabled: !!role && LEADERSHIP_AND_TEACHER.includes(role) });
}
export function useTeachers() {
  const role = useSession((s) => s.user?.role);
  return useQuery({ queryKey: ['teachers'], queryFn: () => api.get<Teacher[]>('/teachers'), enabled: isLeadership(role) });
}
export function useSubjects() {
  const role = useSession((s) => s.user?.role);
  return useQuery({ queryKey: ['subjects'], queryFn: () => api.get<Subject[]>('/subjects'), enabled: isLeadership(role) });
}

export const classLabel = (c: Pick<SchoolClass, 'name' | 'section'>) => `${c.name} – ${c.section}`;

/** Q-236: the API exposes no display names for other users, so people are labelled from what we know. */
export function personLabel(userId: string, currentUserId: string | undefined) {
  return userId === currentUserId ? 'You' : `Member ${userId.slice(0, 4).toUpperCase()}`;
}

/** Local calendar date as yyyy-MM-dd (the server re-derives "today" in the institution's zone). */
export function localDate(d = new Date()) {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function formatDate(iso: string) {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  return new Date(y!, m! - 1, d!).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' });
}

/** Server DateTimes may lack the Z marker when read back from SQL Server — they are always UTC. */
export function parseUtc(value: string) {
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`);
}

export function downloadCsv(filename: string, header: string[], rows: (string | number | null | undefined)[][]) {
  const esc = (v: string | number | null | undefined) => {
    const s = v == null ? '' : String(v);
    return /[",\n]/.test(s) || /^[=+\-@]/.test(s) ? `"${s.replace(/"/g, '""').replace(/^([=+\-@])/, "'$1")}"` : s;
  };
  const csv = [header, ...rows].map((r) => r.map(esc).join(',')).join('\r\n');
  saveBlob(new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' }), filename);
}

export function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
