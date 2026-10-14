import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Lock, MessageSquare, Plus, Trash2, Video } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { useSession } from '@/auth/session';
import { Badge, Button, Card, ConfirmDialog, EmptyState, ErrorBanner, LoadingState, Modal, PageHeader, TextareaField, TextField } from '@/components/ui';
import { toast } from '@/components/ui/toast';
import { api, errorMessage, idempotencyKey } from '@/lib/api';
import { personLabel } from '@/lib/queries';
import { isLeadership } from '@/lib/roles';
import type { Room } from '@/lib/types';

// UI-012 — Meetings / room list (API-041–043, 045). Every role (BR-040). The passcode value is never
// shown — only a "passcode protected" badge. Delete only for creator or leadership (BR-042).
// Q-236: participant avatar stack/count have no API and are not shown (filed).
export function MeetingsPage() {
  const user = useSession((s) => s.user)!;
  const rooms = useQuery({ queryKey: ['rooms'], queryFn: () => api.get<Room[]>('/rooms') });
  const [creating, setCreating] = useState(false);
  const [joining, setJoining] = useState<{ room: Room; then: 'chat' | 'video' } | null>(null);
  const [deleting, setDeleting] = useState<Room | null>(null);
  const qc = useQueryClient();

  const remove = useMutation({
    mutationFn: (r: Room) => api.del(`/rooms/${r.id}`),
    onSuccess: () => { toast.success('Room deleted'); setDeleting(null); void qc.invalidateQueries({ queryKey: ['rooms'] }); },
    onError: (e) => { toast.error(errorMessage(e)); setDeleting(null); }, // ERR-503
  });

  const canDelete = (r: Room) => r.createdByUserId === user.id || isLeadership(user.role);

  return (
    <>
      <PageHeader title="Meetings" description="Collaboration rooms for chat and video."
        actions={<Button onClick={() => setCreating(true)}><Plus aria-hidden className="size-4" />Create Room</Button>} />
      {rooms.isPending && <LoadingState label="Loading rooms" variant="cards" rows={3} />}
      {rooms.isError && <ErrorBanner>{errorMessage(rooms.error)}</ErrorBanner>}
      {rooms.isSuccess && rooms.data.length === 0 && <EmptyState title="No rooms yet" description="Create one to get started." />}
      {rooms.isSuccess && rooms.data.length > 0 && (
        <ul className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {rooms.data.map((r) => (
            <Card as="li" key={r.id} className="flex flex-col p-5">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <h3 className="font-bold text-text-primary">{r.name}</h3>
                  <p className="text-caption text-text-secondary">Created by {personLabel(r.createdByUserId, user.id)}</p>
                </div>
                {canDelete(r) && <Button variant="ghost" size="icon" aria-label={`Delete room ${r.name}`} onClick={() => setDeleting(r)}><Trash2 aria-hidden className="size-4" /></Button>}
              </div>
              {r.description && <p className="mt-3 text-body-sm text-text-secondary">{r.description}</p>}
              <div className="mt-3"><Badge tone="neutral"><Lock aria-hidden className="size-3" />Passcode protected</Badge></div>
              <div className="mt-5 flex flex-wrap gap-2">
                <Button size="sm" onClick={() => setJoining({ room: r, then: 'chat' })}>Join</Button>
                <Button size="sm" variant="outline" onClick={() => setJoining({ room: r, then: 'chat' })}><MessageSquare aria-hidden className="size-4" />Chat</Button>
                <Button size="sm" variant="outline" onClick={() => setJoining({ room: r, then: 'video' })}><Video aria-hidden className="size-4" />Video</Button>
              </div>
            </Card>
          ))}
        </ul>
      )}
      {creating && <CreateRoomModal onClose={() => setCreating(false)} />}
      {joining && <JoinRoomModal room={joining.room} then={joining.then} onClose={() => setJoining(null)} />}
      <ConfirmDialog open={!!deleting} onOpenChange={(o) => !o && setDeleting(null)} subject={deleting ? `room “${deleting.name}”` : ''} loading={remove.isPending} onConfirm={() => deleting && remove.mutate(deleting)} />
    </>
  );
}

function CreateRoomModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const [f, setF] = useState({ name: '', description: '', passcode: '' });
  const [error, setError] = useState<string>();
  const [key] = useState(idempotencyKey);
  const create = useMutation({
    mutationFn: () => api.post<Room>('/rooms', { ...f, description: f.description || null }, { headers: key }),
    onSuccess: () => { toast.success('Room created'); void qc.invalidateQueries({ queryKey: ['rooms'] }); onClose(); },
    onError: (e) => setError(errorMessage(e)),
  });
  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title="Create room">
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); setError(undefined); create.mutate(); }}>
        <ErrorBanner>{error}</ErrorBanner>
        <TextField label="Room name" required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} />
        <TextareaField label="Description" rows={3} value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} />
        <TextField label="Passcode" required type="password" autoComplete="new-password" value={f.passcode} onChange={(e) => setF({ ...f, passcode: e.target.value })} hint="Share it with the people you want in this room." />
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={create.isPending}>Create room</Button>
        </div>
      </form>
    </Modal>
  );
}

export function JoinRoomModal({ room, then, onClose }: { room: Room; then: 'chat' | 'video' | 'stay'; onClose: (joined?: boolean) => void }) {
  const navigate = useNavigate();
  const [passcode, setPasscode] = useState('');
  const [error, setError] = useState<string>();
  const join = useMutation({
    mutationFn: () => api.post(`/rooms/${room.id}/join`, { passcode }),
    onSuccess: () => {
      onClose(true);
      if (then === 'chat') navigate(`/chat/${room.id}`);
      if (then === 'video') navigate(`/video-call/${room.id}`);
    },
    onError: (e) => { setError(errorMessage(e)); setPasscode(''); }, // ERR-502 inline
  });
  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title="Join room">
      <form className="space-y-4" onSubmit={(e: FormEvent) => { e.preventDefault(); setError(undefined); join.mutate(); }}>
        <TextField label="Room" value={room.name} readOnly />
        <TextField label="Passcode" required type="password" autoComplete="off" value={passcode} error={error} onChange={(e) => setPasscode(e.target.value)} />
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => onClose()}>Cancel</Button>
          <Button type="submit" loading={join.isPending}>Join</Button>
        </div>
      </form>
    </Modal>
  );
}
