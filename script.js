import { erisReply, nearestInteractable } from './src/game-core.js';
import { ASSET_SOURCES, loadAssets } from './src/runtime/assets.js';
import { createGameRuntime } from './src/runtime/game-runtime.js';
import { createInputController } from './src/runtime/input.js';
import { renderAshes } from './src/worlds/ashes-renderer.js';
import { renderEris } from './src/worlds/eris-renderer.js';
import { renderMuseum } from './src/worlds/museum-renderer.js';

const canvas = document.querySelector('#gameCanvas');
const worldReadout = document.querySelector('#worldReadout');
const worldName = document.querySelector('#worldName');
const worldHint = document.querySelector('#worldHint');
const interactionPrompt = document.querySelector('#interactionPrompt');
const interactionName = document.querySelector('#interactionName');
const discoveryHint = document.querySelector('#discoveryHint');
const inventoryPeek = document.querySelector('#inventoryPeek');
const inventoryText = document.querySelector('#inventoryText');
const messagePlaque = document.querySelector('#messagePlaque');
const messageText = document.querySelector('#messageText');
const terminal = document.querySelector('#erisTerminal');
const terminalLog = document.querySelector('#terminalLog');
const terminalInput = document.querySelector('#terminalInput');
const closeTerminal = document.querySelector('#closeTerminal');
const mobileInteract = document.querySelector('#mobileInteract');

const assets = await loadAssets(ASSET_SOURCES);
const requestedWorld = new URLSearchParams(window.location.search).get('world');
const initialWorld = requestedWorld === 'ashes' || requestedWorld === 'eris' ? requestedWorld : undefined;

let runtime;
let lastMessage = '';
let messageVisibleUntil = 0;

const input = createInputController({
  moveButtons: document.querySelectorAll('[data-move]'),
  interactButton: mobileInteract,
  onInteract: () => runtime?.interact(),
  canInteract: () => terminal.hidden,
  onEscape: () => {
    if (!terminal.hidden) terminal.hidden = true;
  },
});

runtime = createGameRuntime({
  canvas,
  assets,
  initialWorld,
  renderers: {
    museum: renderMuseum,
    ashes: renderAshes,
    eris: renderEris,
  },
  getInputState: () => input.getState(),
  isPaused: () => !terminal.hidden,
  onHudUpdate: updatePresentation,
  onInteractionEvent: handleInteractionEvent,
});

terminal.addEventListener('submit', (event) => {
  event.preventDefault();
  const value = terminalInput.value.trim();
  if (!value) return;
  terminalSay('You', value);
  terminalSay('Eris con', erisReply(value));
  terminalInput.value = '';
});

closeTerminal.addEventListener('click', () => {
  terminal.hidden = true;
});

document.body.classList.add('game-ready');
runtime.start();

function updatePresentation(state, world) {
  const near = nearestInteractable(state);
  const inMuseum = world.id === 'museum';

  document.body.dataset.world = world.id;

  worldReadout.hidden = inMuseum;
  if (!inMuseum) {
    worldName.textContent = world.label;
    worldHint.textContent = world.id === 'ashes'
      ? 'Field 01 / playable exhibit'
      : 'Living archive / prototype chamber';
  }

  interactionPrompt.hidden = !near;
  if (near) interactionName.textContent = formatInteractionLabel(near);

  discoveryHint.hidden = !(inMuseum && state.elapsed < 7 && !near);

  inventoryPeek.hidden = state.inventory.length === 0;
  inventoryText.textContent = state.inventory.join(' · ');

  if (state.message && state.message !== lastMessage) {
    lastMessage = state.message;
    messageVisibleUntil = performance.now() + 4200;
  }

  const showMessage = Boolean(state.message) && performance.now() < messageVisibleUntil;
  messagePlaque.hidden = !showMessage;
  if (showMessage) messageText.textContent = state.message;
}

function formatInteractionLabel(target) {
  if (target.action?.type === 'enter-world') return target.label;
  if (target.action?.type === 'pickup') return `TAKE / ${target.label}`;
  return target.label;
}

function handleInteractionEvent(event) {
  if (event.type === 'eris-terminal') {
    terminal.hidden = false;
    terminalInput.focus();
  }
}

function terminalSay(speaker, text) {
  const p = document.createElement('p');
  const strong = document.createElement('strong');
  strong.textContent = `${speaker}: `;
  p.append(strong, document.createTextNode(text));
  terminalLog.append(p);
  terminalLog.scrollTop = terminalLog.scrollHeight;
}
