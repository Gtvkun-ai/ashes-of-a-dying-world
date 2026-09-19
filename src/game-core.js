const PLAYER_SPEED = 148;
const ASHES_FIELD_SOURCE_WIDTH = 456;
const ASHES_FIELD_SOURCE_HEIGHT = 474;
const ASHES_FIELD_SCALE = 4;

const treePoints = [
  [14, 30], [143, -1], [306, -1], [475, -1], [646, -1], [818, -1], [989, -1], [1170, -20],
  [1341, -20], [1493, -21], [1644, -20], [1816, -20], [104, 40], [269, 40], [431, 40],
  [605, 40], [778, 42], [948, 40], [1121, -26], [1259, 30], [1410, 31], [1583, 34],
  [1734, 35], [33, 1839], [204, 1839], [422, 1839], [732, 1841], [903, 1841], [1121, 1841],
  [1327, 1838], [1565, 1883], [1783, 1883], [70, 1890], [307, 1891], [542, 1891],
  [785, 1890], [1022, 1891], [1241, 1839], [1480, 1882], [1717, 1883],
];

const appleTreePoints = [
  [1023, 403], [481, 591], [1566, 147], [506, 741], [1060, 1475], [1186, 1555],
  [1633, 1145], [1572, 1671], [1333, 575], [1517, 1642], [923, 1595], [433, 1188],
  [423, 712], [234, 256], [1026, 978], [1409, 798], [564, 698],
];

const grassPoints = [
  [439, 992], [645, 1628], [255, 1008], [252, 1225], [189, 1041], [1762, 513],
  [1710, 54], [1551, 1530], [1494, 1320], [681, 1723], [139, 1600], [345, 1614],
  [529, 1639], [1572, 1148], [1552, 1270], [1266, 1363], [1384, 1245], [1227, 1496],
  [1271, 1535], [1439, 1511], [1674, 1266], [583, 1572], [1369, 577], [1254, 517],
  [1425, 566], [1622, 700], [207, 860], [1166, 216], [823, 49], [1465, 664],
  [116, 1244], [1117, 394], [1669, 1526], [161, 680], [396, 873], [51, 580],
  [208, 717], [211, 708],
];

const grassPatchPoints = [
  [689, 817], [387, 419], [1292, 203], [1515, 123], [1495, 294], [1228, 339],
  [631, 533], [644, 72], [486, 670], [486, 1068], [486, 1289], [899, 1151],
  [957, 1352], [1261, 1208], [1493, 1107], [1024, 1130], [842, 1461], [968, 1518],
  [420, 492], [359, 359], [546, 351], [693, 142], [135, 397], [182, 1297],
];

export const ASHES_FIELD = {
  sourceWidth: ASHES_FIELD_SOURCE_WIDTH,
  sourceHeight: ASHES_FIELD_SOURCE_HEIGHT,
  assetScale: ASHES_FIELD_SCALE,
  width: ASHES_FIELD_SOURCE_WIDTH * ASHES_FIELD_SCALE,
  height: ASHES_FIELD_SOURCE_HEIGHT * ASHES_FIELD_SCALE,
  layers: [
    { id: 'base', asset: 'field', effect: 'albedo' },
    { id: 'grass-field-detail', asset: 'grassFieldDetail', effect: 'grass-detail', alpha: 0.6 },
    { id: 'grass-edge-detail', asset: 'grassEdgeDetail', effect: 'grass-detail', alpha: 0.72 },
    { id: 'cloud-shadow', effect: 'cloud-shadow', alpha: 0.18 },
  ],
  props: [
    ...treePoints.map(([x, y], index) => ({ id: `tree-${index + 1}`, kind: 'tree', x, y, phase: index * 0.37 })),
    ...appleTreePoints.map(([x, y], index) => ({ id: `apple-tree-${index + 1}`, kind: 'appleTree', x, y, phase: 1.7 + index * 0.43 })),
    ...grassPoints.map(([x, y], index) => ({ id: `grass-${index + 1}`, kind: 'grass', x, y, phase: index * 0.53 })),
    ...grassPatchPoints.map(([x, y], index) => ({ id: `grass-patch-${index + 1}`, kind: 'grassPatch', x, y, phase: index * 0.41 })),
  ],
};

