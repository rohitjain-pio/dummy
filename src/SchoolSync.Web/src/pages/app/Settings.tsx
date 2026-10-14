import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Bell, Globe, Settings2 } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useSession } from '@/auth/session';
import { Avatar, Button, Card, ErrorBanner, LoadingState, Modal, PageHeader, SelectField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, errorMessage } from '@/lib/api';
import { isLeadership, ROLE_LABEL } from '@/lib/roles';
import type { InstitutionConfig } from '@/lib/types';

// UI-018 — Settings (API-056–058). Role-gated: leadership sees institution configuration; every other
// role gets a materially smaller own-account screen — the leadership sections are NOT in the DOM.
// BR-059: every save is audited unconditionally — there is no audit checkbox.
// Q-236 gaps (filed): no API reads the school profile, performs account-management actions (FR-064),
// changes a password or stores per-account notification preferences — those cards are not shown.
export function SettingsPage() {
  const user = useSession((s) => s.user)!;
  return (
    <>
      <PageHeader title="Settings" description={isLeadership(user.role) ? 'Institution configuration and your account.' : 'Your account.'} />
      <AccountCard />
      {isLeadership(user.role) && <InstitutionSettings />}
    </>
  );
}

function AccountCard() {
  const user = useSession((s) => s.user)!;
  return (
    <Card className="mb-6 flex flex-wrap items-center gap-4 p-6">
      <Avatar name={user.username} className="size-14 text-h2" />
      <dl className="grid flex-1 gap-3 text-body-sm sm:grid-cols-2">
        <div><dt className="text-text-secondary">Username</dt><dd className="font-semibold text-text-primary">{user.username}</dd></div>
        <div><dt className="text-text-secondary">Role</dt><dd className="font-semibold text-text-primary">{ROLE_LABEL[user.role]}</dd></div>
      </dl>
    </Card>
  );
}

type Section = 'general' | 'notifications' | 'academic';

function InstitutionSettings() {
  const config = useQuery({ queryKey: ['institution-config'], queryFn: () => api.get<InstitutionConfig>('/institution-config') });
  const [open, setOpen] = useState<Section | null>(null);

  if (config.isPending) return <LoadingState label="Loading settings" variant="cards" rows={3} />;
  if (config.isError) return <ErrorBanner>{errorMessage(config.error)}</ErrorBanner>;
  const c = config.data;
  const sections: { key: Section; icon: typeof Globe; title: string; items: [string, string][] }[] = [
    { key: 'general', icon: Globe, title: 'General', items: [['Academic year', c.academicYear || 'Not set'], ['Time zone', c.timeZone], ['Language', c.language === 'en' ? 'English' : c.language]] },
    { key: 'notifications', icon: Bell, title: 'Notifications', items: [['Email', c.notifications.email ? 'On' : 'Off'], ['SMS', c.notifications.sms ? 'On' : 'Off'], ['In-platform', c.notifications.inPlatform ? 'On' : 'Off']] },
    { key: 'academic', icon: BookOpen, title: 'Academic', items: [['Grading scale', c.academic.gradingScale || 'Not set'], ['Course categories', c.academic.courseCategories.join(', ') || 'None'], ['Assessment types', c.academic.assessmentTypes.join(', ') || 'None'], ['Report card format', c.academic.reportCardFormat || 'Not set']] },
  ];
  return (
    <>
      <div className="grid gap-6 lg:grid-cols-3">
        {sections.map((s) => (
          <Card as="section" key={s.key} className="flex flex-col p-6">
            <div className="flex items-center gap-3">
              <span className="grid size-10 place-items-center rounded-md bg-primary-subtle-bg text-primary"><s.icon aria-hidden className="size-5" /></span>
              <h3 className="font-bold text-text-primary">{s.title}</h3>
            </div>
            <dl className="mt-4 flex-1 space-y-2 text-body-sm">
              {s.items.map(([k, v]) => <div key={k} className="flex justify-between gap-4"><dt className="text-text-secondary">{k}</dt><dd className="text-right font-medium text-text-primary">{v}</dd></div>)}
            </dl>
            <Button className="mt-5 self-start" variant="outline" size="sm" aria-label={`Configure ${s.title}`} onClick={() => setOpen(s.key)}><Settings2 aria-hidden className="size-4" />Configure</Button>
          </Card>
        ))}
      </div>
      <Card className="mt-6 p-6">
        <h3 className="font-bold text-text-primary">System Information</h3>
        <dl className="mt-3 grid gap-3 text-body-sm sm:grid-cols-3">
          <div><dt className="text-text-secondary">Version</dt><dd className="font-medium text-text-primary">1.0.0</dd></div>
          <div><dt className="text-text-secondary">Status</dt><dd className="font-medium text-text-primary">Operational</dd></div>
          <div><dt className="text-text-secondary">Configuration</dt><dd className="font-medium text-text-primary">{c.isDefault ? 'Defaults in use' : 'Customised'}</dd></div>
        </dl>
      </Card>
      {open && <ConfigureModal section={open} config={c} onClose={() => setOpen(null)} />}
    </>
  );
}

