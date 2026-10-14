import {
  BarChart3, CalendarDays, ClipboardCheck, GraduationCap, Layers, LayoutDashboard, Megaphone, Settings, UserRound, Video,
  type LucideIcon,
} from 'lucide-react';
import { LEADERSHIP, LEADERSHIP_AND_TEACHER, ROLES, INSTITUTION_ROLES, type Role } from '@/lib/roles';

// UI-006 nav model — CT-601 DoD: "Nav model matches 09-security-spec.md §2 exactly".
// The nav is a UX mirror only; the server is the real boundary (FR-009). Where the UI-006 table and
// §2 disagree, §2 wins (the DoD names it) and the difference is filed as upstream feedback:
//  - Reports: §2 "Generate reports — Teacher ✅ (own classes)" and UI-017 → leadership + Teacher.
//  - Schedule: §2 "Maintain schedule — Teacher/Student/Parent read only" and UI-016 → every
//    institution role (view); PlatformOperator holds no institution-internal capability (§2 footnote).
//  - Attendance / Classes / Students: §2 grants no Student/Parent access to master records or
//    rosters; UI-010 routes their own/linked attendance to the Dashboard variant.
//  - PlatformOperator: §2 grants rooms (Meetings) only among institution features; Dashboard shows
//    the platform summary (the institution dashboard is ❌ for PO); Settings is own-account.
export interface NavItem {
  label: string;
  path: string;
  icon: LucideIcon;
  roles: readonly Role[];
}

export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: LayoutDashboard, roles: ROLES },
  { label: 'Classes', path: '/dashboard/classes', icon: Layers, roles: LEADERSHIP_AND_TEACHER },
  { label: 'Teachers', path: '/dashboard/teachers', icon: UserRound, roles: LEADERSHIP },
  { label: 'Students', path: '/dashboard/students', icon: GraduationCap, roles: LEADERSHIP_AND_TEACHER },
  { label: 'Attendance', path: '/dashboard/attendance', icon: ClipboardCheck, roles: LEADERSHIP_AND_TEACHER },
  { label: 'Announcements', path: '/dashboard/announcements', icon: Megaphone, roles: INSTITUTION_ROLES },
  { label: 'Meetings', path: '/dashboard/meeting', icon: Video, roles: ROLES },
  { label: 'Schedule', path: '/dashboard/schedule', icon: CalendarDays, roles: INSTITUTION_ROLES },
  { label: 'Reports', path: '/dashboard/reports', icon: BarChart3, roles: LEADERSHIP_AND_TEACHER },
  { label: 'Settings', path: '/dashboard/settings', icon: Settings, roles: ROLES },
];

export const navFor = (role: Role) => NAV_ITEMS.filter((i) => i.roles.includes(role));

/** Roles allowed on an app route — chat/video inherit Meetings. Unknown app paths → no roles. */
export function rolesForPath(pathname: string): readonly Role[] | undefined {
  if (pathname.startsWith('/chat/') || pathname.startsWith('/video-call/')) return NAV_ITEMS.find((i) => i.label === 'Meetings')!.roles;
  const exact = NAV_ITEMS.find((i) => i.path === pathname);
  return exact?.roles;
}

export const canAccess = (role: Role, pathname: string) => rolesForPath(pathname)?.includes(role) ?? false;
