import * as Accordion from '@radix-ui/react-accordion';
import { useQuery } from '@tanstack/react-query';
import { Check, ChevronDown, Minus } from 'lucide-react';
import { Link } from 'react-router';
import { Badge, Card, ErrorBanner, LoadingState } from '@/components/ui';
import { api, errorMessage } from '@/lib/api';
import type { Plan, PlanCapability } from '@/lib/types';

// UI-004 — Pricing. GET /public/plans (API-061). BR-061/062/063.
// - Every structural value and every comparison cell is rendered from SubscriptionPlan at render
//   time — never hardcoded feature copy (the operator's binding instruction in UI-004).
// - Q-233 (Rashmi Mehta): no price is shown; every plan's CTA is "Contact sales".
// - Q-237: with no prices, the monthly/yearly toggle would re-render nothing, so it is omitted.
// - "Most Popular" sits on the mid tier (plans are ordered by capacity; unlimited last).
const CAPABILITY_LABEL: Record<PlanCapability, string> = {
  AdvancedAnalytics: 'Advanced analytics',
  ParentAccess: 'Parent access',
  CustomReporting: 'Custom reporting',
};
const CAPABILITIES = Object.keys(CAPABILITY_LABEL) as PlanCapability[];

const capacity = (p: Plan) => (p.learnerCapacity === null ? 'Unlimited learners' : `Up to ${p.learnerCapacity.toLocaleString('en-IN')} learners`);
const order = (p: Plan) => p.learnerCapacity ?? Number.MAX_SAFE_INTEGER;

// FAQ copy: the mockup's proration clause is omitted (no billing in scope — BRD §3.2).
const FAQ = [
  ['Can I change plans anytime?', 'Yes. Contact us and our team will move your institution to a different plan; the new limits take effect as soon as the change is made.'],
  ['Is there a free trial?', 'Yes. Eligible schools can start a 30-day trial of the mid-tier plan. No credit card required.'],
  ['What kind of support do you provide?', 'Each plan includes the support level shown on its card above.'],
];

export function PricingPage() {
  const plans = useQuery({ queryKey: ['public-plans'], queryFn: () => api.get<Plan[]>('/public/plans', { anonymous: true }) });
  const sorted = [...(plans.data ?? [])].sort((a, b) => order(a) - order(b));
  const popularId = sorted.length === 3 ? sorted[1]!.id : undefined;

  return (
    <div className="mx-auto max-w-7xl px-4 py-24 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-3xl text-center">
        <h1 className="text-display text-text-primary">Simple, Transparent Plans</h1>
        <p className="mt-4 text-body-lg text-text-secondary">Choose the plan that fits your school. Talk to our team for pricing tailored to your institution.</p>
      </div>

      {plans.isPending && <div className="mt-14"><LoadingState label="Loading plans" variant="cards" rows={3} /></div>}
      {plans.isError && <div className="mt-14"><ErrorBanner>{errorMessage(plans.error)}</ErrorBanner></div>}

      {plans.isSuccess && (
        <>
          <ul className="mt-14 grid items-start gap-6 lg:grid-cols-3">
            {sorted.map((p) => (
              <Card as="li" key={p.id} className={p.id === popularId ? 'relative rounded-xl border-primary p-8 ring-2 ring-primary-subtle-bg-strong' : 'relative rounded-xl p-8'}>
                {p.id === popularId && <span className="absolute -top-3 left-1/2 -translate-x-1/2"><Badge>Most Popular</Badge></span>}
                <h2 className="text-h2 text-text-primary">{p.name}</h2>
                <p className="mt-3 text-h1 text-text-primary">Contact sales</p>
                <ul className="mt-6 space-y-3 text-body-sm">
                  {[capacity(p), `${p.storageAllowanceGb.toLocaleString('en-IN')} GB storage`, `${p.supportLevel} support`, ...p.capabilities.map((c) => CAPABILITY_LABEL[c])].map((f) => (
                    <li key={f} className="flex gap-2.5"><Check aria-hidden className="size-4 shrink-0 text-success" /><span className="text-text-secondary">{f}</span></li>
                  ))}
                </ul>
                <Link to="/contact" className={p.id === popularId
                  ? 'mt-7 flex h-12 items-center justify-center rounded-md bg-primary font-semibold text-white hover:bg-primary-hover'
                  : 'mt-7 flex h-12 items-center justify-center rounded-md border border-border font-semibold text-text-primary hover:bg-background'}>
                  Contact sales<span className="sr-only"> about the {p.name} plan</span>
                </Link>
              </Card>
            ))}
          </ul>

          <h2 className="mt-24 text-center text-h1 text-text-primary">Compare Features</h2>
          <p className="mt-3 text-center text-text-secondary">See exactly what&apos;s included in each plan.</p>
          <div tabIndex={0} role="region" aria-label="Plan feature comparison" className="mt-8 overflow-x-auto rounded-xl border border-border bg-surface shadow-card focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary">
            <table className="w-full min-w-[640px] text-body-sm">
              <caption className="sr-only">Plan feature comparison</caption>
              <thead className="border-b border-border bg-background text-text-secondary">
                <tr><th scope="col" className="px-6 py-4 text-left font-semibold">Features</th>{sorted.map((p) => <th scope="col" key={p.id} className="px-6 py-4 font-semibold">{p.name}</th>)}</tr>
              </thead>
              <tbody className="divide-y divide-border-subtle">
                <Row label="Learner capacity" cells={sorted.map((p) => capacity(p).replace(' learners', ''))} />
                <Row label="Storage" cells={sorted.map((p) => `${p.storageAllowanceGb} GB`)} />
                <Row label="Support" cells={sorted.map((p) => p.supportLevel)} />
                {CAPABILITIES.map((c) => (
                  <tr key={c}>
                    <th scope="row" className="px-6 py-3.5 text-left font-medium text-text-primary">{CAPABILITY_LABEL[c]}</th>
                    {sorted.map((p) => (
                      <td key={p.id} className="px-6 py-3.5 text-center">
                        {p.capabilities.includes(c)
                          ? <><Check aria-hidden className="mx-auto size-4 text-success" /><span className="sr-only">Included</span></>
                          : <><Minus aria-hidden className="mx-auto size-4 text-text-secondary" /><span className="sr-only">Not included</span></>}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      <h2 className="mt-24 text-center text-h1 text-text-primary">Frequently Asked Questions</h2>
      <Accordion.Root type="single" collapsible defaultValue="0" className="mx-auto mt-8 max-w-3xl space-y-3">
        {FAQ.map(([q, a], i) => (
          <Accordion.Item key={q} value={String(i)} className="overflow-hidden rounded-lg border border-border bg-surface">
            <Accordion.Header>
              <Accordion.Trigger className="group flex w-full justify-between gap-4 px-6 py-4 text-left font-semibold text-text-primary">
                {q}<ChevronDown aria-hidden className="size-5 text-text-secondary transition-transform group-data-[state=open]:rotate-180" />
              </Accordion.Trigger>
            </Accordion.Header>
            <Accordion.Content className="px-6 pb-5 text-body-sm text-text-secondary">{a}</Accordion.Content>
          </Accordion.Item>
        ))}
      </Accordion.Root>
    </div>
  );
}

function Row({ label, cells }: { label: string; cells: string[] }) {
  return (
    <tr>
      <th scope="row" className="px-6 py-3.5 text-left font-medium text-text-primary">{label}</th>
      {cells.map((c, i) => <td key={i} className="px-6 py-3.5 text-center text-text-secondary">{c}</td>)}
    </tr>
  );
}