export const WORLD_DEFS = {
  museum: {
    id: 'museum',
    label: 'White Museum',
    width: 960,
    height: 540,
    bounds: { left: 64, top: 88, right: 896, bottom: 500 },
    spawn: { x: 480, y: 448, facing: 'up' },
    exits: [
      {
        id: 'ashes-door',
        label: 'ASHES OF A DYING WORLD',
        x: 382,
        y: 232,
        radius: 54,
        action: {
          type: 'enter-world',
          targetWorld: 'ashes',
          targetSpawn: { x: 336, y: 1320, facing: 'down' },
        },
      },
      {
        id: 'eris-door',
        label: 'ERIS',
        x: 578,
        y: 232,
        radius: 54,
        action: {
          type: 'enter-world',
          targetWorld: 'eris',
          targetSpawn: { x: 92, y: 278, facing: 'right' },
        },
      },
    ],
    props: [
      {
        id: 'archive-table',
        label: 'ARCHIVE / CURRENT WORK',
        x: 190,
        y: 344,
        radius: 48,
        action: {
          type: 'notice',
          message: 'Two rooms are open. Ashes is the playable world; Eris is the living archive.',
        },
      },
    ],
  },
  ashes: {
    id: 'ashes',
    label: 'Ashes Field 01',
    width: ASHES_FIELD.width,
    height: ASHES_FIELD.height,
    camera: 'follow',
    viewScale: 0.8,
    bounds: { left: 112, top: 216, right: 1712, bottom: 1720 },
    spawn: { x: 336, y: 1320, facing: 'down' },
    exits: [
      {
        id: 'museum-return',
        label: 'RETURN',
        x: 256,
        y: 1368,
        radius: 72,
        action: {
          type: 'enter-world',
          targetWorld: 'museum',
          targetSpawn: { x: 382, y: 314, facing: 'down' },
        },
      },
    ],
    initialState: {
      interactables: [
        {
          id: 'frost-shard',
          label: 'Frost shard',
          x: 840,
          y: 1168,
          radius: 56,
          action: { type: 'pickup' },
        },
      ],
    },
    props: [
      {
        id: 'ashes-update-table',
        label: 'Field marker',
        x: 1320,
        y: 904,
        radius: 64,
        action: {
          type: 'notice',
          message: 'Ashes field slice: real map scale, shader pass, trees and grass. Combat is intentionally off.',
        },
      },
    ],
  },
  eris: {
    id: 'eris',
    label: 'Eris Child World',
    width: 640,
    height: 360,
    bounds: { left: 32, top: 32, right: 608, bottom: 328 },
    spawn: { x: 92, y: 278, facing: 'right' },
    exits: [
      {
        id: 'museum-return',
        label: 'RETURN',
        x: 78,
        y: 292,
        radius: 38,
        action: {
          type: 'enter-world',
          targetWorld: 'museum',
          targetSpawn: { x: 578, y: 314, facing: 'down' },
        },
      },
    ],
    props: [
      {
        id: 'eris-terminal',
        label: 'Talk to Eris',
        x: 320,
        y: 236,
        radius: 40,
        action: {
          type: 'emit',
          eventType: 'eris-terminal',
          message: 'Eris con is awake in this temporary world. API hook can replace this response later.',
        },
      },
      {
        id: 'memory-node',
        label: 'Memory',
        x: 320,
        y: 112,
        radius: 34,
        action: {
          type: 'notice',
          message: 'Memory node: this will become the place where Eris explains what she keeps and why.',
        },
      },
      {
        id: 'observer-node',
        label: 'Observer',
        x: 198,
        y: 210,
        radius: 34,
        action: {
          type: 'notice',
          message: 'Observer node: read-only context, watching without silently changing the workspace.',
        },
      },
      {
        id: 'tools-node',
        label: 'Tools',
        x: 442,
        y: 210,
        radius: 34,
        action: {
          type: 'notice',
          message: 'Tools node: future API actions live behind explicit user interaction.',
        },
      },
    ],
  },
};

export function createInitialState(worldId = 'museum') {
  const world = WORLD_DEFS[worldId] ?? WORLD_DEFS.museum;
  const worlds = Object.fromEntries(
    Object.entries(WORLD_DEFS).map(([id, definition]) => [
      id,
      cloneData(definition.initialState ?? {}),
    ]),
  );

  return {
    worldId: world.id,
    player: { ...world.spawn },
    inventory: [],
    message: '',
    worlds,
    elapsed: 0,
  };
}

export function currentWorld(state) {
  return WORLD_DEFS[state.worldId] ?? WORLD_DEFS.museum;
}

