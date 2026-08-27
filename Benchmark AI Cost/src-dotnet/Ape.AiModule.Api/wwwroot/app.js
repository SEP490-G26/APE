import {
  state,
  $$,
  bindProviderSelectors,
  copyText,
  exportJsonFile,
  postJson,
  setHtml,
  setJson,
  stringifyJson
} from './js/shared/core.js';
import { initOverview, loadInitialOverview } from './js/overview.js';
import { gatekeeperFeature } from './js/gatekeeper.js';
import { extractedFeature } from './js/extracted.js';
import { embeddingFeature } from './js/embedding.js';
import { generationFeature } from './js/generation.js';
import { mentorFeature } from './js/mentor.js';
import { initHistory, loadInitialHistory } from './js/history.js';
import { createRendererMap, fragmentManifest } from './js/manifest.js';

const featureMap = {
  gatekeeper: gatekeeperFeature,
  extracted: extractedFeature,
  embedding: embeddingFeature,
  generation: generationFeature,
  mentor: mentorFeature
};

const features = Object.values(featureMap);
const rendererMap = createRendererMap(featureMap);

window.ApeUi = { state, features, featureMap };

async function loadFragments() {
  for (const fragment of fragmentManifest) {
    const mount = document.getElementById(fragment.mountId);
    if (!mount) {
      continue;
    }

    const response = await fetch(fragment.path, { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`Failed to load fragment: ${fragment.path}`);
    }

    mount.innerHTML = await response.text();
  }
}

function bindNavigation() {
  $$('.nav-link').forEach((button) => {
    button.addEventListener('click', () => {
      $$('.nav-link').forEach((item) => item.classList.remove('is-active'));
      $$('.tab-panel').forEach((item) => item.classList.remove('is-active'));
      button.classList.add('is-active');
      document.getElementById(button.dataset.tabTarget)?.classList.add('is-active');
    });
  });
}

function bindFeature(feature) {
  const form = document.getElementById(feature.formId);
  if (!form) {
    return;
  }

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    await runFeature(feature, feature.endpoint);
  });

  document.querySelector(`[data-run-debug="${feature.key}"]`)?.addEventListener('click', async () => {
    if (!feature.debugEndpoint) {
      return;
    }

    await runFeature(feature, feature.debugEndpoint);
  });

  document.querySelector(`[data-copy-json="${feature.key}"]`)?.addEventListener('click', async () => {
    const result = state.lastResults[feature.key];
    if (result) {
      await copyText(stringifyJson(result));
    }
  });

  document.querySelector(`[data-export-json="${feature.key}"]`)?.addEventListener('click', () => {
    const result = state.lastResults[feature.key];
    if (result) {
      exportJsonFile(feature.key, result);
    }
  });

  feature.init?.();
}

async function runFeature(feature, endpoint) {
  const form = document.getElementById(feature.formId);
  const payload = feature.buildPayload(form);
  setJson(feature.jsonId, { status: 'running', endpoint, payload });
  setHtml(feature.resultId, '<div class="placeholder">Running...</div>');

  try {
    const result = await postJson(endpoint, payload);
    state.lastResults[feature.key] = result;
    setJson(feature.jsonId, result);
    setHtml(feature.resultId, feature.renderResult(result));
  } catch (error) {
    const wrapped = { error };
    state.lastResults[feature.key] = wrapped;
    setJson(feature.jsonId, wrapped);
    setHtml(feature.resultId, `<pre class="json-view">${escapeForPre(wrapped)}</pre>`);
  }
}

function escapeForPre(value) {
  return String(stringifyJson(value))
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

async function bootstrap() {
  await loadFragments();
  bindNavigation();
  initOverview();
  bindProviderSelectors();
  features.forEach(bindFeature);
  initHistory(rendererMap);
  await loadInitialOverview();
  await loadInitialHistory();
  setHtml('history-detail-result', '<div class="placeholder">Select one run in history to inspect detail.</div>');
}

bootstrap().catch((error) => {
  console.error(error);
  document.body.insertAdjacentHTML('beforeend', `<pre class="json-view">${escapeForPre({ bootstrapError: error.message ?? String(error) })}</pre>`);
});
