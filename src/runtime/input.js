const MOVING_KEYS = ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'w', 'a', 's', 'd'];

// Gom keyboard + touch vào một nguồn input duy nhất. Không giữ game state ở đây.
export function createInputController({
  target = window,
  moveButtons = [],
  interactButton = null,
  onInteract = () => {},
  canInteract = () => true,
  onEscape = () => {},
} = {}) {
  const keys = new Set();
  const cleanups = [];

  const normalizeKey = (event) => (
    event.key.length === 1 ? event.key.toLowerCase() : event.key
  );

  const onKeyDown = (event) => {
    const key = normalizeKey(event);
    if (MOVING_KEYS.includes(key)) {
      event.preventDefault();
      keys.add(key);
    }
    if ((key === 'e' || key === 'Enter') && !event.repeat && canInteract()) {
      event.preventDefault();
      onInteract();
    }
    if (key === 'Escape') {
      onEscape();
    }
  };

  const onKeyUp = (event) => keys.delete(normalizeKey(event));
  const onBlur = () => keys.clear();

  target.addEventListener('keydown', onKeyDown);
  target.addEventListener('keyup', onKeyUp);
  target.addEventListener('blur', onBlur);
  cleanups.push(() => target.removeEventListener('keydown', onKeyDown));
  cleanups.push(() => target.removeEventListener('keyup', onKeyUp));
  cleanups.push(() => target.removeEventListener('blur', onBlur));

  for (const button of moveButtons) {
    const moveKey = {
      up: 'ArrowUp',
      down: 'ArrowDown',
      left: 'ArrowLeft',
      right: 'ArrowRight',
    }[button.dataset.move];
    if (!moveKey) continue;

    const start = (event) => {
      event.preventDefault();
      keys.add(moveKey);
      button.setPointerCapture?.(event.pointerId);
    };
    const stop = (event) => {
      keys.delete(moveKey);
      if (button.hasPointerCapture?.(event.pointerId)) {
        button.releasePointerCapture(event.pointerId);
      }
    };

    button.addEventListener('pointerdown', start);
    button.addEventListener('pointerup', stop);
    button.addEventListener('pointercancel', stop);
    button.addEventListener('pointerleave', stop);
    cleanups.push(() => button.removeEventListener('pointerdown', start));
    cleanups.push(() => button.removeEventListener('pointerup', stop));
    cleanups.push(() => button.removeEventListener('pointercancel', stop));
    cleanups.push(() => button.removeEventListener('pointerleave', stop));
  }

  if (interactButton) {
    interactButton.addEventListener('click', onInteract);
    cleanups.push(() => interactButton.removeEventListener('click', onInteract));
  }

  return {
    getState() {
      return {
        left: keys.has('ArrowLeft') || keys.has('a'),
        right: keys.has('ArrowRight') || keys.has('d'),
        up: keys.has('ArrowUp') || keys.has('w'),
        down: keys.has('ArrowDown') || keys.has('s'),
      };
    },
    clear() {
      keys.clear();
    },
    destroy() {
      keys.clear();
      for (const cleanup of cleanups) cleanup();
    },
  };
}
