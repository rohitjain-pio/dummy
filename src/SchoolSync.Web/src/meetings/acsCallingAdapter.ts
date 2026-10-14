import type { CallAgent, CallClient, RemoteParticipant } from '@azure/communication-calling';
import type { CallEvents, CallHandle, CallingAdapter, CallParticipant, CallState, JoinOptions, ParticipantState } from './callingAdapter';

// Real Azure Communication Services adapter (T-006). Tech Design §13.1 carries an open ACS
// reconnection risk, so this maps every non-terminal drop to a visible "reconnecting" state rather
// than assuming the media session silently self-heals (UI-014).

function participantState(p: RemoteParticipant): ParticipantState | null {
  switch (p.state) {
    case 'Connected':
    case 'Hold':
      return 'connected';
    case 'Disconnected':
      return null;
    case 'Idle':
    case 'Connecting':
    case 'Ringing':
    case 'EarlyMedia':
    case 'InLobby':
    default:
      return 'connecting';
  }
}

export async function createAcsCallingAdapter(): Promise<CallingAdapter> {
  const calling = await import('@azure/communication-calling');
  const common = await import('@azure/communication-common');

  return {
    async join({ acsToken, groupCallId, displayName }: JoinOptions, events: CallEvents): Promise<CallHandle> {
      events.onCallState('connecting');
      const client: CallClient = new calling.CallClient();
      let agent: CallAgent;
      try {
        agent = await client.createCallAgent(new common.AzureCommunicationTokenCredential(acsToken), { displayName });
      } catch {
        events.onError('unavailable', 'Meetings are unavailable right now. Please try again shortly.');
        throw new Error('ACS agent creation failed');
      }

      const devices = await client.getDeviceManager();
      const access = await devices.askDevicePermission({ audio: true, video: true }).catch(() => ({ audio: false, video: false }));
      if (!access.audio || !access.video) {
        events.onError('permission-denied', 'Camera or microphone access is blocked.');
      }

      const cameras = access.video ? await devices.getCameras() : [];
      let localVideo = cameras[0] ? new calling.LocalVideoStream(cameras[0]) : undefined;
      const call = agent.join({ groupId: groupCallId }, {
        audioOptions: { muted: !access.audio },
        videoOptions: localVideo ? { localVideoStreams: [localVideo] } : undefined,
      });

      let wasConnected = false;
      const emitState = () => {
        let state: CallState;
        switch (call.state) {
          case 'Connected':
          case 'LocalHold':
          case 'RemoteHold':
            state = 'connected';
            wasConnected = true;
            break;
          case 'Disconnected':
          case 'Disconnecting':
            state = 'disconnected';
            break;
          default:
            state = wasConnected ? 'reconnecting' : 'connecting';
        }
        events.onCallState(state);
      };

      const toParticipant = (p: RemoteParticipant): CallParticipant | null => {
        const state = participantState(p);
        if (!state) return null;
        const id = 'communicationUserId' in p.identifier ? p.identifier.communicationUserId : JSON.stringify(p.identifier);
        return {
          id,
          displayName: p.displayName || 'Participant',
          state,
          muted: p.isMuted,
          attachVideo: async (container) => {
            const stream = p.videoStreams.find((s) => s.isAvailable);
            if (!stream) return () => undefined;
            const renderer = new calling.VideoStreamRenderer(stream);
            const view = await renderer.createView();
            container.appendChild(view.target);
            return () => renderer.dispose();
          },
        };
      };
      const emitParticipants = () => events.onParticipants(call.remoteParticipants.map(toParticipant).filter((p): p is CallParticipant => p !== null));
      const watch = (p: RemoteParticipant) => {
        p.on('stateChanged', emitParticipants);
        p.on('isMutedChanged', emitParticipants);
        p.on('videoStreamsUpdated', emitParticipants);
      };

      call.on('stateChanged', emitState);
      call.on('remoteParticipantsUpdated', (e) => { e.added.forEach(watch); emitParticipants(); });
      call.remoteParticipants.forEach(watch);
      emitState();
      emitParticipants();

      return {
        setMicrophone: (on) => (on ? call.unmute() : call.mute()),
        async setCamera(on) {
          if (on) {
            const cam = (await devices.getCameras())[0];
            if (!cam) { events.onError('permission-denied', 'No camera is available.'); return; }
            localVideo = new calling.LocalVideoStream(cam);
            await call.startVideo(localVideo);
          } else if (localVideo) {
            await call.stopVideo(localVideo);
          }
        },
        setScreenShare: (on) => (on ? call.startScreenSharing() : call.stopScreenSharing()),
        async leave() {
          await call.hangUp().catch(() => undefined);
          await agent.dispose().catch(() => undefined);
        },
      };
    },
  };
}
