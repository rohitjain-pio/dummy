import { useEffect, type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { LoadingState } from '@/components/ui';
import { canAccess } from '@/nav/navConfig';
import { useAuthModal } from './authActions';
import { useSession } from './session';

/** Signed-out visitors to an app route are sent home with the login modal open (no /login page — UI-005). */
export function RequireAuth({ children }: { children: ReactNode }) {
  const status = useSession((s) => s.status);
  const openWith = useAuthModal((s) => s.openWith);
  useEffect(() => { if (status === 'anonymous') openWith('login'); }, [status, openWith]);
  if (status === 'unknown') return <div className="p-8"><LoadingState label="Restoring your session" /></div>;
  if (status === 'anonymous') return <Navigate to="/" replace />;
  return <>{children}</>;
}

/** UI-006/UI-019: a deep link outside the role's set renders Unauthorized, not merely a hidden nav entry. */
export function RequireRole({ children, fallback }: { children: ReactNode; fallback: ReactNode }) {
  const role = useSession((s) => s.user?.role);
  const { pathname } = useLocation();
  if (!role || !canAccess(role, pathname)) return <>{fallback}</>;
  return <>{children}</>;
}
