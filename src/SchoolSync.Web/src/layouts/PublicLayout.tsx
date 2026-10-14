import { Menu, X } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router';
import { useAuthModal } from '@/auth/authActions';
import { useSession } from '@/auth/session';
import { Button } from '@/components/ui';
import { AuthModal } from '@/features/auth/AuthModal';
import { cn } from '@/lib/cn';

const PUBLIC_NAV = [
  { label: 'Home', path: '/' },
  { label: 'About', path: '/about' },
  { label: 'Pricing', path: '/pricing' },
  { label: 'Contact', path: '/contact' },
];

export function Logo({ inverse = false }: { inverse?: boolean }) {
  return (
    <span className="flex items-center gap-2.5">
      <span aria-hidden className="grid size-9 place-items-center rounded-lg bg-primary font-extrabold text-white">S</span>
      <span className={cn('text-lg font-extrabold tracking-tight', inverse ? 'text-white' : 'text-text-primary')}>SchoolSync</span>
    </span>
  );
}

export function PublicLayout() {
  const openWith = useAuthModal((s) => s.openWith);
  const status = useSession((s) => s.status);
  const [scrolled, setScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 4);
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);
  useEffect(() => { window.scrollTo(0, 0); }, [location.pathname]);

  const linkCls = ({ isActive }: { isActive: boolean }) =>
    cn('rounded-md px-3.5 py-2 text-body-sm font-medium', isActive ? 'bg-primary-subtle-bg text-primary-subtle-text' : 'text-text-secondary hover:bg-background hover:text-text-primary');

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[70] focus:rounded-md focus:bg-surface focus:px-4 focus:py-2">Skip to content</a>
      {/* UI-001 sticky header: color-surface, elevation-card on scroll */}
      <header className={cn('sticky top-0 z-30 border-b border-border bg-surface', scrolled && 'shadow-card')}>
        <div className="mx-auto flex h-18 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6 lg:px-8">
          <Link to="/" aria-label="SchoolSync home"><Logo /></Link>
          <nav aria-label="Main" className="hidden items-center gap-1 lg:flex">
            {PUBLIC_NAV.map((i) => <NavLink key={i.path} to={i.path} end className={linkCls}>{i.label}</NavLink>)}
          </nav>
          <div className="flex items-center gap-2">
            {status === 'authenticated'
              ? <Link to="/dashboard" className="inline-flex h-10 items-center rounded-md bg-primary px-4 text-body-sm font-semibold text-white hover:bg-primary-hover">Dashboard</Link>
              : <Button onClick={() => openWith('login')}>Login / Register</Button>}
            <Button variant="outline" size="icon" className="lg:hidden" aria-label={mobileOpen ? 'Close menu' : 'Open menu'} aria-expanded={mobileOpen} aria-controls="public-mobile-nav" onClick={() => setMobileOpen((o) => !o)}>
              {mobileOpen ? <X aria-hidden className="size-5" /> : <Menu aria-hidden className="size-5" />}
            </Button>
          </div>
        </div>
        {mobileOpen && (
          <nav id="public-mobile-nav" aria-label="Main mobile" className="border-t border-border bg-surface px-4 py-3 lg:hidden">
            {PUBLIC_NAV.map((i) => <NavLink key={i.path} to={i.path} end onClick={() => setMobileOpen(false)} className={({ isActive }) => cn(linkCls({ isActive }), 'flex h-11 items-center')}>{i.label}</NavLink>)}
          </nav>
        )}
      </header>

      <main id="main" className="flex-1"><Outlet /></main>

      <footer className="bg-text-primary text-text-quaternary">
        <div className="mx-auto grid max-w-7xl gap-10 px-4 py-14 sm:px-6 md:grid-cols-4 lg:px-8">
          <div>
            <Logo inverse />
            <p className="mt-4 text-body-sm">Transforming education through innovative school management solutions. Empowering schools to focus on what matters most — student success.</p>
            <address className="mt-4 text-body-sm not-italic">avrtech25@outlook.com<br />+91 70147 91203<br />AVR Tech, Business Corner, VT Road,<br />Mansarover, Jaipur, Rajasthan.</address>
          </div>
          {/* Footer links without a destination in scope (Careers, Press, API docs, legal pages) are not rendered as dead links. */}
          <FooterCol title="Product" links={[['Features', '/'], ['Pricing', '/pricing'], ['Demo', '/contact']]} />
          <FooterCol title="Company" links={[['About Us', '/about']]} />
          <FooterCol title="Support" links={[['Contact', '/contact']]} />
        </div>
        <div className="border-t border-white/10">
          <p className="mx-auto max-w-7xl px-4 py-6 text-body-sm sm:px-6 lg:px-8">© 2026 SchoolSync. All rights reserved.</p>
        </div>
      </footer>
      <AuthModal />
    </div>
  );
}

function FooterCol({ title, links }: { title: string; links: [string, string][] }) {
  return (
    <div>
      <h2 className="font-semibold text-white">{title}</h2>
      <ul className="mt-4 space-y-2.5 text-body-sm">
        {links.map(([l, p]) => <li key={l}><Link to={p} className="hover:text-white">{l}</Link></li>)}
      </ul>
    </div>
  );
}
