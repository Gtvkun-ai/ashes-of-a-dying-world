import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';

const expectedModules = [
  'src/runtime/assets.js',
  'src/runtime/input.js',
  'src/runtime/view.js',
  'src/runtime/shared-render.js',
  'src/runtime/game-runtime.js',
  'src/worlds/museum-renderer.js',
  'src/worlds/ashes-renderer.js',
  'src/worlds/eris-renderer.js',
];

test('browser runtime and world renderers are split into focused modules', () => {
  for (const file of expectedModules) {
    assert.equal(existsSync(file), true, `${file} should exist`);
  }

  const bootstrap = readFileSync('script.js', 'utf8');
  const lineCount = bootstrap.split(/\r?\n/).length;
  assert.ok(lineCount < 180, `script.js should stay a small bootstrap, got ${lineCount} lines`);
});
