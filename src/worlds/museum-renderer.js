import { nearestInteractable } from '../game-core.js';
import { drawLabel, drawPlayer } from '../runtime/shared-render.js';

const WORLD_ACCENTS = {
  'ashes-door': 'rgba(83, 139, 56, 0.34)',
  'eris-door': 'rgba(82, 215, 208, 0.38)',
};

// Museum có renderer riêng: nhãn chỉ xuất hiện khi khách thật sự đến gần exhibit.
export function getMuseumExhibitPresentation(world, state) {
  const near = nearestInteractable(state);
  return [...(world.exits ?? []), ...(world.props ?? [])].map((item) => ({
    ...item,
    active: near?.id === item.id,
    showLabel: near?.id === item.id,
  }));
}

export function renderMuseum({ ctx, world, state, assets, moving, animationFrame }) {
  const exhibits = getMuseumExhibitPresentation(world, state);

  drawRoom(ctx, world);

  for (const exhibit of exhibits) {
    if (exhibit.action?.type === 'enter-world') drawDoor(ctx, exhibit);
    else drawArchivePlinth(ctx, exhibit);
  }

  drawPlayer(ctx, state, assets, moving, animationFrame);
}

function drawRoom(ctx, world) {
  ctx.fillStyle = '#f7f6f2';
  ctx.fillRect(0, 0, world.width, world.height);

  // Khối phòng chính: gần như trắng tuyệt đối, chỉ giữ đủ tương phản để đọc được chiều sâu.
  ctx.fillStyle = '#fbfaf7';
  ctx.fillRect(54, 76, world.width - 108, world.height - 112);

  ctx.fillStyle = 'rgba(39, 37, 32, 0.045)';
  ctx.fillRect(54, 76, world.width - 108, 12);
  ctx.fillRect(54, 88, 10, world.height - 124);
  ctx.fillRect(world.width - 64, 88, 10, world.height - 124);

  // Các mảng sàn lớn thay cho grid nhỏ: yên, sạch và giống gallery hơn.
  ctx.strokeStyle = 'rgba(59, 55, 48, 0.055)';
  ctx.lineWidth = 1;
  for (const y of [338, 418]) {
    ctx.beginPath();
    ctx.moveTo(76, y);
    ctx.lineTo(world.width - 76, y);
    ctx.stroke();
  }

  // Hai hốc triển lãm được tạo bằng bóng rất nhẹ thay vì panel UI.
  drawWallBay(ctx, 382, 222);
  drawWallBay(ctx, 578, 222);

  // Ánh sáng trần lớn tạo nhịp nhưng không biến thành texture.
  ctx.fillStyle = 'rgba(255, 255, 255, 0.7)';
  ctx.fillRect(118, 106, 250, 18);
  ctx.fillRect(592, 106, 250, 18);
}

function drawWallBay(ctx, x, y) {
  ctx.fillStyle = 'rgba(23, 22, 19, 0.035)';
  ctx.fillRect(x - 64, y - 118, 128, 144);
  ctx.fillStyle = '#fefdfa';
  ctx.fillRect(x - 60, y - 114, 120, 136);
}

function drawDoor(ctx, exhibit) {
  const { x, y, active } = exhibit;
  const accent = WORLD_ACCENTS[exhibit.id] ?? 'rgba(30, 30, 28, 0.2)';

  ctx.save();
  ctx.shadowColor = active ? 'rgba(26, 24, 20, 0.16)' : 'rgba(26, 24, 20, 0.08)';
  ctx.shadowBlur = active ? 18 : 10;
  ctx.shadowOffsetY = 7;
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(x - 33, y - 86, 66, 98);
  ctx.restore();

  ctx.strokeStyle = active ? '#2d2b27' : 'rgba(45, 43, 39, 0.22)';
  ctx.lineWidth = active ? 2 : 1;
  ctx.strokeRect(x - 33, y - 86, 66, 98);

  ctx.fillStyle = accent;
  ctx.fillRect(x - 31, y + 7, 62, active ? 3 : 2);

  ctx.fillStyle = active ? 'rgba(36, 34, 30, 0.14)' : 'rgba(36, 34, 30, 0.07)';
  ctx.fillRect(x - 25, y - 74, 50, 70);

  if (exhibit.showLabel) {
    drawLabel(ctx, exhibit.label, x, y - 105, '#2d2b27');
    drawSmallLabel(ctx, 'ENTER  E', x, y + 34, '#7b7871');
  }
}

function drawArchivePlinth(ctx, exhibit) {
  const { x, y, active } = exhibit;

  ctx.fillStyle = active ? '#e8e5de' : '#efede7';
  ctx.fillRect(x - 54, y - 18, 108, 28);
  ctx.fillStyle = '#f8f7f3';
  ctx.fillRect(x - 44, y - 46, 88, 28);
  ctx.strokeStyle = active ? 'rgba(42, 40, 36, 0.28)' : 'rgba(42, 40, 36, 0.11)';
  ctx.strokeRect(x - 44, y - 46, 88, 28);

  if (exhibit.showLabel) {
    drawLabel(ctx, exhibit.label, x, y - 66, '#46433d');
    drawSmallLabel(ctx, 'READ  E', x, y + 28, '#868179');
  }
}

function drawSmallLabel(ctx, text, x, y, color) {
  ctx.save();
  ctx.font = '600 8px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillStyle = color;
  ctx.fillText(text, x, y);
  ctx.restore();
}
