import { Lightbulb, Target, Users } from 'lucide-react';
import { Avatar, Card } from '@/components/ui';

// UI-002 — About. Static; client-supplied copy from the mockup.
const VALUES = [
  { icon: Target, title: 'Student-Centered', body: 'Every decision starts with what serves learners and the people who teach them.' },
  { icon: Users, title: 'Accessible Education', body: 'Powerful tools that work for the smallest school and the largest district alike.' },
  { icon: Lightbulb, title: 'Innovation', body: "We keep modernising so schools never have to settle for yesterday's process." },
];
const TEAM = [
  ['Rohit Jain', 'Chief Executive Officer', 'Sets the product vision and leads the company through its next phase of growth.'],
  ['Vinisha Rathod', 'Head of Product', 'Shapes the roadmap around what schools actually need every day.'],
  ['Anand Sharma', 'Head of Engineering', 'Builds the secure, reliable platform that institutions run on.'],
];

export function AboutPage() {
  return (
    <div className="mx-auto max-w-7xl px-4 py-24 sm:px-6 lg:px-8">
      <h1 className="max-w-3xl text-display text-text-primary">Empowering Education Through Innovation</h1>
      <p className="mt-5 max-w-2xl text-body-lg text-text-secondary">We build technology that gives schools back their time — so educators can spend it where it counts.</p>
      <div className="mt-14 grid gap-6 md:grid-cols-2">
        <Card className="rounded-xl p-8"><h2 className="text-h2 text-text-primary">Our Mission</h2><p className="mt-3 text-body text-text-secondary">To simplify school administration with a single, secure platform that unifies student records, staff management, attendance and communication.</p></Card>
        <Card className="rounded-xl p-8"><h2 className="text-h2 text-text-primary">Our Vision</h2><p className="mt-3 text-body text-text-secondary">A world where every institution — regardless of size — runs on modern, accessible, data-informed tools.</p></Card>
      </div>
      <h2 className="mt-20 text-center text-h1 text-text-primary">Our Core Values</h2>
      <ul className="mt-10 grid gap-6 md:grid-cols-3">
        {VALUES.map((v) => (
          <Card as="li" key={v.title} className="rounded-xl p-7 text-center">
            <span className="mx-auto grid size-12 place-items-center rounded-lg bg-primary-subtle-bg text-primary"><v.icon aria-hidden className="size-6" /></span>
            <h3 className="mt-4 font-bold text-text-primary">{v.title}</h3>
            <p className="mt-2 text-body-sm text-text-secondary">{v.body}</p>
          </Card>
        ))}
      </ul>
      <h2 className="mt-20 text-center text-h1 text-text-primary">Meet Our Leadership Team</h2>
      <ul className="mt-10 grid gap-6 sm:grid-cols-3">
        {TEAM.map(([name, role, bio]) => (
          <Card as="li" key={name} className="rounded-xl p-7 text-center">
            <Avatar name={name} className="mx-auto size-24 rounded-full text-h2" />
            <h3 className="mt-4 font-bold text-text-primary">{name}</h3>
            <p className="text-body-sm font-medium text-primary">{role}</p>
            <p className="mt-3 text-body-sm text-text-secondary">{bio}</p>
          </Card>
        ))}
      </ul>
    </div>
  );
}
