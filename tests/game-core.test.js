import test from 'node:test';
import assert from 'node:assert/strict';

import {
  applyInteraction,
  ASHES_FIELD,
  createInitialState,
  movePlayer,
  nearestInteractable,
  WORLD_DEFS,
} from '../src/game-core.js';

test('museum Ashes door switches into the Ashes mini world', () => {
  const state = createInitialState();
  const ashesDoor = WORLD_DEFS.museum.exits.find((exit) => exit.id === 'ashes-door');
  state.player.x = ashesDoor.x;
  state.player.y = ashesDoor.y;

  assert.equal(nearestInteractable(state)?.id, 'ashes-door');

  const result = applyInteraction(state);

  assert.equal(result.state.worldId, 'ashes');
  assert.deepEqual(result.state.player, { x: 336, y: 1320, facing: 'down' });
  assert.equal(result.events[0].type, 'world-change');
});

test('movement is clamped to the current world bounds', () => {
  const state = createInitialState();
  state.player.x = 4;
  state.player.y = 4;

  const moved = movePlayer(state, { left: true, up: true }, 1);

  assert.equal(moved.player.x, WORLD_DEFS.museum.bounds.left);
  assert.equal(moved.player.y, WORLD_DEFS.museum.bounds.top);
});

test('Ashes item pickup records inventory and removes the item', () => {
  const state = createInitialState('ashes');
  state.player.x = 840;
  state.player.y = 1168;

  assert.equal(nearestInteractable(state)?.id, 'frost-shard');

  const result = applyInteraction(state);

  assert.deepEqual(result.state.inventory, ['Frost shard']);
  assert.equal(result.state.worlds.ashes.interactables.length, 0);
  assert.equal(result.events[0].type, 'pickup');
});

test('Ashes field uses the real Godot field scale without combat actors', () => {
  assert.equal(WORLD_DEFS.ashes.width, 456 * 4);
  assert.equal(WORLD_DEFS.ashes.height, 474 * 4);
  assert.equal('slimes' in WORLD_DEFS.ashes, false);
  assert.equal(ASHES_FIELD.assetScale, 4);
  assert.equal(WORLD_DEFS.ashes.viewScale, 0.8);
});

test('Ashes field exposes dense real-map vegetation props for rendering', () => {
  const trees = ASHES_FIELD.props.filter((prop) => prop.kind === 'tree');
  const appleTrees = ASHES_FIELD.props.filter((prop) => prop.kind === 'appleTree');
  const grasses = ASHES_FIELD.props.filter((prop) => prop.kind === 'grass' || prop.kind === 'grassPatch');

  assert.ok(trees.length >= 24);
  assert.ok(appleTrees.length >= 8);
  assert.ok(grasses.length >= 24);
  assert.ok(ASHES_FIELD.layers.some((layer) => layer.effect === 'grass-detail'));
  assert.ok(ASHES_FIELD.layers.some((layer) => layer.effect === 'cloud-shadow'));
});

test('Eris terminal interaction keeps the user inside the Eris world', () => {
  const state = createInitialState('eris');
  state.player.x = 320;
  state.player.y = 236;

  assert.equal(nearestInteractable(state)?.id, 'eris-terminal');

  const result = applyInteraction(state);

  assert.equal(result.state.worldId, 'eris');
  assert.equal(result.events[0].type, 'eris-terminal');
  assert.match(result.events[0].message, /Eris con/i);
});



test('initial state creates an independent state bucket for every world', () => {
  const state = createInitialState();

  assert.deepEqual(Object.keys(state.worlds).sort(), Object.keys(WORLD_DEFS).sort());
  assert.ok(Array.isArray(state.worlds.ashes.interactables));
  assert.notEqual(state.worlds.ashes.interactables, WORLD_DEFS.ashes.initialState.interactables);
});

test('state cloning preserves arbitrary nested state for worlds other than Ashes', () => {
  const state = createInitialState('eris');
  state.worlds.eris.memory = { visited: ['memory-node'] };

  const moved = movePlayer(state, { right: true }, 0.1);
  moved.worlds.eris.memory.visited.push('tools-node');

  assert.deepEqual(state.worlds.eris.memory.visited, ['memory-node']);
  assert.deepEqual(moved.worlds.eris.memory.visited, ['memory-node', 'tools-node']);
});

test('generic pickup action works for a dynamic interactable in any world', () => {
  const state = createInitialState('eris');
  state.player.x = 520;
  state.player.y = 100;
  state.worlds.eris.interactables = [
    {
      id: 'test-token',
      label: 'Test token',
      x: 520,
      y: 100,
      radius: 24,
      action: { type: 'pickup' },
    },
  ];

  assert.equal(nearestInteractable(state)?.id, 'test-token');

  const result = applyInteraction(state);

  assert.deepEqual(result.state.inventory, ['Test token']);
  assert.deepEqual(result.state.worlds.eris.interactables, []);
  assert.equal(result.events[0].type, 'pickup');
});

test('world definitions expose declarative actions instead of core-specific ids', () => {
  assert.equal(WORLD_DEFS.museum.exits[0].action.type, 'enter-world');
  assert.equal(WORLD_DEFS.ashes.initialState.interactables[0].action.type, 'pickup');
  assert.equal(WORLD_DEFS.eris.props[0].action.type, 'emit');
  assert.equal(WORLD_DEFS.eris.props[0].action.eventType, 'eris-terminal');
});
