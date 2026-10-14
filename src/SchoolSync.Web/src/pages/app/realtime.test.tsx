import { act, cleanup, fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createFakeCallingAdapter } from '@/meetings/fakeCallingAdapter';
import { mockApi, renderRoute, signIn } from '@/test/render';
import type { RoomHubHandlers } from '@/realtime/useRoomHub';

// The hub is replaced with a controllable stub; the real hub protocol is covered by the backend's
// real-SignalR integration tests (CT-303).
const hub = vi.hoisted(() => ({ status: 'connected' as string, handlers: {} as RoomHubHandlers, invoke: vi.fn() }));
vi.mock('@/realtime/useRoomHub', async (orig) => ({
  ...(await orig<typeof import('@/realtime/useRoomHub')>()),
  useRoomHub: (_roomId: string, _enabled: boolean, handlers: RoomHubHandlers) => { hub.handlers = handlers; return { status: hub.status, invoke: hub.invoke }; },
}));

const fake = vi.hoisted(() => ({ adapter: null as ReturnType<typeof createFakeCallingAdapter> | null }));
vi.mock('@/meetings/callingAdapter', async (orig) => ({
  ...(await orig<typeof import('@/meetings/callingAdapter')>()),
  createCallingAdapter: async () => fake.adapter!,
}));

const { ChatPage, MAX_MESSAGE } = await import('./Chat');
const { VideoCallPage, mergeTiles } = await import('./VideoCall');

const ROOM = { id: 'r1', name: 'Staff room', description: null, createdByUserId: 'x' };

