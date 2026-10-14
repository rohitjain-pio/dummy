import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactNode } from 'react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { vi } from 'vitest';
import { useSession } from '@/auth/session';
import { ToastHost } from '@/components/ui/toast';
import type { Role } from '@/lib/roles';

export const USER_ID = '11111111-1111-1111-1111-111111111111';

export function signIn(role: Role) {
  useSession.setState({ status: 'authenticated', accessToken: 'token', user: { id: USER_ID, username: `test.${role.toLowerCase()}`, role, institutionId: 'inst' } });
}

type Handler = (url: string, init: RequestInit) => unknown;
/** Route table keyed "METHOD /path" (path without /api/v1, query ignored unless present in the key). */
export function mockApi(routes: Record<string, unknown | Handler>) {
  const calls: { method: string; path: string; body: unknown }[] = [];
  const fetchMock = vi.fn(async (input: string, init: RequestInit = {}) => {
    const method = init.method ?? 'GET';
    const path = input.replace('/api/v1', '');
    const body = typeof init.body === 'string' ? JSON.parse(init.body) : undefined;
    calls.push({ method, path, body });
    const key = Object.keys(routes).find((k) => k === `${method} ${path}`) ?? Object.keys(routes).find((k) => k === `${method} ${path.split('?')[0]}`);
    if (!key) return new Response(null, { status: 404 });
    const value = routes[key];
    const result = typeof value === 'function' ? (value as Handler)(path, init) : value;
    if (result instanceof Response) return result;
    return new Response(JSON.stringify(result), { status: method === 'POST' ? 201 : 200, headers: { 'Content-Type': 'application/json' } });
  });
  vi.stubGlobal('fetch', fetchMock);
  return calls;
}

export function renderRoute(element: ReactNode, path = '/', routePath = path) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter([{ path: routePath, element: <>{element}<ToastHost /></> }, { path: '*', element: <p>navigated</p> }], { initialEntries: [path] });
  return { ...render(<QueryClientProvider client={client}><RouterProvider router={router} /></QueryClientProvider>), router };
}
