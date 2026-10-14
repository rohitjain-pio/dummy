import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';
import { useSession } from '@/auth/session';
import { Avatar, Button, Card, ConfirmDialog, EmptyState, ErrorBanner, LoadingState, Modal, PageHeader, TextareaField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, errorMessage, idempotencyKey } from '@/lib/api';
import { formatDate, localDate, personLabel } from '@/lib/queries';
import { isLeadership, LEADERSHIP_AND_TEACHER } from '@/lib/roles';
import type { Announcement } from '@/lib/types';
import { SearchBox } from './Teachers';

// UI-011 — Announcements (API-037–040). View: every institution role. Post/edit: leadership + Teacher
// (BR-037). Withdraw (delete icon): leadership ONLY (BR-038) — not rendered for Teacher at all.
export function AnnouncementsPage() {
  const user = useSession((s) => s.user)!;
  const canPost = LEADERSHIP_AND_TEACHER.includes(user.role);
  const canWithdraw = isLeadership(user.role);
  const [params, setParams] = useSearchParams();
  const list = useQuery({ queryKey: ['announcements'], queryFn: () => api.get<Announcement[]>('/announcements') });
  const [search, setSearch] = useState('');
  const [viewing, setViewing] = useState<Announcement | null>(null);
  const [editing, setEditing] = useState<Announcement | 'new' | null>(params.get('new') && canPost ? 'new' : null);
  const [withdrawing, setWithdrawing] = useState<Announcement | null>(null);
  const qc = useQueryClient();

  const withdraw = useMutation({
    mutationFn: (a: Announcement) => api.post(`/announcements/${a.id}/withdraw`),
    onSuccess: () => { toast.success('Announcement withdrawn'); setWithdrawing(null); void qc.invalidateQueries({ queryKey: ['announcements'] }); },
    onError: (e) => { toast.error(errorMessage(e)); setWithdrawing(null); }, // ERR-501 on stale client state
  });

  const filtered = useMemo(() => {
    const s = search.trim().toLowerCase();
    return [...(list.data ?? [])]
      .filter((a) => !s || a.title.toLowerCase().includes(s) || a.content.toLowerCase().includes(s))
      .sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate));
  }, [list.data, search]);

  const closeEditor = () => { setEditing(null); if (params.get('new')) setParams({}); };

  return (
    <>
      <PageHeader title="Announcements" description="Notices for everyone in your school."
        actions={canPost && <Button onClick={() => setEditing('new')}><Plus aria-hidden className="size-4" />New Announcement</Button>} />
      <div className="mb-6"><SearchBox label="Search announcements" value={search} onChange={setSearch} /></div>
      {list.isPending && <LoadingState label="Loading announcements" variant="cards" rows={3} />}
      {list.isError && <ErrorBanner>{errorMessage(list.error)}</ErrorBanner>}
      {list.isSuccess && filtered.length === 0 && <EmptyState title="No announcements found" description="Nothing matches your search right now." />}
      {filtered.length > 0 && (
        <ul className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {filtered.map((a) => {
            const author = personLabel(a.authorId, user.id);
            return (
              <Card as="li" key={a.id} className="flex flex-col p-5">
                <div className="flex items-center justify-between gap-2">
                  <span className="flex items-center gap-2.5"><Avatar name={author} /><span className="text-body-sm"><span className="block font-medium text-text-primary">{author}</span><span className="block text-caption text-text-secondary">{formatDate(a.effectiveDate)}</span></span></span>
                  <span className="flex">
                    <Button variant="ghost" size="icon" aria-label={`View ${a.title}`} onClick={() => setViewing(a)}><Eye aria-hidden className="size-4" /></Button>
                    {canPost && <Button variant="ghost" size="icon" aria-label={`Edit ${a.title}`} onClick={() => setEditing(a)}><Pencil aria-hidden className="size-4" /></Button>}
                    {canWithdraw && <Button variant="ghost" size="icon" aria-label={`Withdraw ${a.title}`} onClick={() => setWithdrawing(a)}><Trash2 aria-hidden className="size-4" /></Button>}
                  </span>
                </div>
                <h3 className="mt-4 font-bold text-text-primary">{a.title}</h3>
                <p className="mt-2 line-clamp-3 flex-1 text-body-sm text-text-secondary">{a.content}</p>
                <button type="button" onClick={() => setViewing(a)} className="mt-4 self-start text-body-sm font-semibold text-primary hover:underline">Read more<span className="sr-only">: {a.title}</span></button>
              </Card>
            );
          })}
        </ul>
      )}
      {viewing && (
        <Modal open onOpenChange={(o) => !o && setViewing(null)} title={viewing.title} description={`${personLabel(viewing.authorId, user.id)} · ${formatDate(viewing.effectiveDate)}`} size="lg">
          <p className="whitespace-pre-wrap text-body text-text-primary">{viewing.content}</p>
          <p className="mt-6 text-caption text-text-secondary">Visible to all members of your school.</p>
        </Modal>
      )}
      {editing && <AnnouncementModal existing={editing === 'new' ? null : editing} onClose={closeEditor} />}
      <ConfirmDialog open={!!withdrawing} onOpenChange={(o) => !o && setWithdrawing(null)} subject={withdrawing ? `“${withdrawing.title}”` : ''} actionLabel="Withdraw"
        loading={withdraw.isPending} onConfirm={() => withdrawing && withdraw.mutate(withdrawing)} />
    </>
  );
}

function AnnouncementModal({ existing, onClose }: { existing: Announcement | null; onClose: () => void }) {
  const qc = useQueryClient();
  const [title, setTitle] = useState(existing?.title ?? '');
  const [content, setContent] = useState(existing?.content ?? '');
  const [effectiveDate, setEffectiveDate] = useState(existing?.effectiveDate ?? localDate());
  const [error, setError] = useState<string>();
  const [key] = useState(idempotencyKey); // one key per compose — a double submit cannot create two
  const save = useMutation({
    mutationFn: () => existing
      ? api.put(`/announcements/${existing.id}`, { title, content, effectiveDate })
      : api.post('/announcements', { title, content, effectiveDate }, { headers: key }),
    onSuccess: () => { toast.success(existing ? 'Announcement updated' : 'Announcement published'); void qc.invalidateQueries({ queryKey: ['announcements'] }); onClose(); },
    onError: (e) => setError(errorMessage(e)),
  });
  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={existing ? 'Edit announcement' : 'New announcement'} size="lg">
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); setError(undefined); save.mutate(); }}>
        <ErrorBanner>{error}</ErrorBanner>
        <TextField label="Title" required value={title} onChange={(e) => setTitle(e.target.value)} />
        <TextareaField label="Content" required rows={6} value={content} onChange={(e) => setContent(e.target.value)} />
        <TextField label="Effective date" type="date" required value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} />
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={save.isPending}>{existing ? 'Save changes' : 'Publish'}</Button>
        </div>
      </form>
    </Modal>
  );
}
