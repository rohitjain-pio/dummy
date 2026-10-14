import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useSession } from '@/auth/session';
import { api, ApiError } from './api';

const user = { id: 'u1', username: 'jane', role: 'Teacher' as const, institutionId: 'i1' };
const json = (status: number, body?: unknown) => new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

describe('API client — in-memory access token + cookie refresh (Q-232)', () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    useSession.setState({ status: 'authenticated', accessToken: 'old', user });
  });
  afterEach(() => vi.unstubAllGlobals());

  it('on a 401 refreshes via the cookie once, then retries with the new token', async () => {
    fetchMock
      .mockResolvedValueOnce(json(401))
      .mockResolvedValueOnce(json(200, { accessToken: 'new', user }))
      .mockResolvedValueOnce(json(200, [{ id: 'c1' }]));

    const result = await api.get<{ id: string }[]>('/classes');

    expect(result).toEqual([{ id: 'c1' }]);
    const [refreshUrl, refreshInit] = fetchMock.mock.calls[1]!;
    expect(refreshUrl).toBe('/api/v1/auth/refresh');
    expect(refreshInit.body).toBeUndefined(); // the refresh token is never sent from script — only the cookie
    expect(refreshInit.credentials).toBe('same-origin');
    expect(fetchMock.mock.calls[2]![1].headers.Authorization).toBe('Bearer new');
  });

  it('concurrent 401s share a single refresh (the refresh token rotates on use)', async () => {
    fetchMock.mockImplementation(async (url: string, init: RequestInit) => {
      if (url === '/api/v1/auth/refresh') return json(200, { accessToken: 'new', user });
      return (init.headers as Record<string, string>).Authorization === 'Bearer new' ? json(200, []) : json(401);
    });
    await Promise.all([api.get('/a'), api.get('/b'), api.get('/c')]);
    expect(fetchMock.mock.calls.filter(([u]) => u === '/api/v1/auth/refresh')).toHaveLength(1);
  });

  it('a failed refresh signs the user out and surfaces the 401', async () => {
    fetchMock.mockResolvedValueOnce(json(401)).mockResolvedValueOnce(json(401, { code: 'ERR-204', message: 'Your session has expired. Please sign in again.' }));
    await expect(api.get('/classes')).rejects.toBeInstanceOf(ApiError);
    expect(useSession.getState().status).toBe('anonymous');
    expect(useSession.getState().accessToken).toBeNull();
  });

  it('maps catalogue errors and the timetable multi-conflict body', async () => {
    fetchMock.mockResolvedValueOnce(json(409, { errors: [{ code: 'ERR-701', message: 'Teacher busy.' }, { code: 'ERR-703', message: 'Room booked.' }] }));
    const err = (await api.post('/timetable', {}).catch((e: unknown) => e)) as ApiError;
    expect(err.status).toBe(409);
    expect(err.errors.map((e) => e.code)).toEqual(['ERR-701', 'ERR-703']);
  });

  it('never writes tokens to web storage', async () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    fetchMock.mockResolvedValueOnce(json(401)).mockResolvedValueOnce(json(200, { accessToken: 'new', user })).mockResolvedValueOnce(json(200, {}));
    await api.get('/x');
    expect(setItem).not.toHaveBeenCalled();
  });
});
