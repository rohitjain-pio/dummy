import { BarChart3, ClipboardCheck, GraduationCap, Megaphone } from 'lucide-react';
import { Link } from 'react-router';
import { Card } from '@/components/ui';

// UI-001 — Home. Static marketing content; success state only. The "Live" badge is decorative
// (06-event-contracts.md §3) — no socket. Copy from the client mockup; the 4 feature cards are the
// in-scope ones (the mockup's "Accounts Management" card is out of scope — FR-081/082 excluded).
// Stat and testimonial copy is client-supplied marketing text, flagged for validation before launch.
const STATS = [['15,000+', 'Students Managed'], ['10+', 'Schools Trust Us'], ['99.9%', 'Uptime Guarantee'], ['24/7', 'Support Available']];
const FEATURES = [
  { icon: GraduationCap, title: 'Student Management', body: 'Complete student profiles, enrolment records, class placement and guardian details in one place.' },
  { icon: ClipboardCheck, title: 'Attendance Tracking', body: 'Mark a full class register in seconds and track presence day by day.' },
  { icon: Megaphone, title: 'Announcements & Events', body: 'Publish notices to staff, students and families with a full institutional record.' },
  { icon: BarChart3, title: 'Analytics & Reports', body: 'Dashboards and exportable reports on attendance across your classes.' },
];
const TESTIMONIALS = [
  ['“Attendance that used to take our staff an hour now takes minutes.”', 'Anita Verma', 'Principal, Greenwood International'],
  ['“Every record finally lives in one place — our admin team got their week back.”', 'Sameer Khan', 'School In-charge, St. Xavier High'],
  ['“Announcements reach every parent instantly and stay on record.”', 'Leela Menon', 'Administrator, Sunrise Academy'],
];
const GLANCE = [['Total Schools', '24'], ['Total Students', '12,394'], ['Total Teachers', '1,823']];

export function HomePage() {
  return (
    <>
      <section className="border-b border-border bg-primary-subtle-bg">
        <div className="mx-auto grid max-w-7xl items-start gap-12 px-4 pb-20 pt-14 sm:px-6 lg:grid-cols-[1.1fr_1fr] lg:px-8">
          <div className="lg:pt-6">
            <span className="inline-flex items-center gap-2 rounded-full bg-primary-subtle-bg-strong px-3 py-1.5 text-caption font-semibold text-primary-subtle-text">New Features Available</span>
            <h1 className="mt-5 text-display text-text-primary">Streamline Your <span className="text-primary">School Management</span></h1>
            <p className="mt-5 text-body-lg text-text-secondary">Comprehensive school management system that simplifies administration, enhances communication, and improves student outcomes with modern technology.</p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Link to="/pricing" className="inline-flex h-12 items-center justify-center rounded-md bg-primary px-6 font-semibold text-white shadow-glow-primary hover:bg-primary-hover">Start Free Trial</Link>
              <Link to="/contact" className="inline-flex h-12 items-center justify-center rounded-md border border-border bg-surface px-6 font-semibold text-text-primary hover:border-primary">Watch Demo</Link>
            </div>
            <p className="mt-4 text-caption text-text-secondary">Free 30-day trial · No credit card required</p>
            <dl className="mt-10 grid max-w-xl grid-cols-2 gap-4 sm:grid-cols-4">
              {STATS.map(([v, l]) => (
                <Card key={l} className="rounded-lg px-3 py-3">
                  <dt className="text-caption text-text-secondary">{l}</dt>
                  <dd className="order-first text-lg font-extrabold text-text-primary">{v}</dd>
                </Card>
              ))}
            </dl>
          </div>
          <Card className="rounded-xl p-6">
            <div className="flex items-center justify-between">
              <p className="text-micro-label uppercase text-text-secondary">Platform at a glance</p>
              <span className="inline-flex items-center gap-1.5 text-caption font-semibold text-success-subtle-text"><span aria-hidden className="size-1.5 rounded-full bg-success" />Live</span>
            </div>
            <dl className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
              {GLANCE.map(([l, v]) => (
                <div key={l} className="rounded-lg border border-border px-3.5 py-4">
                  <dt className="text-micro-label uppercase text-text-secondary">{l}</dt>
                  <dd className="mt-2 text-h2 text-text-primary">{v}</dd>
                </div>
              ))}
            </dl>
            <div className="mt-4 rounded-lg border border-border p-4">
              <div className="flex items-center justify-between"><p className="text-micro-label uppercase text-text-secondary">Enrolment growth</p><span className="text-caption font-semibold text-success-subtle-text">+18% YoY</span></div>
              <svg viewBox="0 0 320 90" className="mt-3 h-24 w-full" role="img" aria-label="Enrolment growth trend rising across eight quarters">
                <polyline fill="none" stroke="var(--color-primary)" strokeWidth="3" strokeLinejoin="round" points="0,80 45,72 90,62 135,54 180,40 225,30 270,18 320,8" />
              </svg>
            </div>
          </Card>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-4 py-24 sm:px-6 lg:px-8" aria-labelledby="features-title">
        <div className="mx-auto max-w-2xl text-center">
          <h2 id="features-title" className="text-h1 text-text-primary">Everything You Need to Manage Your School</h2>
          <p className="mt-4 text-body-lg text-text-secondary">Our comprehensive platform provides all the tools you need to streamline operations and focus on what matters most — education.</p>
        </div>
        {/* UI-001 responsive: 1-col < bp-md, 2-col at bp-md, 4-col at bp-lg; radius-xl, space-6 gutter */}
        <ul className="mt-14 grid gap-6 md:grid-cols-2 lg:grid-cols-4">
          {FEATURES.map((f) => (
            <Card as="li" key={f.title} className="rounded-xl p-6">
              <span className="grid size-11 place-items-center rounded-lg bg-primary-subtle-bg text-primary"><f.icon aria-hidden className="size-6" /></span>
              <h3 className="mt-4 font-bold text-text-primary">{f.title}</h3>
              <p className="mt-2 text-body-sm text-text-secondary">{f.body}</p>
            </Card>
          ))}
        </ul>
      </section>

      <section className="border-y border-border bg-surface" aria-labelledby="testimonials-title">
        <div className="mx-auto max-w-7xl px-4 py-24 sm:px-6 lg:px-8">
          <h2 id="testimonials-title" className="text-center text-h1 text-text-primary">Trusted by Educators Worldwide</h2>
          <ul className="mt-12 grid gap-6 md:grid-cols-3">
            {TESTIMONIALS.map(([q, n, r]) => (
              <li key={n} className="rounded-xl border border-border bg-background p-7">
                <blockquote className="italic text-text-primary">{q}</blockquote>
                <p className="mt-5 text-body-sm font-semibold text-text-primary">{n}</p>
                <p className="text-caption text-text-secondary">{r}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>

      {/* Closing CTA band — dark surface (ui-design-final.md: color-text-primary-toned) */}
      <section className="bg-text-primary">
        <div className="mx-auto max-w-2xl px-6 py-20 text-center">
          <h2 className="text-h1 text-white">Ready to Transform Your School Management?</h2>
          <p className="mt-4 text-body-lg text-text-quaternary">Join schools already modernising their operations with SchoolSync.</p>
          <Link to="/pricing" className="mt-8 inline-flex h-12 items-center rounded-md bg-surface px-7 font-bold text-primary shadow-glow-dark">Start Your Free Trial</Link>
        </div>
      </section>
    </>
  );
}
