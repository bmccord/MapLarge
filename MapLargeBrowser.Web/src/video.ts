import { el, iconBtn } from './dom';

const VIDEO_URL = '/walkthrough.mp4';

export function startVideoPlayer(): void {
  const trigger = document.getElementById('open-walkthrough');
  if (!trigger) return;

  trigger.addEventListener('click', openVideoDialog);
}

function openVideoDialog(): void {
  if (document.querySelector('.video-dialog-overlay')) return;

  const overlay = el('div', 'video-dialog-overlay');
  const dialog = el('div', 'video-dialog');
  const closeBtn = iconBtn('video-dialog-close', 'x', 'Close', close);

  const video = el('video', 'video-dialog-player');
  video.controls = true;
  video.autoplay = true;
  video.src = VIDEO_URL;

  dialog.append(closeBtn, video);
  overlay.append(dialog);
  document.body.append(overlay);
  document.body.style.overflow = 'hidden';

  document.addEventListener('keydown', onKey);
  overlay.addEventListener('click', event => {
    if (event.target === overlay) close();
  });

  function close(): void {
    document.removeEventListener('keydown', onKey);
    video.pause();
    overlay.remove();
    document.body.style.overflow = '';
  }

  function onKey(event: KeyboardEvent): void {
    if (event.key === 'Escape' && !event.defaultPrevented) {
      event.preventDefault();
      close();
    }
  }
}
