// BR-007 — seven closed roles. Values match the API's role strings exactly.
export const ROLES = ['PlatformOperator', 'Administrator', 'Principal', 'SchoolInCharge', 'Teacher', 'Student', 'Parent'] as const;
export type Role = (typeof ROLES)[number];

export const LEADERSHIP: readonly Role[] = ['Administrator', 'Principal', 'SchoolInCharge'];
export const LEADERSHIP_AND_TEACHER: readonly Role[] = [...LEADERSHIP, 'Teacher'];
export const INSTITUTION_ROLES: readonly Role[] = [...LEADERSHIP, 'Teacher', 'Student', 'Parent'];

export const isLeadership = (role: Role | undefined) => !!role && LEADERSHIP.includes(role);

export const ROLE_LABEL: Record<Role, string> = {
  PlatformOperator: 'Platform Operator',
  Administrator: 'Administrator',
  Principal: 'Principal',
  SchoolInCharge: 'School In-charge',
  Teacher: 'Teacher',
  Student: 'Student',
  Parent: 'Parent',
};

// UI-005 / Q-202: PlatformOperator is never self-registrable, so it is not even offered.
export const REGISTRABLE_ROLES: readonly Role[] = INSTITUTION_ROLES;
