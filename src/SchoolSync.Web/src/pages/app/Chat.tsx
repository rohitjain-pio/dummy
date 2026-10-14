import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, LogOut, Send, Video } from 'lucide-react';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useSession } from '@/auth/session';
import { Avatar, Button, Card, EmptyState, ErrorBanner, LoadingState, Skeleton } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, ApiError, errorMessage } from '@/lib/api';
import { cn } from '@/lib/cn';
import { parseUtc, personLabel } from '@/lib/queries';
import type { Message, Room } from '@/lib/types';
import { hubErrorCode, useRoomHub, type MessageAck, type MessageSentEvent } from '@/realtime/useRoomHub';
import { JoinRoomModal } from './Meetings';

// UI-013 — Chat (API-044/046; EVT-001–004, 006). Presence and typing are live hub events, not a
// static line (improved-beyond-mockup). Composer enforces CFG-003's 4,000 characters before send.
export const MAX_MESSAGE = 4000;

interface ChatMessage { key: string; id?: string; authorId: string; content: string; sentAtUtc: string; pending?: boolean; failed?: boolean }

export function ChatPage() {
  const { roomId } = useParams<{ roomId: string }>();
  const user = useSession((s) => s.user)!;
  const navigate = useNavigate();
  const qc = useQueryClient();
  const rooms = useQuery({ queryKey: ['rooms'], queryFn: () => api.get<Room[]>('/rooms') });
  const room = rooms.data?.find((r) => r.id === roomId);
  const history = useQuery({ queryKey: ['messages', roomId], queryFn: () => api.get<Message[]>(`/rooms/${roomId}/messages`), retry: false });
  const notMember = history.error instanceof ApiError && history.error.status === 403;

  const [live, setLive] = useState<ChatMessage[]>([]);
  const [online, setOnline] = useState<Set<string>>(new Set());
  const [typing, setTyping] = useState<Set<string>>(new Set());
  const [draft, setDraft] = useState('');
  const logRef = useRef<HTMLOListElement>(null);
  const typingSent = useRef(false);

  const { status, invoke } = useRoomHub(roomId, history.isSuccess, {
    onMessage: (e: MessageSentEvent) => setLive((prev) => {
      // de-duplicate: our own optimistic copy (by clientMessageId) or a repeat broadcast (by messageId)
      const without = prev.filter((m) => m.key !== e.clientMessageId && m.id !== e.messageId);
      return [...without, { key: e.messageId, id: e.messageId, authorId: e.authorId, content: e.content, sentAtUtc: e.sentAtUtc }];
    }),
    onPresence: (e) => setOnline((prev) => { const n = new Set(prev); if (e.status === 'online') n.add(e.userId); else n.delete(e.userId); return n; }),
    onTyping: (e) => setTyping((prev) => { const n = new Set(prev); if (e.isTyping) n.add(e.userId); else n.delete(e.userId); return n; }),
    onHistoryResync: () => { setLive((prev) => prev.filter((m) => m.pending || m.failed)); void qc.invalidateQueries({ queryKey: ['messages', roomId] }); },
  });

  const persisted: ChatMessage[] = (history.data ?? []).map((m) => ({ key: m.id, id: m.id, authorId: m.authorId, content: m.isWithdrawn ? '' : m.content, sentAtUtc: m.sentAtUtc }));
  const seen = new Set(persisted.map((m) => m.id));
  const messages = [...persisted, ...live.filter((m) => !m.id || !seen.has(m.id))];

  useEffect(() => { logRef.current?.lastElementChild?.scrollIntoView?.({ block: 'end' }); }, [messages.length]);

  async function send(content: string, clientMessageId: string = crypto.randomUUID()) {
    setLive((prev) => [...prev.filter((m) => m.key !== clientMessageId), { key: clientMessageId, authorId: user.id, content, sentAtUtc: new Date().toISOString(), pending: true }]);
    for (let attempt = 0; attempt < 2; attempt++) {
      try {
        const ack = await invoke<MessageAck>('SendMessage', roomId, clientMessageId, content);
        setLive((prev) => prev.map((m) => (m.key === clientMessageId ? { ...m, id: ack.messageId, pending: false } : m)));
        return;
      } catch (err) {
        const code = hubErrorCode(err);
        if (code === 'ERR-504' || code === 'ERR-102' || attempt === 1) {
          // a retry reuses the same clientMessageId, so the server never stores it twice
          setLive((prev) => prev.map((m) => (m.key === clientMessageId ? { ...m, pending: false, failed: true } : m)));
          toast.error(code === 'ERR-504' ? 'Messages can’t be longer than 4,000 characters.' : 'Message not sent. Try again.');
          return;
        }
      }
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    const content = draft.trim();
    if (!content || content.length > MAX_MESSAGE) return;
    setDraft('');
    void send(content);
    void invoke('Typing', roomId, false).catch(() => undefined);
    typingSent.current = false;
  }

  function onDraft(value: string) {
    setDraft(value);
    const isTyping = value.length > 0;
    if (isTyping !== typingSent.current) {
      typingSent.current = isTyping;
      void invoke('Typing', roomId, isTyping).catch(() => undefined);
    }
  }

  async function leave() {
    await invoke('LeaveRoom', roomId).catch(() => undefined);
    navigate('/dashboard/meeting');
  }

  if (notMember && room) {
    return <JoinRoomModal room={room} then="stay" onClose={(joined) => (joined ? void history.refetch() : navigate('/dashboard/meeting'))} />;
  }
  if (history.isError && !notMember) return <ErrorBanner>{errorMessage(history.error)}</ErrorBanner>;

  const tooLong = draft.length > MAX_MESSAGE;
  const others = [...typing].filter((id) => id !== user.id);
  const participants = [user.id, ...[...online].filter((id) => id !== user.id)];

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_16rem]">
      <Card className="flex min-h-[70vh] flex-col">
        <div className="flex flex-wrap items-center gap-3 border-b border-border-subtle px-4 py-3">
          <Link to="/dashboard/meeting" aria-label="Back to meetings" className="grid size-9 place-items-center rounded-md text-text-secondary hover:bg-background"><ArrowLeft aria-hidden className="size-5" /></Link>
          <div className="min-w-0 flex-1">
            <h2 className="truncate font-bold text-text-primary">{room?.name ?? 'Room'}</h2>
            <p className="text-caption text-text-secondary">{participants.length} online</p>
          </div>
          <Link to={`/video-call/${roomId}`} className="inline-flex h-9 items-center gap-2 rounded-md bg-primary px-3 text-body-sm font-semibold text-white hover:bg-primary-hover"><Video aria-hidden className="size-4" />Start Video Call</Link>
          <Button size="sm" variant="outline" onClick={leave}><LogOut aria-hidden className="size-4" />Leave Room</Button>
        </div>
        {status !== 'connected' && history.isSuccess && (
          <p role="status" className="bg-warning-bg px-4 py-2 text-body-sm text-warning-text">
            {status === 'connecting' ? 'Connecting to chat…' : status === 'reconnecting' ? 'Connection lost — reconnecting…' : 'Disconnected. Messages will not send until the connection is restored.'}
          </p>
        )}
        <div className="flex-1 overflow-y-auto px-4 py-4">
          {history.isPending ? (
            <div role="status" aria-live="polite" className="space-y-3"><span className="sr-only">Loading messages…</span>{[1, 2, 3].map((i) => <Skeleton key={i} className={cn('h-12', i % 2 ? 'w-2/3' : 'ml-auto w-1/2')} />)}</div>
          ) : messages.length === 0 ? (
            <EmptyState title="No messages yet" description="Say hello." />
          ) : (
            <>
              <p className="mb-4 text-center text-caption text-text-secondary">Earlier messages loaded</p>
              <ol ref={logRef} aria-label="Messages" className="space-y-3">
                {messages.map((m) => {
                  const mine = m.authorId === user.id;
                  return (
                    <li key={m.key} className={cn('flex gap-2.5', mine && 'flex-row-reverse')}>
                      <Avatar name={personLabel(m.authorId, user.id)} />
                      <div className={cn('max-w-[75%] rounded-lg px-3.5 py-2', mine ? 'bg-primary text-white' : 'bg-background text-text-primary')}>
                        <p className={cn('text-caption font-semibold', mine ? 'text-white' : 'text-text-secondary')}>{personLabel(m.authorId, user.id)}</p>
                        <p className="whitespace-pre-wrap break-words text-body-sm">{m.content || <em>Message withdrawn</em>}</p>
                        <p className={cn('mt-0.5 text-caption', mine ? 'text-white' : 'text-text-secondary')}>
                          {m.pending ? 'Sending…' : m.failed ? 'Not sent' : parseUtc(m.sentAtUtc).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' })}
                        </p>
                        {m.failed && <button type="button" className="text-caption font-semibold underline" onClick={() => void send(m.content, m.key)}>Retry</button>}
                      </div>
                    </li>
                  );
                })}
              </ol>
            </>
          )}
          <p aria-live="polite" className="mt-2 h-5 text-caption text-text-secondary">{others.length > 0 ? `${others.map((id) => personLabel(id, user.id)).join(', ')} ${others.length > 1 ? 'are' : 'is'} typing…` : ''}</p>
        </div>
        <form onSubmit={onSubmit} className="border-t border-border-subtle px-4 py-3">
          <div className="flex items-end gap-2">
            <label className="min-w-0 flex-1">
              <span className="sr-only">Message</span>
              <textarea value={draft} onChange={(e) => onDraft(e.target.value)} rows={2} placeholder="Type a message…" aria-invalid={tooLong || undefined} aria-describedby="composer-count"
                onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); onSubmit(e); } }}
                className="w-full resize-none rounded-md border border-border bg-surface px-3.5 py-2.5 text-body-sm text-text-primary outline-none focus:border-primary aria-[invalid=true]:border-error" />
            </label>
            <Button type="submit" disabled={!draft.trim() || tooLong || status !== 'connected'}><Send aria-hidden className="size-4" />Send</Button>
          </div>
          <p id="composer-count" aria-live="polite" className={cn('mt-1 text-right text-caption', tooLong ? 'text-error' : 'text-text-secondary')}>
            {tooLong ? `Messages can’t be longer than 4,000 characters (${draft.length.toLocaleString('en-IN')}).` : `${draft.length.toLocaleString('en-IN')} / 4,000`}
          </p>
        </form>
      </Card>
      <Card className="hidden p-4 lg:block">
        <h3 className="font-bold text-text-primary">Participants</h3>
        {rooms.isPending ? <LoadingState rows={2} /> : (
          <ul className="mt-3 space-y-2">
            {participants.map((id) => (
              <li key={id} className="flex items-center gap-2.5 text-body-sm">
                <span className="relative"><Avatar name={personLabel(id, user.id)} /><span className="absolute -bottom-0.5 -right-0.5 size-2.5 rounded-full border-2 border-surface bg-success" aria-hidden /></span>
                <span className="text-text-primary">{personLabel(id, user.id)}</span><span className="sr-only">online</span>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
