import { state } from './state.js';
import { $$, escapeHtml, setHtml, setJson } from './dom.js';
import { fetchJson } from './api.js';

export function formatCurrency(value) {
  const number = Number(value ?? 0);
  return Number.isFinite(number) ? `$${number.toFixed(6)}` : '$0.000000';
}

export function formatNumber(value) {
  const number = Number(value ?? 0);
  return Number.isFinite(number) ? number.toLocaleString('en-US') : '0';
}

export function formatLatency(value) {
  const number = Number(value ?? 0);
  return Number.isFinite(number) ? number.toFixed(2) : '0.00';
}

export function formatDateTime(value) {
  if (!value) {
    return '';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? String(value) : parsed.toLocaleString();
}

export function normalizeFunctionName(value) {
  return String(value ?? '').trim().toLowerCase();
}

export function inferProviderFromModel(model) {
  const value = String(model ?? '').toLowerCase();
  if (value.includes('gemini')) return 'gemini';
  if (value.includes('command') || value.includes('embed-') || value.includes('cohere')) return 'cohere';
  return 'openai';
}

export function renderBadge(value) {
  const label = String(value ?? '');
  const normalized = label.toLowerCase();
  let tone = 'neutral';
  if (['completed', 'accepted', 'supported', 'correct', 'enabled'].includes(normalized)) {
    tone = 'ok';
  } else if (['failed', 'unsupported', 'incorrect', 'needs_revision', 'needs_fix', 'disabled', 'missing'].includes(normalized)) {
    tone = 'danger';
  } else if (['ambiguous', 'pending', 'acceptable_with_minor_notes'].includes(normalized)) {
    tone = 'warn';
  }

  return `<span class="status-pill status-pill--${tone}">${escapeHtml(label || 'n/a')}</span>`;
}

export function renderSummaryCards(targetId, cards) {
  const html = (cards ?? []).map((card) => `
    <article class="summary-card">
      <span class="summary-label">${escapeHtml(card.label)}</span>
      <strong class="summary-value">${card.badge ? renderBadge(card.value) : escapeHtml(card.value)}</strong>
    </article>
  `).join('');

  setHtml(targetId, html || '<article class="summary-card"><span class="summary-label">No data</span><strong class="summary-value">Run a request</strong></article>');
}

export function summarizeUsageLogs(data) {
  const logs = Array.isArray(data?.usageLogs) ? data.usageLogs : Array.isArray(data?.UsageLogs) ? data.UsageLogs : [];
  return logs.reduce((accumulator, item) => {
    accumulator.inputTokens += Number(item?.inputTokens ?? item?.InputTokens ?? 0);
    accumulator.outputTokens += Number(item?.outputTokens ?? item?.OutputTokens ?? 0);
    accumulator.cost += Number(item?.costUsd ?? item?.CostUsd ?? 0);
    return accumulator;
  }, { inputTokens: 0, outputTokens: 0, cost: 0 });
}

export function getTotals(data) {
  return data?.totals ?? data?.Totals ?? null;
}

export function renderMetricCards(data) {
  const totals = getTotals(data);
  const fallback = summarizeUsageLogs(data);
  const inputTokens = Number(totals?.inputTokens ?? totals?.InputTokens ?? fallback.inputTokens ?? 0);
  const outputTokens = Number(totals?.outputTokens ?? totals?.OutputTokens ?? fallback.outputTokens ?? 0);
  const inputCost = Number(totals?.inputCostUsd ?? totals?.InputCostUsd ?? 0);
  const outputCost = Number(totals?.outputCostUsd ?? totals?.OutputCostUsd ?? 0);
  const totalCost = Number(totals?.totalCostUsd ?? totals?.TotalCostUsd ?? fallback.cost ?? 0);
  const latency = Number(totals?.latencyMs ?? totals?.LatencyMs ?? 0);

  return `
    <div class="summary-grid">
      <article class="summary-card"><span class="summary-label">Input tokens</span><strong class="summary-value">${escapeHtml(formatNumber(inputTokens))}</strong></article>
      <article class="summary-card"><span class="summary-label">Output tokens</span><strong class="summary-value">${escapeHtml(formatNumber(outputTokens))}</strong></article>
      <article class="summary-card"><span class="summary-label">Input cost</span><strong class="summary-value">${escapeHtml(formatCurrency(inputCost))}</strong></article>
      <article class="summary-card"><span class="summary-label">Output cost</span><strong class="summary-value">${escapeHtml(formatCurrency(outputCost))}</strong></article>
      <article class="summary-card"><span class="summary-label">Total cost</span><strong class="summary-value">${escapeHtml(formatCurrency(totalCost))}</strong></article>
      <article class="summary-card"><span class="summary-label">Latency (ms)</span><strong class="summary-value">${escapeHtml(formatLatency(latency))}</strong></article>
    </div>
  `;
}

export function renderStageLogs(data) {
  const items = Array.isArray(data?.stageLogs) ? data.stageLogs : Array.isArray(data?.StageLogs) ? data.StageLogs : [];
  if (items.length === 0) {
    return '';
  }

  const rows = items.map((item) => {
    const cost = item?.cost ?? item?.Cost ?? {};
    return `
      <tr>
        <td>${escapeHtml(item?.stageName ?? item?.StageName ?? '')}</td>
        <td>${renderBadge(item?.status ?? item?.Status ?? '')}</td>
        <td>${escapeHtml(item?.modelFields?.primaryModel ?? item?.ModelFields?.PrimaryModel ?? item?.modelFields?.generatorModel ?? item?.ModelFields?.GeneratorModel ?? '')}</td>
        <td>${escapeHtml(formatNumber(cost?.inputTokens ?? cost?.InputTokens ?? 0))}</td>
        <td>${escapeHtml(formatNumber(cost?.outputTokens ?? cost?.OutputTokens ?? 0))}</td>
        <td>${escapeHtml(formatCurrency(cost?.totalCostUsd ?? cost?.TotalCostUsd ?? 0))}</td>
        <td>${escapeHtml(formatLatency(cost?.latencyMs ?? cost?.LatencyMs ?? 0))}</td>
      </tr>
    `;
  }).join('');

  return `
    <details class="details-block" open>
      <summary>Stage logs</summary>
      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Stage</th>
              <th>Status</th>
              <th>Model</th>
              <th>Input Tok</th>
              <th>Output Tok</th>
              <th>Total Cost</th>
              <th>Latency</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>
    </details>
  `;
}

export function renderUsageLogs(data) {
  const items = Array.isArray(data?.usageLogs) ? data.usageLogs : Array.isArray(data?.UsageLogs) ? data.UsageLogs : [];
  if (items.length === 0) {
    return '';
  }

  const rows = items.map((item) => `
    <tr>
      <td>${escapeHtml(item?.stageName ?? item?.StageName ?? '')}</td>
      <td>${escapeHtml(item?.provider ?? item?.Provider ?? '')}</td>
      <td>${escapeHtml(item?.modelName ?? item?.ModelName ?? '')}</td>
      <td>${escapeHtml(formatNumber(item?.inputTokens ?? item?.InputTokens ?? 0))}</td>
      <td>${escapeHtml(formatNumber(item?.outputTokens ?? item?.OutputTokens ?? 0))}</td>
      <td>${escapeHtml(formatCurrency(item?.costUsd ?? item?.CostUsd ?? 0))}</td>
      <td>${escapeHtml(item?.usageSource ?? item?.UsageSource ?? '')}</td>
      <td>${renderBadge(item?.status ?? item?.Status ?? '')}</td>
    </tr>
  `).join('');

  return `
    <details class="details-block">
      <summary>Usage logs</summary>
      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Stage</th>
              <th>Provider</th>
              <th>Model</th>
              <th>Input Tok</th>
              <th>Output Tok</th>
              <th>Cost</th>
              <th>Usage source</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>
    </details>
  `;
}

export function renderJsonDetails(title, value, open = false) {
  if (value == null) {
    return '';
  }

  return `
    <details class="details-block" ${open ? 'open' : ''}>
      <summary>${escapeHtml(title)}</summary>
      <pre class="json-view">${escapeHtml(JSON.stringify(value ?? {}, null, 2))}</pre>
    </details>
  `;
}

export function renderTextBlock(title, value, open = true) {
  if (!value) {
    return '';
  }

  return `
    <details class="details-block" ${open ? 'open' : ''}>
      <summary>${escapeHtml(title)}</summary>
      <pre class="json-view">${escapeHtml(String(value))}</pre>
    </details>
  `;
}

export async function loadProviders() {
  const providers = await fetchJson('/api/ai-module/providers');
  state.providerList = Array.isArray(providers) ? providers : [];
  setJson('provider-raw', state.providerList);
  renderProviderCards();
  await populateAllProviderSelects();
  return state.providerList;
}

export function renderProviderCards() {
  const html = state.providerList.map((item) => `
    <article class="provider-card">
      <h4>${escapeHtml(item.provider ?? item.Provider ?? '')}</h4>
      <p><strong>Enabled:</strong> ${renderBadge((item.enabled ?? item.Enabled) ? 'enabled' : 'disabled')}</p>
      <p><strong>API key:</strong> ${renderBadge((item.hasApiKey ?? item.HasApiKey) ? 'configured' : 'missing')}</p>
      <p><strong>Base URL:</strong> ${escapeHtml(item.baseUrl ?? item.BaseUrl ?? '')}</p>
      <p><strong>Text model:</strong> ${escapeHtml(item.defaultTextModel ?? item.DefaultTextModel ?? '')}</p>
      <p><strong>Vision model:</strong> ${escapeHtml(item.defaultVisionModel ?? item.DefaultVisionModel ?? '')}</p>
      <p><strong>Embedding model:</strong> ${escapeHtml(item.defaultEmbeddingModel ?? item.DefaultEmbeddingModel ?? '')}</p>
    </article>
  `).join('');

  setHtml('provider-cards', html || '<div class="placeholder">No provider data loaded.</div>');
}

export async function getProviderCatalog(provider) {
  const key = String(provider ?? '').trim().toLowerCase();
  if (!key) {
    return null;
  }

  if (!state.providers[key]) {
    state.providers[key] = await fetchJson(`/api/ai-module/providers/${key}/models`);
  }

  return state.providers[key];
}

export async function populateProviderModels(provider, modelSelectId) {
  const select = document.getElementById(modelSelectId);
  if (!select || !provider) {
    return;
  }

  const catalog = await getProviderCatalog(provider);
  const models = Array.isArray(catalog?.models) ? catalog.models : Array.isArray(catalog?.Models) ? catalog.Models : [];
  const current = select.value;
  select.innerHTML = models.map((item) => {
    const id = item.id ?? item.Id ?? item.name ?? item.Name ?? '';
    const label = item.name ?? item.Name ?? id;
    return `<option value="${escapeHtml(id)}">${escapeHtml(label)}</option>`;
  }).join('');

  if (current && Array.from(select.options).some((option) => option.value === current)) {
    select.value = current;
  }
}

export async function populateAllProviderSelects() {
  const selects = $$('[data-provider-select]');
  selects.forEach((select) => {
    const current = select.value;
    select.innerHTML = state.providerList.map((item) => {
      const provider = item.provider ?? item.Provider ?? '';
      return `<option value="${escapeHtml(provider.toLowerCase())}">${escapeHtml(provider.toLowerCase())}</option>`;
    }).join('');

    if (current && Array.from(select.options).some((option) => option.value === current)) {
      select.value = current;
    }
  });

  for (const select of selects) {
    const provider = select.value || select.options[0]?.value;
    const target = select.dataset.modelTarget;
    if (provider && target) {
      await populateProviderModels(provider, target);
    }
  }
}

export function bindProviderSelectors() {
  $$('[data-provider-select]').forEach((select) => {
    select.addEventListener('change', async () => {
      const target = select.dataset.modelTarget;
      await populateProviderModels(select.value, target);
    });
  });
}

export async function loadPrompts() {
  const prompts = await fetchJson('/api/ai-module/prompts');
  state.prompts = Array.isArray(prompts) ? prompts : [];
  setJson('prompt-raw', state.prompts);
  renderPromptList();
  fillPromptSelects();
  return state.prompts;
}

export function renderPromptList() {
  const html = state.prompts.map((item) => `
    <article class="list-card">
      <h4>${escapeHtml(item.key ?? item.Key ?? '')}</h4>
      <p><strong>Version:</strong> ${escapeHtml(item.version ?? item.Version ?? '')}</p>
      <p><strong>Active:</strong> ${escapeHtml(String(item.isActive ?? item.IsActive ?? false))}</p>
      <p>${escapeHtml(item.description ?? item.Description ?? '')}</p>
    </article>
  `).join('');

  setHtml('prompt-list', html || '<div class="placeholder">No prompt loaded.</div>');
}

export function fillPromptSelects() {
  const options = state.prompts.map((item) => {
    const key = item.key ?? item.Key ?? '';
    return `<option value="${escapeHtml(key)}">${escapeHtml(key)}</option>`;
  }).join('');

  ['source-prompt-key', 'prompt-editor-key'].forEach((id) => {
    const select = document.getElementById(id);
    if (!select) {
      return;
    }

    const current = select.value;
    select.innerHTML = options;
    if (current && Array.from(select.options).some((option) => option.value === current)) {
      select.value = current;
    }
  });
}
