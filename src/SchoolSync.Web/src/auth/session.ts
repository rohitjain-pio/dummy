import { create } from 'zustand';
import type { SessionUser } from '@/lib/types';

// NFR-017 / Q-232: the access token is held in memory only — no persist middleware, no web storage.
// A reload restores the session through the httpOnly refresh cookie (refreshSession in lib/api).
export type SessionStatus = 'unknown' | 'authenticated' | 'anonymous';

interface SessionState {
  status: SessionStatus;
  accessToken: string | null;
  user: SessionUser | null;
  setSession: (accessToken: string, user: SessionUser) => void;
  clear: () => void;
}

export const useSession = create<SessionState>((set) => ({
  status: 'unknown',
  accessToken: null,
  user: null,
  setSession: (accessToken, user) => set({ status: 'authenticated', accessToken, user }),
  clear: () => set({ status: 'anonymous', accessToken: null, user: null }),
}));
