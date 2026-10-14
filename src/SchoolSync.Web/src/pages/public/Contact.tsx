import { Clock, Mail, MapPin, Phone } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Button, Card, ErrorBanner, SelectField, TextareaField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { emailError } from '@/features/auth/validation';
import { api, errorMessage, idempotencyKey } from '@/lib/api';

// UI-003 — Contact. POST /public/enquiries (API-062). FR-069.
const CARDS = [
  { icon: Mail, title: 'Email Us', lines: ['avrtech25@outlook.com', 'work.rohitjain@outlook.com'] },
  { icon: Phone, title: 'Call Us', lines: ['+91 70147 91203', '+91 91712 12231'] },
  { icon: MapPin, title: 'Visit Us', lines: ['AVR Tech, Business Corner', 'VT Road, Mansarover, Jaipur'] },
  { icon: Clock, title: 'Business Hours', lines: ['Everyday: 10AM – 6PM', 'Support: 24/7'] },
];
const RANGES = ['1-50 students', '51-200 students', '201-500 students', '501-1000 students', '1000+ students'];
const EMPTY = { fullName: '', email: '', institution: '', studentCountRange: '', message: '' };

export function ContactPage() {
  const [form, setForm] = useState(EMPTY);
  const [emailTouched, setEmailTouched] = useState(false);
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setEmailTouched(true);
    if (emailError(form.email)) return;
    setError(undefined);
    setLoading(true);
    try {
      await api.post('/public/enquiries', { ...form, studentCountRange: form.studentCountRange || null, institution: form.institution || null }, { anonymous: true, headers: idempotencyKey() });
      toast.success('Message sent — our team will respond within 24 hours.');
      setForm(EMPTY);
      setEmailTouched(false);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="mx-auto max-w-7xl px-4 py-24 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-3xl text-center">
        <h1 className="text-display text-text-primary">Get in Touch</h1>
        <p className="mt-4 text-body-lg text-text-secondary">Ready to transform your school&apos;s management system? We&apos;re here to help you get started and answer any questions you might have.</p>
      </div>
      <ul className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {CARDS.map((c) => (
          <Card as="li" key={c.title} className="rounded-xl p-6">
            <span className="grid size-11 place-items-center rounded-lg bg-primary-subtle-bg text-primary"><c.icon aria-hidden className="size-5" /></span>
            <h2 className="mt-4 font-bold text-text-primary">{c.title}</h2>
            <p className="mt-2 text-body-sm text-text-secondary">{c.lines.map((l) => <span key={l} className="block">{l}</span>)}</p>
          </Card>
        ))}
      </ul>
      <div className="mt-14 grid gap-6 lg:grid-cols-5">
        <Card className="rounded-xl p-8 lg:col-span-3">
          <h2 className="text-h2 text-text-primary">Send us a message</h2>
          <form onSubmit={submit} className="mt-6 grid gap-5 sm:grid-cols-2">
            <div className="sm:col-span-2"><ErrorBanner>{error}</ErrorBanner></div>
            <TextField label="Full Name" required value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} autoComplete="name" />
            <TextField label="Email Address" required type="email" value={form.email} autoComplete="email"
              error={emailTouched ? emailError(form.email) : undefined}
              onBlur={() => setEmailTouched(true)} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            <TextField label="School / Institution" value={form.institution} onChange={(e) => setForm({ ...form, institution: e.target.value })} autoComplete="organization" />
            <SelectField label="Number of Students" value={form.studentCountRange} onChange={(e) => setForm({ ...form, studentCountRange: e.target.value })}>
              <option value="">Select range</option>
              {RANGES.map((r) => <option key={r}>{r}</option>)}
            </SelectField>
            <TextareaField className="sm:col-span-2" label="Message" required rows={5} value={form.message} onChange={(e) => setForm({ ...form, message: e.target.value })} />
            <div className="sm:col-span-2"><Button type="submit" size="lg" loading={loading}>Send Message</Button></div>
          </form>
        </Card>
        <div className="space-y-4 lg:col-span-2">
          <Card className="rounded-xl p-7">
            <h2 className="text-lg font-bold text-text-primary">Let&apos;s Start a Conversation</h2>
            <dl className="mt-5 space-y-5 text-body-sm">
              <div><dt className="font-semibold text-text-primary">Quick Response</dt><dd className="mt-1 text-text-secondary">We typically respond to all inquiries within 24 hours during business days.</dd></div>
              <div><dt className="font-semibold text-text-primary">Free Consultation</dt><dd className="mt-1 text-text-secondary">Schedule a free 30-minute consultation to discuss your school&apos;s specific needs.</dd></div>
              <div><dt className="font-semibold text-text-primary">Custom Demo</dt><dd className="mt-1 text-text-secondary">Get a personalized demo tailored to your institution&apos;s requirements.</dd></div>
            </dl>
          </Card>
          <div className="rounded-xl bg-primary p-7 text-white">
            <p className="font-bold">Prefer to call?</p>
            <p className="mt-2 text-h2">+91 70147 91203</p>
            <p className="mt-1 text-body-sm">Available Everyday, 10AM–6PM IST</p>
          </div>
        </div>
      </div>
    </div>
  );
}
