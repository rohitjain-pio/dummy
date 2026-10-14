import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect } from 'react';
import { createBrowserRouter, Outlet, RouterProvider } from 'react-router';
import { RequireAuth, RequireRole } from '@/auth/guards';
import { useSession } from '@/auth/session';
import { ToastHost } from '@/components/ui/toast';
import { AppShell } from '@/layouts/AppShell';
import { PublicLayout } from '@/layouts/PublicLayout';
import { ApiError, refreshSession } from '@/lib/api';
import { AnnouncementsPage } from '@/pages/app/Announcements';
import { AttendancePage } from '@/pages/app/Attendance';
import { ChatPage } from '@/pages/app/Chat';
import { ClassesPage } from '@/pages/app/Classes';
import { DashboardPage } from '@/pages/app/Dashboard';
import { MeetingsPage } from '@/pages/app/Meetings';
import { ReportsPage } from '@/pages/app/Reports';
import { SchedulePage } from '@/pages/app/Schedule';
import { SettingsPage } from '@/pages/app/Settings';
import { StudentsPage } from '@/pages/app/Students';
import { TeachersPage } from '@/pages/app/Teachers';
import { VideoCallPage } from '@/pages/app/VideoCall';
import { AboutPage } from '@/pages/public/About';
import { ContactPage } from '@/pages/public/Contact';
import { HomePage } from '@/pages/public/Home';
import { PricingPage } from '@/pages/public/Pricing';
import { NotFoundPage, UnauthorizedPage } from '@/pages/Utility';

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // never retry a definitive answer (403/404/402…), only transient failures
        retry: (count, error) => count < 2 && !(error instanceof ApiError && error.status < 500),
      },
    },
  });
}

const guarded = (element: React.ReactNode) => <RequireRole fallback={<UnauthorizedPage />}>{element}</RequireRole>;

// Q-231: path routes, served by the API's SPA fallback. UI spec route names kept (/dashboard/meeting, /chat/:roomId, /video-call/:roomId).
export const routes = [
  {
    element: <Root />,
    children: [
      {
        element: <PublicLayout />,
        children: [
          { path: '/', element: <HomePage /> },
          { path: '/about', element: <AboutPage /> },
          { path: '/pricing', element: <PricingPage /> },
          { path: '/contact', element: <ContactPage /> },
        ],
      },
      {
        element: <RequireAuth><AppShell /></RequireAuth>,
        children: [
          { path: '/dashboard', element: guarded(<DashboardPage />) },
          { path: '/dashboard/classes', element: guarded(<ClassesPage />) },
          { path: '/dashboard/teachers', element: guarded(<TeachersPage />) },
          { path: '/dashboard/students', element: guarded(<StudentsPage />) },
          { path: '/dashboard/attendance', element: guarded(<AttendancePage />) },
          { path: '/dashboard/announcements', element: guarded(<AnnouncementsPage />) },
          { path: '/dashboard/meeting', element: guarded(<MeetingsPage />) },
          { path: '/dashboard/schedule', element: guarded(<SchedulePage />) },
          { path: '/dashboard/reports', element: guarded(<ReportsPage />) },
          { path: '/dashboard/settings', element: guarded(<SettingsPage />) },
          { path: '/chat/:roomId', element: guarded(<ChatPage />) },
          { path: '/dashboard/*', element: <UnauthorizedOrMissing /> },
        ],
      },
      // UI-014: dark full-screen, outside the shell
      { path: '/video-call/:roomId', element: <RequireAuth>{guarded(<VideoCallPage />)}</RequireAuth> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];

function UnauthorizedOrMissing() {
  return <NotFoundInShell />;
}
function NotFoundInShell() {
  return (
    <div className="mx-auto max-w-lg py-12 text-center">
      <h2 className="text-h2 text-text-primary">Page not found</h2>
      <p className="mt-3 text-body text-text-secondary">The page you’re looking for doesn’t exist or has been moved.</p>
    </div>
  );
}

/** Restores the session once per page load from the httpOnly refresh cookie (Q-232). */
function Root() {
  const status = useSession((s) => s.status);
  useEffect(() => {
    if (status === 'unknown') void refreshSession();
  }, [status]);
  return (
    <>
      <Outlet />
      <ToastHost />
    </>
  );
}

const router = createBrowserRouter(routes);
const queryClient = createQueryClient();

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}
