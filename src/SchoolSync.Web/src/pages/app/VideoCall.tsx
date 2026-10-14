import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Loader2, MessageSquare, Mic, MicOff, MonitorUp, PhoneOff, Video, VideoOff, WifiOff } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useSession } from '@/auth/session';
import { Avatar } from '@/components/ui';
import { api, ApiError } from '@/lib/api';
import { cn } from '@/lib/cn';
import type { MeetingToken, Room } from '@/lib/types';
import { createCallingAdapter, type CallErrorKind, type CallHandle, type CallParticipant, type CallState } from '@/meetings/callingAdapter';
import { useRoomHub } from '@/realtime/useRoomHub';

// UI-014 — Video call (API-047; EVT-005). Dark full-screen. The connecting / reconnecting / error
// states are the mandated defensive posture for Tech Design §13.1's ACS risks, not polish:
//  - connecting: a per-tile spinner until each participant's stream resolves
//  - reconnecting: a dropped participant keeps their tile with an overlay — it never vanishes
//  - error: camera/mic denied shows an inline "how to enable" message on your own tile
export const DROP_GRACE_MS = 30_000;

type Tile = CallParticipant & { droppedAt?: number };

export function VideoCallPage() {
  const { roomId } = useParams<{ roomId: string }>();
  const user = useSession((s) => s.user)!;
  const navigate = useNavigate();
  const rooms = useQuery({ queryKey: ['rooms'], queryFn: () => api.get<Room[]>('/rooms') });
  const room = rooms.data?.find((r) => r.id === roomId);

  const [callState, setCallState] = useState<CallState>('connecting');
  const [tiles, setTiles] = useState<Tile[]>([]);
  const [fatal, setFatal] = useState<string>();
  const [deviceError, setDeviceError] = useState<string>();
  const [mic, setMic] = useState(true);
  const [camera, setCamera] = useState(true);
  const [sharing, setSharing] = useState(false);
  const handle = useRef<CallHandle | null>(null);

  // EVT-005: our own mic/camera state is broadcast to the room; a remote tile's muted flag comes from
  // the SDK itself, since hub events carry app user ids and ACS tiles carry ACS identities.
  const { invoke } = useRoomHub(roomId, true, {});

  useEffect(() => {
    let disposed = false;
    (async () => {
      try {
        const token = await api.post<MeetingToken>(`/rooms/${roomId}/meetings/token`);
        const adapter = await createCallingAdapter();
        const h = await adapter.join({ acsToken: token.acsToken, groupCallId: token.groupCallId, displayName: user.username }, {
          onCallState: (s) => !disposed && setCallState(s),
          onParticipants: (list) => !disposed && setTiles((prev) => mergeTiles(prev, list, Date.now())),
          onError: (kind: CallErrorKind, message) => {
            if (disposed) return;
            if (kind === 'permission-denied') { setDeviceError(message); setCamera(false); setMic(false); }
            else setFatal(message);
          },
        });
        if (disposed) void h.leave(); else handle.current = h;
      } catch (err) {
        if (disposed) return;
        if (err instanceof ApiError && err.code === 'ERR-505') setFatal('Join this room before joining its meeting.');
        else if (err instanceof ApiError && err.status === 503) setFatal('Meetings are unavailable right now. Chat is still available.');
        else setFatal((prev) => prev ?? 'We couldn’t start the call. Please try again.');
      }
    })();
    return () => { disposed = true; void handle.current?.leave(); handle.current = null; };
  }, [roomId, user.username]);

  // drop tiles whose grace period has expired
  useEffect(() => {
    if (!tiles.some((t) => t.droppedAt)) return;
    const id = setInterval(() => setTiles((prev) => prev.filter((t) => !t.droppedAt || Date.now() - t.droppedAt < DROP_GRACE_MS)), 1000);
    return () => clearInterval(id);
  }, [tiles]);

  async function toggle(kind: 'mic' | 'camera' | 'screen') {
    const h = handle.current;
    if (!h) return;
    if (kind === 'mic') { const next = !mic; await h.setMicrophone(next); setMic(next); void invoke('SetMediaState', roomId, next, camera).catch(() => undefined); }
    if (kind === 'camera') { const next = !camera; await h.setCamera(next); setCamera(next); void invoke('SetMediaState', roomId, mic, next).catch(() => undefined); }
    if (kind === 'screen') { const next = !sharing; await h.setScreenShare(next); setSharing(next); }
  }

  async function leaveCall() {
    await handle.current?.leave();
    handle.current = null;
    navigate('/dashboard/meeting'); // no confirmation — live session, no unsaved form state
  }

  const count = tiles.length + 1;
  return (
    <main id="main" className="flex min-h-screen flex-col bg-text-primary text-white">
      <header className="flex flex-wrap items-center gap-3 border-b border-white/10 px-4 py-3">
        <Link to={`/chat/${roomId}`} aria-label="Back to chat" className="grid size-10 place-items-center rounded-md hover:bg-white/10"><ArrowLeft aria-hidden className="size-5" /></Link>
        <h1 className="min-w-0 flex-1 truncate font-bold">{room?.name ?? 'Video call'}</h1>
        <span className="text-body-sm text-text-quaternary">{count} {count === 1 ? 'participant' : 'participants'}</span>
        {callState === 'connected' && <span className="rounded-full bg-error px-2.5 py-0.5 text-caption font-bold">LIVE</span>}
      </header>

      <p role="status" aria-live="polite" className="sr-only">
        {fatal ?? (callState === 'connecting' ? 'Connecting to the call' : callState === 'reconnecting' ? 'Connection lost, reconnecting' : callState === 'connected' ? 'Connected' : 'Call ended')}
      </p>

      {fatal ? (
        <div className="grid flex-1 place-items-center px-4">
          <div role="alert" className="max-w-md rounded-lg bg-white/10 p-6 text-center">
            <p className="font-semibold">{fatal}</p>
            <div className="mt-4 flex justify-center gap-3">
              <Link to={`/chat/${roomId}`} className="inline-flex h-10 items-center rounded-md bg-surface px-4 text-body-sm font-semibold text-text-primary">Go to chat</Link>
              <Link to="/dashboard/meeting" className="inline-flex h-10 items-center rounded-md border border-white/30 px-4 text-body-sm font-semibold">Back to rooms</Link>
            </div>
          </div>
        </div>
      ) : (
        <>
          {callState === 'reconnecting' && <p className="bg-warning-bg px-4 py-2 text-center text-body-sm text-warning-text">Your connection dropped — reconnecting…</p>}
          <ul className="grid flex-1 auto-rows-fr gap-3 p-4 sm:grid-cols-2 xl:grid-cols-3" aria-label="Participants">
            <li data-testid="tile-self" className="relative grid min-h-48 place-items-center overflow-hidden rounded-lg bg-white/5">
              {callState === 'connecting' ? <TileSpinner label="Connecting" /> : <Avatar name={user.username} className="size-20 text-h2" />}
              {deviceError && (
                <div role="alert" className="absolute inset-x-3 top-3 rounded-md bg-warning-bg p-3 text-body-sm text-warning-text">
                  <p className="font-semibold">{deviceError}</p>
                  <p className="mt-1">To enable it, click the camera icon in your browser&apos;s address bar, allow camera and microphone for this site, then rejoin the call.</p>
                </div>
              )}
              <TileLabel name={`${user.username} (You)`} muted={!mic} />
            </li>
            {tiles.map((t) => (
              <ParticipantTile key={t.id} tile={t} muted={t.muted} />
            ))}
          </ul>
        </>
      )}

      <nav aria-label="Call controls" className="flex flex-wrap items-center justify-center gap-3 border-t border-white/10 px-4 py-4">
        <ControlButton label={mic ? 'Mute microphone' : 'Unmute microphone'} pressed={!mic} onClick={() => toggle('mic')} disabled={!!fatal || !!deviceError}>{mic ? <Mic className="size-5" /> : <MicOff className="size-5" />}</ControlButton>
        <ControlButton label={camera ? 'Turn camera off' : 'Turn camera on'} pressed={!camera} onClick={() => toggle('camera')} disabled={!!fatal || !!deviceError}>{camera ? <Video className="size-5" /> : <VideoOff className="size-5" />}</ControlButton>
        <ControlButton label={sharing ? 'Stop sharing screen' : 'Share screen'} pressed={sharing} onClick={() => toggle('screen')} disabled={!!fatal}><MonitorUp className="size-5" /></ControlButton>
        <Link to={`/chat/${roomId}`} aria-label="Open chat" className="grid size-12 place-items-center rounded-full bg-white/10 hover:bg-white/20"><MessageSquare aria-hidden className="size-5" /></Link>
        <button type="button" onClick={leaveCall} className="inline-flex h-12 items-center gap-2 rounded-full bg-error px-5 font-semibold text-white hover:opacity-90"><PhoneOff aria-hidden className="size-5" />Leave Call</button>
      </nav>
    </main>
  );
}

