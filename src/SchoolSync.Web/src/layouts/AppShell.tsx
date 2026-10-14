import * as DropdownMenu from '@radix-ui/react-dropdown-menu';
import { Bell, ChevronDown, LogOut, Menu, Search, X } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router';
import { logout } from '@/auth/authActions';
import { useSession } from '@/auth/session';
import { Avatar, Button } from '@/components/ui';
import { cn } from '@/lib/cn';
import { ROLE_LABEL } from '@/lib/roles';
import { navFor, NAV_ITEMS } from '@/nav/navConfig';
import { Logo } from './PublicLayout';

// UI-006 — App shell: dark side nav (layout-sidenav-width, Q-213) + header + profile menu.
// No role switcher (BR-008; ui-design-final.md §0) — never built.

/** Real breadcrumb from actual route nesting (improved-beyond-mockup: no fixed "Home"). */
export function useCrumbs(pathname: string): { title: string; trail: { label: string; to?: string }[] } {
  const dash = { label: 'Dashboard', to: '/dashboard' };
  if (pathname === '/dashboard') return { title: 'Dashboard', trail: [{ label: 'Dashboard' }] };
  if (pathname.startsWith('/chat/')) return { title: 'Chat', trail: [dash, { label: 'Meetings', to: '/dashboard/meeting' }, { label: 'Chat' }] };
  const item = NAV_ITEMS.find((i) => i.path === pathname);
  const title = item?.label ?? 'Not found';
  return { title, trail: [dash, { label: title }] };
}

export function AppShell() {
  const user = useSession((s) => s.user)!;
  const location = useLocation();
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const { title, trail } = useCrumbs(location.pathname);

  useEffect(() => {
    if (!sidebarOpen) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setSidebarOpen(false);
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [sidebarOpen]);

  async function onLogout() {
    await logout();
    navigate('/');
  }

  const items = navFor(user.role);

  return (
    <div className="flex min-h-screen bg-background">
      <a href="#app-main" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[70] focus:rounded-md focus:bg-surface focus:px-4 focus:py-2">Skip to content</a>
      <aside
        id="app-sidebar"
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex w-sidenav flex-col bg-text-primary text-text-quaternary transition-transform lg:static lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full',
        )}
        aria-label="Sidebar"
      >
        <div className="flex h-18 shrink-0 items-center justify-between border-b border-white/10 px-5">
          <Link to="/" aria-label="SchoolSync home"><Logo inverse /></Link>
          <button type="button" className="text-text-quaternary hover:text-white lg:hidden" aria-label="Close navigation" onClick={() => setSidebarOpen(false)}><X aria-hidden className="size-5" /></button>
        </div>
        <nav aria-label="Primary" className="flex-1 space-y-1 overflow-y-auto px-4 py-5">
          {items.map((i) => (
            <NavLink
              key={i.path}
              to={i.path}
              end
              onClick={() => setSidebarOpen(false)}
              className={({ isActive }) => cn('flex h-10 items-center gap-3 rounded-md px-3 text-body-sm font-medium',
                isActive ? 'bg-primary text-white shadow-glow-nav' : 'hover:bg-white/10 hover:text-white')}
            >
              <i.icon aria-hidden className="size-5" />{i.label}
            </NavLink>
          ))}
        </nav>
        <div className="border-t border-white/10 px-4 py-4">
          <button type="button" onClick={onLogout} className="flex h-10 w-full items-center gap-3 rounded-md px-3 text-body-sm font-medium hover:bg-white/10 hover:text-white">
            <LogOut aria-hidden className="size-5" /> Log out
          </button>
        </div>
      </aside>
      {sidebarOpen && <div aria-hidden className="fixed inset-0 z-30 bg-text-primary/50 lg:hidden" onClick={() => setSidebarOpen(false)} />}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 border-b border-border bg-surface">
          <div className="flex h-18 items-center gap-4 px-4 sm:px-6 lg:px-8">
            <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Open navigation" aria-expanded={sidebarOpen} aria-controls="app-sidebar" onClick={() => setSidebarOpen(true)}>
              <Menu aria-hidden className="size-5" />
            </Button>
            <div className="min-w-0">
              <h1 className="truncate text-body font-bold text-text-primary">{title}</h1>
              <nav aria-label="Breadcrumb">
                <ol className="flex items-center gap-1 truncate text-micro-label uppercase text-text-secondary">
                  {trail.map((c, idx) => (
                    <li key={c.label} className="flex items-center gap-1">
                      {idx > 0 && <span aria-hidden>/</span>}
                      {c.to ? <Link to={c.to} className="hover:text-primary">{c.label}</Link> : <span aria-current="page">{c.label}</span>}
                    </li>
                  ))}
                </ol>
              </nav>
            </div>
            <div className="ml-auto flex items-center gap-2">
              {/* Decorative search and bell — no FR backs either (UI-006); not wired to any endpoint. */}
              <div className="hidden items-center gap-2 rounded-md border border-border bg-background px-3 py-2 md:flex" aria-hidden>
                <Search className="size-4 text-text-secondary" />
                <span className="w-40 text-body-sm text-text-secondary">Search anything…</span>
              </div>
              <span aria-hidden className="hidden size-10 place-items-center rounded-md text-text-secondary sm:grid"><Bell className="size-5" /></span>

              {/* modal={false}: a modal Radix menu aria-hidden-s the rest of the page including this trigger,
                  which removes the disclosure from the accessibility tree while it is open (UI-006). */}
              <DropdownMenu.Root modal={false}>
                <DropdownMenu.Trigger asChild>
                  <button type="button" className="flex items-center gap-2.5 rounded-md py-1.5 pl-1 pr-2.5 hover:bg-background" aria-label={`Profile menu for ${user.username}`}>
                    <Avatar name={user.username} />
                    <span className="hidden text-left leading-tight sm:block">
                      <span className="block text-body-sm font-semibold text-text-primary">{user.username}</span>
                      <span className="block text-caption text-text-secondary">{ROLE_LABEL[user.role]}</span>
                    </span>
                    <ChevronDown aria-hidden className="size-4 text-text-secondary" />
                  </button>
                </DropdownMenu.Trigger>
                <DropdownMenu.Portal>
                  <DropdownMenu.Content align="end" sideOffset={8} className="z-50 w-64 rounded-lg border border-border bg-surface p-2 shadow-card">
                    <div className="border-b border-border-subtle px-3 py-3">
                      <p className="text-body-sm font-bold text-text-primary">{user.username}</p>
                      <span className="mt-2 inline-block rounded-sm bg-primary-subtle-bg px-2 py-0.5 text-caption font-semibold text-primary-subtle-text">{ROLE_LABEL[user.role]}</span>
                    </div>
                    <DropdownMenu.Item asChild>
                      <Link to="/dashboard/settings" className="mt-1 block rounded-md px-3 py-2 text-body-sm text-text-primary outline-none data-[highlighted]:bg-background">Account settings</Link>
                    </DropdownMenu.Item>
                    <DropdownMenu.Item onSelect={onLogout} className="cursor-pointer rounded-md px-3 py-2 text-body-sm text-error outline-none data-[highlighted]:bg-error-subtle-bg">Log out</DropdownMenu.Item>
                  </DropdownMenu.Content>
                </DropdownMenu.Portal>
              </DropdownMenu.Root>
            </div>
          </div>
        </header>
        <main id="app-main" className="w-full flex-1 px-4 py-8 sm:px-6 lg:px-8 lg:py-10"><Outlet /></main>
      </div>
    </div>
  );
}