function ConfigureModal({ section, config, onClose }: { section: Section; config: InstitutionConfig; onClose: () => void }) {
  const qc = useQueryClient();
  const [general, setGeneral] = useState({ academicYear: config.academicYear, timeZone: config.timeZone, language: config.language });
  const [notif, setNotif] = useState(config.notifications);
  const [academic, setAcademic] = useState({ ...config.academic, courseCategories: config.academic.courseCategories.join(', '), assessmentTypes: config.academic.assessmentTypes.join(', ') });
  const [error, setError] = useState<string>();
  const list = (s: string) => s.split(',').map((x) => x.trim()).filter(Boolean);
  const zones = typeof Intl.supportedValuesOf === 'function' ? Intl.supportedValuesOf('timeZone') : [config.timeZone];

  const save = useMutation({
    mutationFn: () => {
      if (section === 'general') return api.put('/institution-config', general);
      if (section === 'notifications') return api.put('/institution-config/notifications', notif);
      return api.put('/institution-config/academic', { ...academic, courseCategories: list(academic.courseCategories), assessmentTypes: list(academic.assessmentTypes) });
    },
    onSuccess: () => { toast.success('Settings saved'); void qc.invalidateQueries({ queryKey: ['institution-config'] }); onClose(); },
    onError: (e) => setError(errorMessage(e)),
  });

  const title = { general: 'General settings', notifications: 'Notification preferences', academic: 'Academic settings' }[section];
  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={title} description="Changes apply immediately and are recorded in the audit log.">
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); setError(undefined); save.mutate(); }}>
        <ErrorBanner>{error}</ErrorBanner>
        {section === 'general' && (
          <>
            <TextField label="Academic year" required value={general.academicYear} placeholder="2026-27" onChange={(e) => setGeneral({ ...general, academicYear: e.target.value })} />
            <SelectField label="Time zone" required value={general.timeZone} onChange={(e) => setGeneral({ ...general, timeZone: e.target.value })}>
              {[...new Set([general.timeZone, ...zones])].map((z) => <option key={z}>{z}</option>)}
            </SelectField>
            <SelectField label="Language" required value={general.language} onChange={(e) => setGeneral({ ...general, language: e.target.value })}><option value="en">English</option></SelectField>
          </>
        )}
        {section === 'notifications' && (
          <fieldset className="space-y-3">
            <legend className="sr-only">Notification channels</legend>
            {([['email', 'Email'], ['sms', 'SMS'], ['inPlatform', 'In-platform']] as const).map(([k, l]) => (
              <label key={k} className="flex items-center justify-between gap-4 rounded-md border border-border px-4 py-3 text-body-sm">
                <span className="font-medium text-text-primary">{l}</span>
                <input type="checkbox" className="size-5 accent-primary" checked={notif[k]} onChange={(e) => setNotif({ ...notif, [k]: e.target.checked })} />
              </label>
            ))}
          </fieldset>
        )}
        {section === 'academic' && (
          <>
            <TextField label="Grading scale" required value={academic.gradingScale} onChange={(e) => setAcademic({ ...academic, gradingScale: e.target.value })} />
            <TextField label="Course categories" value={academic.courseCategories} hint="Separate with commas" onChange={(e) => setAcademic({ ...academic, courseCategories: e.target.value })} />
            <TextField label="Assessment types" value={academic.assessmentTypes} hint="Separate with commas" onChange={(e) => setAcademic({ ...academic, assessmentTypes: e.target.value })} />
            <TextField label="Report card format" required value={academic.reportCardFormat} onChange={(e) => setAcademic({ ...academic, reportCardFormat: e.target.value })} />
          </>
        )}
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={save.isPending}>Save</Button>
        </div>
      </form>
    </Modal>
  );
}
