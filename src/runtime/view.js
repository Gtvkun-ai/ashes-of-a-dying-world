export function computeViewport(canvas, world, state) {
  const scale = world.camera === 'follow'
    ? (world.viewScale ?? 1)
    : Math.min(canvas.width / world.width, canvas.height / world.height);

  if (world.camera !== 'follow') {
    return {
      scale,
      x: (canvas.width - world.width * scale) / 2,
      y: (canvas.height - world.height * scale) / 2,
    };
  }

  const scaledWidth = world.width * scale;
  const scaledHeight = world.height * scale;
  const focusX = state.player.x * scale;
  const focusY = state.player.y * scale;
  return {
    scale,
    x: clamp(canvas.width / 2 - focusX, canvas.width - scaledWidth, 0),
    y: clamp(canvas.height / 2 - focusY, canvas.height - scaledHeight, 0),
  };
}

export function drawLetterbox(ctx, canvas, view) {
  ctx.save();
  ctx.fillStyle = '#f3f1ec';
  if (view.x > 0) {
    ctx.fillRect(0, 0, view.x, canvas.height);
    ctx.fillRect(canvas.width - view.x, 0, view.x, canvas.height);
  }
  if (view.y > 0) {
    ctx.fillRect(0, 0, canvas.width, view.y);
    ctx.fillRect(0, canvas.height - view.y, canvas.width, view.y);
  }
  ctx.restore();
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}
