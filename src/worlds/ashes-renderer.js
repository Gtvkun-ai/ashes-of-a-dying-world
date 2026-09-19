import { ASHES_FIELD, nearestInteractable } from '../game-core.js';
import { drawDiamond, drawLabel, drawPlayer } from '../runtime/shared-render.js';

// Props tĩnh nên sort một lần thay vì copy + sort mỗi frame.
const SORTED_PROPS = [...ASHES_FIELD.props]
  .sort((a, b) => (a.y - b.y) || a.id.localeCompare(b.id));

export function renderAshes({ ctx, world, state, assets, moving, animationFrame }) {
  ctx.fillStyle = '#4f8b35';
  ctx.fillRect(0, 0, world.width, world.height);
  ctx.drawImage(assets.field, 0, 0, ASHES_FIELD.width, ASHES_FIELD.height);
  drawGroundLayers(ctx, state, assets);
  drawCloudShadow(ctx, world, state);

  const splitY = state.player.y + 8;
  for (const prop of SORTED_PROPS) {
    if (prop.y <= splitY) drawAshesProp(ctx, prop, state, assets);
  }

  const dynamicInteractables = state.worlds[world.id]?.interactables ?? [];
  for (const item of dynamicInteractables) {
    drawDiamond(ctx, item.x, item.y - 10, 7, '#bfeaff', '#436c8c');
  }

  drawAshesInteractables(ctx, world, state);
  drawPlayer(ctx, state, assets, moving, animationFrame);

  for (const prop of SORTED_PROPS) {
    if (prop.y > splitY) drawAshesProp(ctx, prop, state, assets);
  }

  drawColorGrade(ctx, world, state);
}

function drawGroundLayers(ctx, state, assets) {
  for (const layer of ASHES_FIELD.layers) {
    if (!layer.asset || layer.asset === 'field') continue;
    const image = assets[layer.asset];
    if (!image) continue;

    ctx.save();
    ctx.globalAlpha = layer.alpha ?? 1;
    if (layer.effect === 'grass-detail') {
      ctx.globalCompositeOperation = 'soft-light';
      ctx.filter = `saturate(1.18) contrast(1.04) brightness(${1 + Math.sin(state.elapsed * 0.85) * 0.015})`;
    }
    ctx.drawImage(image, 0, 0, ASHES_FIELD.width, ASHES_FIELD.height);
    ctx.restore();
  }

  ctx.save();
  ctx.globalCompositeOperation = 'multiply';
  ctx.globalAlpha = 0.16;
  const shade = ctx.createLinearGradient(0, 0, ASHES_FIELD.width, ASHES_FIELD.height);
  shade.addColorStop(0, '#324326');
  shade.addColorStop(0.42, 'rgba(75, 86, 50, 0)');
  shade.addColorStop(1, '#1f2e22');
  ctx.fillStyle = shade;
  ctx.fillRect(0, 0, ASHES_FIELD.width, ASHES_FIELD.height);
  ctx.restore();
}

