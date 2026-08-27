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
} from './js/shared/core.js?v=20260719-mentor-v2';
import { initOverview, loadInitialOverview } from './js/overview.js?v=20260719-mentor-v2';
import { gatekeeperFeature } from './js/gatekeeper.js?v=20260719-mentor-v2';
import { extractedFeature } from './js/extracted.js?v=20260719-mentor-v2';
import { embeddingFeature } from './js/embedding.js?v=20260719-mentor-v2';
import { generationFeature } from './js/generation.js?v=20260719-mentor-v2';
import { mentorFeature } from './js/mentor.js?v=20260719-mentor-v2';
import { initHistory, loadInitialHistory } from './js/history.js?v=20260719-mentor-v2';
import { createRendererMap, fragmentManifest } from './js/manifest.js?v=20260719-mentor-v2';

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

  document.querySelector(`[data-copy-parsed="${feature.key}"]`)?.addEventListener('click', async () => {
    const container = document.getElementById(feature.resultId);
    const text = container?.innerText?.trim();
    if (text) {
      await copyText(text);
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
  try {
    const form = document.getElementById(feature.formId);
    const payload = feature.buildPayload(form);
    const resolvedEndpoint = feature.resolveEndpoint?.(form, endpoint, payload) ?? endpoint;
    setJson(feature.jsonId, { status: 'running', endpoint: resolvedEndpoint, payload });
    setHtml(feature.resultId, '<div class="placeholder">Running...</div>');

    const result = await postJson(resolvedEndpoint, payload);
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
