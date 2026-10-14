// CT-304 / T-006 / Q-234 — the ACS Calling SDK sits behind this adapter so UI-014's connecting /
// connected / reconnecting / error states can be exercised with a fake (TC-508/509) and the real SDK
// is only loaded when a call actually starts. The API never relays media (Tech Design §6).

export type ParticipantState = 'connecting' | 'connected' | 'reconnecting';
export type CallState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';
export type CallErrorKind = 'permission-denied' | 'unavailable' | 'failed';

export interface CallParticipant {
  id: string;
  displayName: string;
  state: ParticipantState;
  muted: boolean;
  /** attaches this participant's video to a container; returns a detach function */
  attachVideo?: (container: HTMLElement) => Promise<() => void>;
}

export interface CallEvents {
  onCallState: (state: CallState) => void;
  onParticipants: (participants: CallParticipant[]) => void;
  onError: (kind: CallErrorKind, message: string) => void;
}

export interface CallHandle {
  setMicrophone(on: boolean): Promise<void>;
  setCamera(on: boolean): Promise<void>;
  setScreenShare(on: boolean): Promise<void>;
  leave(): Promise<void>;
}

export interface JoinOptions { acsToken: string; groupCallId: string; displayName: string }

export interface CallingAdapter {
  join(options: JoinOptions, events: CallEvents): Promise<CallHandle>;
}

export async function createCallingAdapter(): Promise<CallingAdapter> {
  // The fake is compiled in only for the e2e build mode (Q-234) — never in production bundles.
  if (import.meta.env.MODE === 'e2e') return (await import('./fakeCallingAdapter')).createFakeCallingAdapter();
  return (await import('./acsCallingAdapter')).createAcsCallingAdapter();
}
