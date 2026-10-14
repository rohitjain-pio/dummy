import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import { refreshSession } from '@/lib/api';
import { useSession } from '@/auth/session';

// CT-303/603 — /hubs/collaboration (06-event-contracts.md EVT-001–006). Chat, presence, typing and
// media state flow over the hub, never REST polling. Reconnect is automatic; on every (re)join the
// server sends HistoryResync and the screen re-fetches history (NFR-012).
export type HubStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

export interface MessageSentEvent { roomId: string; messageId: string; clientMessageId: string | null; authorId: string; content: string; sentAtUtc: string }
export interface MessageAck { messageId: string; clientMessageId: string | null; sentAtUtc: string; wasDuplicate: boolean }

export interface RoomHubHandlers {
  onMessage?: (e: MessageSentEvent) => void;
  onPresence?: (e: { userId: string; status: 'online' | 'offline' }) => void;
  onTyping?: (e: { userId: string; isTyping: boolean }) => void;
  onMediaState?: (e: { userId: string; mic: boolean; camera: boolean }) => void;
  onHistoryResync?: () => void;
}

/** Parse the ERR-nnn code a HubException carries in its message. */
export const hubErrorCode = (err: unknown) => (err instanceof Error ? /ERR-\d{3}/.exec(err.message)?.[0] : undefined);

export function useRoomHub(roomId: string | undefined, enabled: boolean, handlers: RoomHubHandlers) {
  const [status, setStatus] = useState<HubStatus>('connecting');
  const connectionRef = useRef<HubConnection | null>(null);
  const handlersRef = useRef(handlers);
  useEffect(() => { handlersRef.current = handlers; });

  useEffect(() => {
    if (!roomId || !enabled) return;
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/collaboration', {
        // The hub accepts the JWT only as ?access_token for /hubs (ADR: CT-303). Refresh first if the in-memory token is gone.
        accessTokenFactory: async () => useSession.getState().accessToken ?? ((await refreshSession()) ? useSession.getState().accessToken ?? '' : ''),
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;

    connection.on('MessageSent', (e: MessageSentEvent) => e.roomId === roomId && handlersRef.current.onMessage?.(e));
    connection.on('PresenceChanged', (e: { roomId: string; userId: string; status: 'online' | 'offline' }) => e.roomId === roomId && handlersRef.current.onPresence?.(e));
    connection.on('TypingChanged', (e: { roomId: string; userId: string; isTyping: boolean }) => e.roomId === roomId && handlersRef.current.onTyping?.(e));
    connection.on('MediaStateChanged', (e: { roomId: string; userId: string; mic: boolean; camera: boolean }) => e.roomId === roomId && handlersRef.current.onMediaState?.(e));
    connection.on('HistoryResync', (e: { roomId: string }) => e.roomId === roomId && handlersRef.current.onHistoryResync?.());
    connection.on('ParticipantJoined', () => undefined);
    connection.on('ParticipantLeft', () => undefined);

    connection.onreconnecting(() => setStatus('reconnecting'));
    connection.onreconnected(async () => {
      await connection.invoke('JoinRoom', roomId).catch(() => undefined);
      setStatus('connected');
    });
    connection.onclose(() => setStatus('disconnected'));

    let cancelled = false;
    (async () => {
      try {
        setStatus('connecting');
        await connection.start();
        await connection.invoke('JoinRoom', roomId);
        if (!cancelled) setStatus('connected');
      } catch {
        if (!cancelled) setStatus('disconnected');
      }
    })();

    return () => {
      cancelled = true;
      connectionRef.current = null;
      void connection.stop();
    };
  }, [roomId, enabled]);

  const invoke = useCallback(async <T = void>(method: string, ...args: unknown[]): Promise<T> => {
    const c = connectionRef.current;
    if (!c || c.state !== HubConnectionState.Connected) throw new Error('ERR-903 not connected');
    return c.invoke<T>(method, ...args);
  }, []);

  return { status, invoke };
}
