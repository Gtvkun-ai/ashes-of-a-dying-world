import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import * as museumRenderer from '../src/worlds/museum-renderer.js';
import { createInitialState, WORLD_DEFS } from '../src/game-core.js';

test('museum shell removes persistent website chrome around the canvas', () => {
  const html = readFileSync('index.html', 'utf8');

  assert.doesNotMatch(html, /class="topbar"/);
  assert.doesNotMatch(html, /class="control-strip"/);
  assert.match(html, /id="interactionPrompt"/);
  assert.match(html, /id="discoveryHint"/);
  assert.match(html, /id="inventoryPeek"/);
  assert.match(html, /id="messagePlaque"/);
});

test('museum door labels are revealed only for the nearby door', () => {
  assert.equal(typeof museumRenderer.getMuseumExhibitPresentation, 'function');

  const state = createInitialState('museum');
  const world = WORLD_DEFS.museum;
  state.player.x = world.exits[0].x;
  state.player.y = world.exits[0].y;

  const presentation = museumRenderer.getMuseumExhibitPresentation(world, state);
  const ashes = presentation.find((item) => item.id === 'ashes-door');
  const eris = presentation.find((item) => item.id === 'eris-door');

  assert.equal(ashes?.active, true);
  assert.equal(ashes?.showLabel, true);
  assert.equal(eris?.showLabel, false);
});

test('museum renderer owns its exhibit rendering instead of generic always-on labels', () => {
  const source = readFileSync('src/worlds/museum-renderer.js', 'utf8');
  assert.doesNotMatch(source, /drawGenericInteractables/);
  assert.match(source, /getMuseumExhibitPresentation/);
});

test('museum is a full canvas-size room with more negative space', () => {
  const museum = WORLD_DEFS.museum;
  assert.equal(museum.width, 960);
  assert.equal(museum.height, 540);
  assert.ok(museum.spawn.y > museum.height * 0.7);
  assert.ok(museum.exits[0].y < museum.spawn.y);
  assert.ok(museum.exits[1].y < museum.spawn.y);
});