function drawCloudShadow(ctx, world, state) {
  const t = state.elapsed * 16;
  ctx.save();
  ctx.globalCompositeOperation = 'multiply';
  ctx.globalAlpha = 0.13;
  ctx.fillStyle = '#50624b';
  for (let i = -2; i < 7; i += 1) {
    const x = ((i * 420 + t) % (world.width + 520)) - 260;
    const y = 120 + i * 230;
    ctx.beginPath();
    ctx.ellipse(x, y, 260, 48, -0.22, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}

function drawAshesProp(ctx, prop, state, assets) {
  if (prop.kind === 'tree') {
    drawLayeredTree(ctx, prop, state, assets.treeTrunk, assets.treeCanopy, 144, 176);
  } else if (prop.kind === 'appleTree') {
    drawLayeredTree(ctx, prop, state, assets.appleTreeTrunk, assets.appleTreeCanopy, 176, 200);
  } else if (prop.kind === 'grass') {
    drawGrass(ctx, prop, state, assets);
  } else if (prop.kind === 'grassPatch') {
    drawGrassPatch(ctx, prop, state, assets);
  }
}

function drawLayeredTree(ctx, prop, state, trunk, canopy, width, height) {
  const sway = Math.sin(state.elapsed * 1.45 + prop.phase) * 1.35;

  ctx.save();
  ctx.globalAlpha = 0.2;
  ctx.fillStyle = '#0f1c12';
  ctx.beginPath();
  ctx.ellipse(prop.x + 10, prop.y + 50, width * 0.28, 14, -0.18, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();

  ctx.drawImage(trunk, prop.x - width / 2, prop.y - 13 - height / 2, width, height);

  ctx.save();
  ctx.filter = 'saturate(1.08) contrast(1.03)';
  ctx.drawImage(canopy, prop.x - width / 2 + sway, prop.y - 13 - height / 2, width, height);
  ctx.restore();
}

function drawGrass(ctx, prop, state, assets) {
  const frame = Math.floor((state.elapsed * 2.4 + prop.phase) % 4);
  const order = [0, 1, 2, 1];
  const sx = order[frame] * 16;
  const sy = 51;
  const sway = Math.sin(state.elapsed * 2.8 + prop.phase) * 1.2;

  drawFloraShadow(ctx, prop.x, prop.y + 31, 18, 5);
  ctx.drawImage(assets.floraSmall, sx, sy, 16, 17, prop.x - 10 + sway, prop.y + 4, 20, 21);
}

function drawGrassPatch(ctx, prop, state, assets) {
  const frame = Math.floor((state.elapsed * 1.8 + prop.phase) % 4);
  const order = [0, 1, 2, 1];
  const sx = order[frame] * 52;
  const sway = Math.sin(state.elapsed * 1.9 + prop.phase) * 1;

  drawFloraShadow(ctx, prop.x, prop.y + 26, 42, 7);
  ctx.drawImage(assets.grassPatch, sx, 0, 52, 34, prop.x - 26 + sway, prop.y - 4, 52, 34);
}

function drawFloraShadow(ctx, x, y, rx, ry) {
  ctx.save();
  ctx.globalAlpha = 0.16;
  ctx.fillStyle = '#15210f';
  ctx.beginPath();
  ctx.ellipse(x, y, rx, ry, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();
}

function drawAshesInteractables(ctx, world, state) {
  const near = nearestInteractable(state);
  for (const exit of world.exits ?? []) {
    const active = near?.id === exit.id;
    ctx.save();
    ctx.globalAlpha = active ? 0.95 : 0.68;
    ctx.strokeStyle = active ? '#f8f2dc' : '#d8c69f';
    ctx.fillStyle = active ? 'rgba(248, 242, 220, 0.18)' : 'rgba(40, 34, 22, 0.18)';
    ctx.lineWidth = active ? 4 : 2;
    ctx.beginPath();
    ctx.roundRect(exit.x - 34, exit.y - 74, 68, 76, 6);
    ctx.fill();
    ctx.stroke();
    ctx.restore();
    drawLabel(ctx, exit.label, exit.x, exit.y - 86, active ? '#fff8df' : '#e5d5a7');
  }

  for (const prop of world.props ?? []) {
    const active = near?.id === prop.id;
    ctx.fillStyle = active ? '#e5d5a7' : '#6c4a2f';
    ctx.fillRect(prop.x - 32, prop.y - 24, 64, 32);
    ctx.fillStyle = '#d8c69f';
    ctx.fillRect(prop.x - 25, prop.y - 18, 50, 20);
    drawLabel(ctx, prop.label, prop.x, prop.y - 36, active ? '#fff8df' : '#f8f2dc');
  }
}

function drawColorGrade(ctx, world, state) {
  ctx.save();
  ctx.globalCompositeOperation = 'overlay';
  ctx.globalAlpha = 0.11;
  const warm = ctx.createLinearGradient(0, 0, world.width, world.height);
  warm.addColorStop(0, '#ffd58d');
  warm.addColorStop(0.55, 'rgba(255,255,255,0)');
  warm.addColorStop(1, '#5c89ad');
  ctx.fillStyle = warm;
  ctx.fillRect(0, 0, world.width, world.height);
  ctx.restore();

  ctx.save();
  ctx.globalCompositeOperation = 'multiply';
  ctx.globalAlpha = 0.12;
  const vignette = ctx.createRadialGradient(
    state.player.x,
    state.player.y,
    150,
    state.player.x,
    state.player.y,
    760,
  );
  vignette.addColorStop(0, 'rgba(255,255,255,0)');
  vignette.addColorStop(1, '#1a2419');
  ctx.fillStyle = vignette;
  ctx.fillRect(0, 0, world.width, world.height);
  ctx.restore();
}