export function movePlayer(state, input, dt) {
  const world = currentWorld(state);
  const next = cloneState(state);
  const vector = inputVector(input);
  const distance = PLAYER_SPEED * Math.max(0, dt);

  next.player.x += vector.x * distance;
  next.player.y += vector.y * distance;

  // 8-hướng facing theo cách Ashes dùng.
  const dx = vector.x;
  const dy = vector.y;
  if (dx < 0 && dy < 0) next.player.facing = 'up_left';
  else if (dx > 0 && dy < 0) next.player.facing = 'up_right';
  else if (dx < 0 && dy > 0) next.player.facing = 'down_left';
  else if (dx > 0 && dy > 0) next.player.facing = 'down_right';
  else if (dx < 0) next.player.facing = 'left';
  else if (dx > 0) next.player.facing = 'right';
  else if (dy < 0) next.player.facing = 'up';
  else if (dy > 0) next.player.facing = 'down';

  next.player.x = clamp(next.player.x, world.bounds.left, world.bounds.right);
  next.player.y = clamp(next.player.y, world.bounds.top, world.bounds.bottom);
  next.elapsed += Math.max(0, dt);
  return next;
}

export function nearestInteractable(state) {
  const world = currentWorld(state);
  const worldState = state.worlds[world.id] ?? {};
  const candidates = [
    ...(world.exits ?? []),
    ...(worldState.interactables ?? []),
    ...(world.props ?? []),
  ];

  let nearest = null;
  let nearestDistance = Infinity;
  for (const candidate of candidates) {
    const d = Math.hypot(state.player.x - candidate.x, state.player.y - candidate.y);
    if (d <= candidate.radius && d < nearestDistance) {
      nearest = candidate;
      nearestDistance = d;
    }
  }
  return nearest;
}

export function applyInteraction(state) {
  const target = nearestInteractable(state);
  if (!target) {
    const next = cloneState(state);
    next.message = 'Move closer to an object.';
    return { state: next, events: [{ type: 'empty' }] };
  }

  const action = target.action ?? { type: 'notice', message: target.label };
  const handler = INTERACTION_HANDLERS[action.type] ?? INTERACTION_HANDLERS.notice;
  return handler(state, target, action);
}

const INTERACTION_HANDLERS = {
  'enter-world': (state, target, action) => {
    const targetWorld = WORLD_DEFS[action.targetWorld];
    if (!targetWorld) {
      return interactionNotice(state, target, {
        message: `Unknown world: ${action.targetWorld ?? 'undefined'}.`,
      });
    }

    const next = cloneState(state);
    next.worldId = targetWorld.id;
    next.player = { ...(action.targetSpawn ?? targetWorld.spawn) };
    next.message = `Entered ${targetWorld.label}.`;
    return {
      state: next,
      events: [{ type: 'world-change', targetWorld: targetWorld.id, id: target.id }],
    };
  },

  pickup: (state, target, action) => {
    const next = cloneState(state);
    const worldState = next.worlds[next.worldId] ?? (next.worlds[next.worldId] = {});
    const interactables = worldState.interactables ?? [];
    worldState.interactables = interactables.filter((item) => item.id !== target.id);

    const inventoryItem = action.inventoryItem ?? target.label;
    next.inventory = [...next.inventory, inventoryItem];
    next.message = action.message ?? `Picked up ${target.label}.`;
    return {
      state: next,
      events: [{ type: 'pickup', item: inventoryItem, id: target.id }],
    };
  },

  emit: (state, target, action) => {
    const next = cloneState(state);
    next.message = action.message ?? target.label;
    return {
      state: next,
      events: [{
        type: action.eventType ?? 'notice',
        message: next.message,
        id: target.id,
      }],
    };
  },

  notice: interactionNotice,
};

function interactionNotice(state, target, action) {
  const next = cloneState(state);
  next.message = action.message ?? target.label;
  return {
    state: next,
    events: [{ type: 'notice', message: next.message, id: target.id }],
  };
}

export function erisReply(message, node = 'terminal') {
  const trimmed = String(message ?? '').trim();
  if (!trimmed) {
    return 'Eris con waits. Choose a node or type something first.';
  }
  return `Eris con heard "${trimmed}" from ${node}. The real API can answer here later.`;
}

function inputVector(input) {
  let x = 0;
  let y = 0;
  if (input.left) x -= 1;
  if (input.right) x += 1;
  if (input.up) y -= 1;
  if (input.down) y += 1;
  if (x && y) {
    x *= Math.SQRT1_2;
    y *= Math.SQRT1_2;
  }
  return { x, y };
}

function cloneState(state) {
  return cloneData(state);
}

function cloneData(value) {
  if (typeof structuredClone === 'function') {
    return structuredClone(value);
  }
  return JSON.parse(JSON.stringify(value));
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}
