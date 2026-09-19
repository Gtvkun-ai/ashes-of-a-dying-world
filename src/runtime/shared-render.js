import { nearestInteractable } from '../game-core.js';

export function drawGenericInteractables(ctx, world, state) {
  const near = nearestInteractable(state);

  for (const exit of world.exits ?? []) {
    const active = near?.id === exit.id;
    ctx.fillStyle = world.id === 'museum' ? '#fefefe' : 'rgba(255,255,255,0.9)';
    ctx.strokeStyle = active ? '#111412' : '#bdb7aa';
    ctx.lineWidth = active ? 3 : 2;
    ctx.fillRect(exit.x - 21, exit.y - 40, 42, 54);
    ctx.strokeRect(exit.x - 21, exit.y - 40, 42, 54);
    drawLabel(ctx, exit.label, exit.x, exit.y - 52, active ? '#111412' : '#79746a');
  }

  for (const prop of world.props ?? []) {
    const active = near?.id === prop.id;
    if (world.id === 'eris') {
      drawNode(ctx, prop.x, prop.y, prop.label, active);
    } else if (!prop.id.includes('table')) {
      drawLabel(ctx, prop.label, prop.x, prop.y - 28, active ? '#111412' : '#79746a');
    }
  }
}

export function drawPlayer(ctx, state, assets, moving, animationFrame) {
  const { sheet, row, flipX } = resolveDirectionSprite(state.player.facing, assets);
  const height = state.worldId === 'ashes' ? 38 : 42;
  const width = height * 0.55;
  const destY = state.player.y - height + 8 + (moving ? Math.sin(state.elapsed * 22) : 0);

  // Sprite diagonal trong asset gốc có thứ tự frame đảo chiều.
  const isDiag = state.player.facing.includes('_');
  const frameOrder = isDiag && !flipX ? [3, 2, 1, 0] : [0, 1, 2, 3];
  const srcFrame = frameOrder[animationFrame % 4];
  const sourceX = srcFrame * 16;
  const sourceY = row * 32;

  ctx.save();
  if (state.worldId === 'museum') {
    ctx.filter = 'grayscale(1) brightness(1.55) contrast(0.84)';
    ctx.globalAlpha = 0.86;
  }

  if (flipX) {
    ctx.translate(state.player.x, 0);
    ctx.scale(-1, 1);
    ctx.drawImage(sheet, sourceX, sourceY, 16, 32, -width / 2, destY, width, height);
  } else {
    ctx.drawImage(sheet, sourceX, sourceY, 16, 32, state.player.x - width / 2, destY, width, height);
  }
  ctx.restore();

  ctx.fillStyle = 'rgba(0, 0, 0, 0.18)';
  ctx.beginPath();
  ctx.ellipse(state.player.x, state.player.y + 5, height * 0.22, height * 0.08, 0, 0, Math.PI * 2);
  ctx.fill();
}

export function drawNode(ctx, x, y, label, active) {
  ctx.fillStyle = active ? 'rgba(82, 215, 208, 0.2)' : 'rgba(82, 215, 208, 0.08)';
  ctx.strokeStyle = active ? '#e9fffc' : '#52d7d0';
  ctx.lineWidth = active ? 3 : 2;
  ctx.beginPath();
  ctx.arc(x, y, 22, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
  drawLabel(ctx, label, x, y - 32, active ? '#e9fffc' : '#8de7e2');
}

export function drawDiamond(ctx, x, y, radius, fill, stroke) {
  ctx.fillStyle = fill;
  ctx.strokeStyle = stroke;
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(x, y - radius);
  ctx.lineTo(x + radius, y);
  ctx.lineTo(x, y + radius);
  ctx.lineTo(x - radius, y);
  ctx.closePath();
  ctx.fill();
  ctx.stroke();
}

export function drawLabel(ctx, text, x, y, color) {
  ctx.save();
  ctx.font = '700 10px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillStyle = color;
  ctx.fillText(text, x, y);
  ctx.restore();
}

function resolveDirectionSprite(facing, assets) {
  switch (facing) {
    case 'down': return { sheet: assets.hyou, row: 0, flipX: false };
    case 'left': return { sheet: assets.hyou, row: 1, flipX: false };
    case 'right': return { sheet: assets.hyou, row: 1, flipX: true };
    case 'up': return { sheet: assets.hyou, row: 3, flipX: false };
    case 'down_left': return { sheet: assets.hyouDiag, row: 0, flipX: false };
    case 'up_left': return { sheet: assets.hyouDiag, row: 1, flipX: false };
    case 'up_right': return { sheet: assets.hyouDiag, row: 1, flipX: true };
    case 'down_right': return { sheet: assets.hyouDiag, row: 0, flipX: true };
    default: return { sheet: assets.hyou, row: 0, flipX: false };
  }
}
