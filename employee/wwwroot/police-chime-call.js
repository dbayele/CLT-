import {
  ConsoleLogger,
  DefaultDeviceController,
  DefaultMeetingSession,
  LogLevel,
  MeetingSessionConfiguration
} from 'https://esm.sh/amazon-chime-sdk-js@3.27.0';

const cfg = window.CLT_CHIME_CALL;
const status = document.getElementById('chime-status');
const joinButton = document.getElementById('join-call');
const leaveButton = document.getElementById('leave-call');
const remoteAudio = document.getElementById('remote-audio');
let session;

function meetingResponse(c) {
  return {
    Meeting: {
      MeetingId: c.meetingId,
      MediaRegion: c.mediaRegion,
      MediaPlacement: c.mediaPlacement || window.CLT_CHIME_MEDIA_PLACEMENT
    }
  };
}
function attendeeResponse(c) {
  return { Attendee: { AttendeeId: c.attendeeId, JoinToken: c.joinToken } };
}

joinButton?.addEventListener('click', async () => {
  try {
    status.textContent = 'Requesting microphone access…';
    const logger = new ConsoleLogger('CLTPPPoliceChime', LogLevel.WARN);
    const devices = new DefaultDeviceController(logger);
    const config = new MeetingSessionConfiguration(meetingResponse(cfg), attendeeResponse(cfg));
    session = new DefaultMeetingSession(config, logger, devices);
    const inputs = await session.audioVideo.listAudioInputDevices();
    if (!inputs.length) throw new Error('No microphone is available.');
    await session.audioVideo.startAudioInput(inputs[0].deviceId);
    session.audioVideo.bindAudioElement(remoteAudio);
    session.audioVideo.start();
    joinButton.disabled = true;
    leaveButton.disabled = false;
    status.textContent = 'Microphone connected. The phone participant is bridged through Amazon Chime.';
  } catch (e) {
    console.error(e);
    status.textContent = `Unable to join the Chime call: ${e.message || e}`;
  }
});

leaveButton?.addEventListener('click', async () => {
  if (!session) return;
  try { session.audioVideo.stop(); } finally {
    session = undefined;
    joinButton.disabled = false;
    leaveButton.disabled = true;
    status.textContent = 'Disconnected from the Chime call.';
  }
});
