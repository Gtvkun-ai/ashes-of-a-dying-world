import {
  applyInteraction,
  createInitialState,
  currentWorld,
  movePlayer,
} from '../game-core.js';
import { computeViewport, drawLetterbox } from './view.js';

// Runtime chỉ điều phối state -> update -> render. Logic riêng của từng world nằm ngoài file này.
export function createGameRuntime({
  canvas,
  assets,
  renderers,
  initialWorld,
  getInputState = () => ({}),
  isPaused = () => false,
  onHudUpdate = () => {},
  onInteractionEvent = () => {},
}) {
  const ctx = canvas.getContext('2d');
  ctx.imageSmoothingEnabled = false;

  let state = createInitialState(initialWorld);
  let lastTime = performance.now();
  let frameTimer = 0;
  let animationFrame = 0;
  let frameRequest = null;

  function tick(now) {
    const dt = Math.min((now - lastTime) / 1000, 0.05);
    lastTime = now;
    const input = getInputState();
    const moving = Object.values(input).some(Boolean);

    if (!isPaused()) {
      if (moving) {
        state = movePlayer(state, input, dt);
        frameTimer += dt;
        if (frameTimer > 0.22) {
          frameTimer = 0;
          animationFrame = (animationFrame + 1) % 6;
        }
      } else {
        state = { ...state, elapsed: state.elapsed + Math.max(0, dt) };
        animationFrame = 0;
      }
    }

    draw(moving);
    frameRequest = requestAnimationFrame(tick);
  }

  function draw(moving = false) {
    const world = currentWorld(state);
    const view = computeViewport(canvas, world, state);
    const renderWorld = renderers[world.id];

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.save();
    ctx.translate(view.x, view.y);
    ctx.scale(view.scale, view.scale);

    if (renderWorld) {
      renderWorld({ ctx, world, state, assets, moving, animationFrame });
    }

    ctx.restore();
    drawLetterbox(ctx, canvas, view);
    onHudUpdate(state, world);
  }

  function interact() {
    const result = applyInteraction(state);
    state = result.state;
    for (const event of result.events) onInteractionEvent(event, state);
    onHudUpdate(state, currentWorld(state));
    return result;
  }

  return {
    start() {
      if (frameRequest === null) frameRequest = requestAnimationFrame(tick);
    },
    stop() {
      if (frameRequest !== null) cancelAnimationFrame(frameRequest);
      frameRequest = null;
    },
    draw,
    interact,
    getState() {
      return state;
    },
  };
}
