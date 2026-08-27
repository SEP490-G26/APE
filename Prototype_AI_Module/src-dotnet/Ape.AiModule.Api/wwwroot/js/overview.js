import {
  state,
  $,
  copyText,
  fetchJson,
  loadPrompts,
  loadProviders,
  putJson,
  setHtml,
  setJson
} from './shared/core.js';

const SOURCE_CONFIG = {
  gatekeeper: {
    policy: '/api/ai-module/gatekeeper/policy',
    rubric: '/api/ai-module/gatekeeper/rubric',
    groundTruth: '/api/ai-module/gatekeeper/ground-truth'
  },
  'extracted-content': {
    policy: '/api/ai-module/extracted-content/policy',
    rubric: '/api/ai-module/extracted-content/rubric',
    groundTruth: '/api/ai-module/extracted-content/ground-truth'
  },
  'embedding-tagging': {
    policy: '/api/ai-module/embedding-tagging/policy',
    rubric: '/api/ai-module/embedding-tagging/rubric',
    groundTruth: '/api/ai-module/embedding-tagging/ground-truth'
  },
  'question-generation': {
    rubric: (subject, questionType) => `/api/ai-module/question-generation/rubric?subject=${encodeURIComponent(subject)}&questionType=${encodeURIComponent(questionType)}`,
    groundTruth: (subject, questionType) => `/api/ai-module/question-generation/ground-truth?subject=${encodeURIComponent(subject)}&questionType=${encodeURIComponent(questionType)}`
  },
  'code-mentor': {
    policy: '/api/ai-module/code-mentor/policy',
    rubric: '/api/ai-module/code-mentor/rubric',
    groundTruth: '/api/ai-module/code-mentor/ground-truth'
  }
};

export function initOverview() {
  $('#refresh-provider-list')?.addEventListener('click', loadProviders);
  $('#reload-provider-config')?.addEventListener('click', async () => {
    const result = await fetchJson('/api/ai-module/providers/reload', { method: 'POST' });
    setJson('provider-raw', result);
    await loadProviders();
  });

  $('#refresh-prompt-list')?.addEventListener('click', loadPrompts);
  $('#source-load')?.addEventListener('click', loadSourceOfTruth);
  $('#source-copy')?.addEventListener('click', async () => {
    const value = $('#source-json')?.textContent ?? '';
    await copyText(value);
  });

  $('#prompt-load')?.addEventListener('click', loadPromptIntoEditor);
  $('#prompt-save')?.addEventListener('click', savePromptFromEditor);
}

export async function loadInitialOverview() {
  await loadProviders();
  await loadPrompts();
  await loadSourceOfTruth();
  await loadPromptIntoEditor();
}

async function loadSourceOfTruth() {
  const kind = $('#source-kind')?.value;
  const functionName = $('#source-function')?.value;
  const promptKey = $('#source-prompt-key')?.value;
  const subject = $('#source-subject')?.value;
  const questionType = $('#source-question-type')?.value;

  let payload = null;
  let label = '';

  if (kind === 'prompt') {
    if (!promptKey) {
      return;
    }

    payload = await fetchJson(`/api/ai-module/prompts/${encodeURIComponent(promptKey)}`);
    label = `Prompt: ${promptKey}`;
  } else {
    const config = SOURCE_CONFIG[functionName];
    const endpoint = config?.[kind === 'ground-truth' ? 'groundTruth' : kind];
    if (!endpoint) {
      payload = { message: `No ${kind} for ${functionName}.` };
      label = `${functionName} | ${kind}`;
    } else {
      const url = typeof endpoint === 'function' ? endpoint(subject, questionType) : endpoint;
      payload = await fetchJson(url);
      label = `${functionName} | ${kind}`;
    }
  }

  setHtml('source-meta', `<div class="kv-block"><p><strong>Loaded:</strong> ${label}</p></div>`);
  setJson('source-json', payload);
}

async function loadPromptIntoEditor() {
  const key = $('#prompt-editor-key')?.value;
  if (!key) {
    return;
  }

  const prompt = await fetchJson(`/api/ai-module/prompts/${encodeURIComponent(key)}`);
  $('#prompt-version').value = prompt.version ?? prompt.Version ?? '';
  $('#prompt-description').value = prompt.description ?? prompt.Description ?? '';
  $('#prompt-system').value = prompt.systemPrompt ?? prompt.SystemPrompt ?? '';
  $('#prompt-user').value = prompt.userPrompt ?? prompt.UserPrompt ?? '';
  $('#prompt-active').value = String(prompt.isActive ?? prompt.IsActive ?? false);
  setJson('prompt-editor-json', prompt);
}

async function savePromptFromEditor() {
  const key = $('#prompt-editor-key')?.value;
  if (!key) {
    return;
  }

  const payload = {
    key,
    version: $('#prompt-version').value.trim(),
    description: $('#prompt-description').value.trim(),
    systemPrompt: $('#prompt-system').value,
    userPrompt: $('#prompt-user').value,
    isActive: $('#prompt-active').value === 'true'
  };

  const saved = await putJson(`/api/ai-module/prompts/${encodeURIComponent(key)}`, payload);
  setJson('prompt-editor-json', saved);
  await loadPrompts();
}
