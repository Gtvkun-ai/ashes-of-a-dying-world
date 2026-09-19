import { drawGenericInteractables, drawPlayer } from '../runtime/shared-render.js';

export function renderEris({ ctx, world, state, assets, moving, animationFrame }) {
  ctx.fillStyle = '#101716';
  ctx.fillRect(0, 0, world.width, world.height);

  ctx.strokeStyle = 'rgba(82, 215, 208, 0.14)';
  ctx.lineWidth = 1;
  for (let x = 0; x < world.width; x += 32) {
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, world.height);
    ctx.stroke();
  }
  for (let y = 0; y < world.height; y += 32) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(world.width, y);
    ctx.stroke();
  }

  const nodes = world.props.filter((prop) => prop.id !== 'eris-terminal');
  ctx.strokeStyle = 'rgba(82, 215, 208, 0.45)';
  ctx.lineWidth = 2;
  for (const node of nodes) {
    ctx.beginPath();
    ctx.moveTo(320, 236);
    ctx.lineTo(node.x, node.y);
    ctx.stroke();
  }

  ctx.fillStyle = '#0b1110';
  ctx.strokeStyle = '#52d7d0';
  ctx.lineWidth = 2;
  ctx.strokeRect(266, 216, 108, 42);
  ctx.fillText('>_', 306, 242);

  drawGenericInteractables(ctx, world, state);
  drawPlayer(ctx, state, assets, moving, animationFrame);
}
