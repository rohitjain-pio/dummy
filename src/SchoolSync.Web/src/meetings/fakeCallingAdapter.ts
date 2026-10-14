import type { CallErrorKind, CallEvents, CallHandle, CallingAdapter, CallParticipant, CallState } from './callingAdapter';

// Q-234 fake ACS: drives UI-014's states deterministically from tests (TC-508/509).
// Tests control it through window.__fakeCall (e2e build mode only) or the returned controller (unit tests).
export interface FakeCallController {
  setCallState(state: CallState): void;
  setParticipants(participants: Omit<CallParticipant, 'attachVideo'>[]): void;
  fail(kind: CallErrorKind, message: string): void;
  readonly calls: string[];
}

declare global {
  interface Window { __fakeCall?: FakeCallController }
}

export function createFakeCallingAdapter(): CallingAdapter & { controller: FakeCallController } {
  let events: CallEvents | null = null;
  const calls: string[] = [];
  const controller: FakeCallController = {
    calls,
    setCallState: (s) => events?.onCallState(s),
    setParticipants: (p) => events?.onParticipants(p),
    fail: (k, m) => events?.onError(k, m),
  };
  if (typeof window !== 'undefined') window.__fakeCall = controller;

  return {
    controller,
    async join(options, e): Promise<CallHandle> {
      events = e;
      calls.push(`join:${options.groupCallId}`);
      e.onCallState('connecting');
      return {
        setMicrophone: async (on) => void calls.push(`mic:${on}`),
        setCamera: async (on) => void calls.push(`camera:${on}`),
        setScreenShare: async (on) => void calls.push(`screen:${on}`),
        leave: async () => void calls.push('leave'),
      };
    },
  };
}
