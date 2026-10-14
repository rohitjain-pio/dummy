import { create } from 'zustand';
import { api } from '@/lib/api';
import type { Role } from '@/lib/roles';
import type { SessionResponse } from '@/lib/types';
import { useSession } from './session';

export async function login(username: string, password: string) {
  const s = await api.post<SessionResponse>('/auth/login', { username, password }, { anonymous: true });
  useSession.getState().setSession(s.accessToken, s.user);
  return s.user;
}

export async function register(body: { username: string; email: string; password: string; role: Role; institutionId: string }) {
  const s = await api.post<SessionResponse>('/auth/register', body, { anonymous: true });
  useSession.getState().setSession(s.accessToken, s.user); // FR-006: immediate session, no separate login
  return s.user;
}

export async function logout() {
  try {
    await api.post('/auth/logout');
  } finally {
    useSession.getState().clear(); // the server also clears the refresh cookie
  }
}

// UI-005 is a modal mounted over any public route (no /login page) — its open state is global so
// the header CTA, pricing CTAs and a signed-out deep link can all open it.
type AuthMode = 'login' | 'register';
interface AuthModalState { open: boolean; mode: AuthMode; openWith: (mode: AuthMode) => void; setOpen: (open: boolean) => void; setMode: (mode: AuthMode) => void }
export const useAuthModal = create<AuthModalState>((set) => ({
  open: false,
  mode: 'login',
  openWith: (mode) => set({ open: true, mode }),
  setOpen: (open) => set({ open }),
  setMode: (mode) => set({ mode }),
}));