beforeEach(() => { hub.status = 'connected'; hub.invoke.mockReset(); hub.invoke.mockResolvedValue(undefined); signIn('Student'); });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('UI-013 chat', () => {
  it('guards the 4,000 character limit before send (ERR-504 mirror) and shows a live counter', async () => {
    mockApi({ 'GET /rooms': [ROOM], 'GET /rooms/r1/messages': [] });
    renderRoute(<ChatPage />, '/chat/r1', '/chat/:roomId');
    const box = await screen.findByLabelText('Message');
    fireEvent.change(box, { target: { value: 'x'.repeat(MAX_MESSAGE + 1) } });
    expect(screen.getByText(/can’t be longer than 4,000 characters/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send' })).toBeDisabled();
    fireEvent.change(box, { target: { value: 'hello' } });
    expect(screen.getByText('5 / 4,000')).toBeInTheDocument();
  });

  it('sends with a client message id, retries once with the SAME id, and de-duplicates the broadcast', async () => {
    mockApi({ 'GET /rooms': [ROOM], 'GET /rooms/r1/messages': [] });
    let attempt = 0;
    hub.invoke.mockImplementation(async (method: string, _room: string, clientId: string) => {
      if (method !== 'SendMessage') return undefined;
      if (attempt++ === 0) throw new Error('transient');
      return { messageId: 'm1', clientMessageId: clientId, sentAtUtc: '2026-09-14T06:00:00Z', wasDuplicate: false };
    });
    renderRoute(<ChatPage />, '/chat/r1', '/chat/:roomId');
    fireEvent.change(await screen.findByLabelText('Message'), { target: { value: 'hello' } });
    fireEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(hub.invoke.mock.calls.filter(([m]) => m === 'SendMessage')).toHaveLength(2));
    const ids = hub.invoke.mock.calls.filter(([m]) => m === 'SendMessage').map((c) => c[2]);
    expect(ids[0]).toBe(ids[1]);
    act(() => hub.handlers.onMessage?.({ roomId: 'r1', messageId: 'm1', clientMessageId: ids[0], authorId: 'u', content: 'hello', sentAtUtc: '2026-09-14T06:00:00Z' }));
    expect(screen.getAllByText('hello', { selector: 'p' })).toHaveLength(1);
  });

  it('shows live typing and a reconnecting banner', async () => {
    mockApi({ 'GET /rooms': [ROOM], 'GET /rooms/r1/messages': [] });
    hub.status = 'reconnecting';
    renderRoute(<ChatPage />, '/chat/r1', '/chat/:roomId');
    expect(await screen.findByText(/reconnecting/)).toBeInTheDocument();
    act(() => hub.handlers.onTyping?.({ userId: 'abcd-1234', isTyping: true }));
    expect(screen.getByText(/Member ABCD is typing/)).toBeInTheDocument();
    act(() => hub.handlers.onTyping?.({ userId: 'abcd-1234', isTyping: false }));
    expect(screen.queryByText(/is typing/)).toBeNull();
  });
});

describe('UI-014 video call with the fake ACS adapter (TC-508/509)', () => {
  beforeEach(() => { fake.adapter = createFakeCallingAdapter(); });

  function renderCall(tokenResponse: unknown = { acsToken: 't', expiresAtUtc: '2026-09-14T07:00:00Z', groupCallId: 'g1' }) {
    mockApi({ 'GET /rooms': [ROOM], 'POST /rooms/r1/meetings/token': tokenResponse });
    return renderRoute(<VideoCallPage />, '/video-call/r1', '/video-call/:roomId');
  }

  it('TC-508: connecting → connected, with a per-tile spinner until the participant resolves', async () => {
    renderCall();
    await waitFor(() => expect(fake.adapter!.controller.calls).toContain('join:g1'));
    expect(screen.getByTestId('tile-self')).toHaveTextContent('Connecting');
    act(() => fake.adapter!.controller.setParticipants([{ id: 'p1', displayName: 'Anita', state: 'connecting', muted: false }]));
    expect(screen.getByTestId('tile-p1')).toHaveTextContent('Connecting Anita');
    act(() => { fake.adapter!.controller.setCallState('connected'); fake.adapter!.controller.setParticipants([{ id: 'p1', displayName: 'Anita', state: 'connected', muted: true }]); });
    expect(screen.getByText('LIVE')).toBeInTheDocument();
    expect(screen.getByTestId('tile-p1')).toHaveAttribute('data-state', 'connected');
    expect(screen.getByTestId('tile-p1')).toHaveTextContent('Muted:');
  });

  it('TC-509: a dropped participant keeps a reconnecting tile instead of vanishing', async () => {
    renderCall();
    await waitFor(() => expect(fake.adapter!.controller.calls).toContain('join:g1'));
    act(() => { fake.adapter!.controller.setCallState('connected'); fake.adapter!.controller.setParticipants([{ id: 'p1', displayName: 'Anita', state: 'connected', muted: false }]); });
    act(() => fake.adapter!.controller.setParticipants([]));
    expect(screen.getByTestId('tile-p1')).toHaveAttribute('data-state', 'reconnecting');
    expect(screen.getByTestId('tile-p1')).toHaveTextContent('Reconnecting…');
    act(() => fake.adapter!.controller.setCallState('reconnecting'));
    expect(screen.getByText(/Your connection dropped/)).toBeInTheDocument();
  });

  it('camera/mic permission denied shows the how-to-enable message on your own tile', async () => {
    renderCall();
    await waitFor(() => expect(fake.adapter!.controller.calls).toContain('join:g1'));
    act(() => fake.adapter!.controller.fail('permission-denied', 'Camera or microphone access is blocked.'));
    const tile = screen.getByTestId('tile-self');
    expect(tile).toHaveTextContent('Camera or microphone access is blocked.');
    expect(tile).toHaveTextContent(/allow camera and microphone/);
  });

  it('ACS not configured (503) shows a non-silent error that points back to chat', async () => {
    renderCall(() => new Response(JSON.stringify({ code: 'ERR-903', message: 'x' }), { status: 503 }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Meetings are unavailable right now. Chat is still available.');
    expect(screen.getByRole('link', { name: 'Go to chat' })).toHaveAttribute('href', '/chat/r1');
  });

  it('leaving the call hangs up and returns to the room list without confirmation', async () => {
    const { router } = renderCall();
    await waitFor(() => expect(fake.adapter!.controller.calls).toContain('join:g1'));
    fireEvent.click(screen.getByRole('button', { name: 'Leave Call' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/dashboard/meeting'));
    expect(fake.adapter!.controller.calls).toContain('leave');
  });

  it('mergeTiles marks only missing participants as reconnecting and keeps their first drop time', () => {
    const a = { id: 'a', displayName: 'A', state: 'connected' as const, muted: false };
    const b = { id: 'b', displayName: 'B', state: 'connected' as const, muted: false };
    const once = mergeTiles([a, b], [a], 1000);
    expect(once.find((t) => t.id === 'b')).toMatchObject({ state: 'reconnecting', droppedAt: 1000 });
    expect(mergeTiles(once, [a], 5000).find((t) => t.id === 'b')?.droppedAt).toBe(1000);
    expect(mergeTiles(once, [a, b], 6000).find((t) => t.id === 'b')).toMatchObject({ state: 'connected', droppedAt: undefined });
  });
});