/** Keeps a participant who disappears from the SDK's list as a "reconnecting" tile for DROP_GRACE_MS. */
export function mergeTiles(prev: Tile[], incoming: CallParticipant[], now: number): Tile[] {
  const ids = new Set(incoming.map((p) => p.id));
  const kept = prev.filter((t) => !ids.has(t.id)).map((t) => ({ ...t, state: 'reconnecting' as const, droppedAt: t.droppedAt ?? now }));
  return [...incoming.map((p) => ({ ...p, droppedAt: undefined })), ...kept];
}

function ParticipantTile({ tile, muted }: { tile: Tile; muted: boolean }) {
  const videoRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (tile.state !== 'connected' || !tile.attachVideo || !videoRef.current) return;
    let detach: (() => void) | undefined;
    let cancelled = false;
    void tile.attachVideo(videoRef.current).then((d) => { if (cancelled) d(); else detach = d; });
    return () => { cancelled = true; detach?.(); };
  }, [tile]);
  return (
    <li data-testid={`tile-${tile.id}`} data-state={tile.state} className="relative grid min-h-48 place-items-center overflow-hidden rounded-lg bg-white/5">
      <div ref={videoRef} className="absolute inset-0" aria-hidden />
      {tile.state === 'connecting' ? <TileSpinner label={`Connecting ${tile.displayName}`} /> : <Avatar name={tile.displayName} className="size-20 text-h2" />}
      {tile.state === 'reconnecting' && (
        <div className="absolute inset-0 grid place-items-center bg-text-primary/70">
          <span className="flex items-center gap-2 text-body-sm font-semibold"><WifiOff aria-hidden className="size-5" />Reconnecting…</span>
        </div>
      )}
      <TileLabel name={tile.displayName} muted={muted} />
    </li>
  );
}

function TileSpinner({ label }: { label: string }) {
  return <span className="flex flex-col items-center gap-2 text-body-sm"><Loader2 aria-hidden className="size-8 animate-spin" />{label}…</span>;
}

function TileLabel({ name, muted }: { name: string; muted: boolean }) {
  return (
    <span className="absolute bottom-2 left-2 flex items-center gap-1.5 rounded-md bg-text-primary/80 px-2 py-1 text-caption font-semibold">
      {muted && <><MicOff aria-hidden className="size-3.5" /><span className="sr-only">Muted:</span></>}{name}
    </span>
  );
}

function ControlButton({ label, pressed, onClick, disabled, children }: { label: string; pressed: boolean; onClick: () => void; disabled?: boolean; children: React.ReactNode }) {
  return (
    <button type="button" aria-label={label} aria-pressed={pressed} disabled={disabled} onClick={onClick}
      className={cn('grid size-12 place-items-center rounded-full disabled:opacity-40', pressed ? 'bg-surface text-text-primary' : 'bg-white/10 hover:bg-white/20')}>
      <span aria-hidden>{children}</span>
    </button>
  );
}
