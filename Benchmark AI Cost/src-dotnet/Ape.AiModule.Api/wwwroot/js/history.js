import {
  state,
  $,
  copyText,
  escapeHtml,
  exportJsonFile,
  fetchJson,
  formatCurrency,
  formatDateTime,
  formatNumber,
  normalizeFunctionName,
  renderSummaryCards,
  setHtml,
  setJson,
  stringifyJson
} from './shared/core.js';

export function initHistory(renderers) {
  $('#history-load-list')?.addEventListener('click', () => loadHistoryList());
  $('#history-apply-filters')?.addEventListener('click', refreshHistoryBrowser);
  $('#history-clear-filters')?.addEventListener('click', clearFilters);
  $('#history-copy-json')?.addEventListener('click', async () => {
    if (state.historyDetail) {
      await copyText(stringifyJson(state.historyDetail));
    }
  });
  $('#history-copy-tsv')?.addEventListener('click', async () => {
    if (state.historyDetail?.summary) {
      await copyText(buildTsv(state.historyDetail.summary));
    }
  });
  $('#history-copy-tsv-list')?.addEventListener('click', async () => {
    if (state.historyList.length > 0) {
      await copyText(state.historyList.map((item) => buildTsv(item)).join('\n'));
    }
  });
  $('#history-export-json')?.addEventListener('click', () => {
    if (state.historyDetail) {
      exportJsonFile('history-detail', state.historyDetail);
    }
  });
  $('#history-export-list-json')?.addEventListener('click', () => {
    if (state.historyList.length > 0) {
      exportJsonFile('history-list', state.historyList);
    }
  });

  async function openDetail(functionName, runId) {
    const detail = await fetchJson(`/api/ai-module/history/${functionName}/${encodeURIComponent(runId)}`);
    state.historyDetail = detail;
    setJson('history-detail-json', detail);
    const summary = detail.summary ?? detail.Summary ?? {};
    const routeKey = normalizeFunctionName(summary.routeKey ?? summary.RouteKey ?? functionName);
    const renderer = renderers[routeKey] ?? renderers[normalizeFunctionName(functionName)];
    const response = detail.response ?? detail.Response ?? {};
    const parsed = renderer ? renderer(response) : `<pre class="json-view">${escapeHtml(stringifyJson(response))}</pre>`;
    setHtml('history-detail-result', `
      <div class="kv-block">
        <p><strong>Run ID:</strong> ${escapeHtml(summary.runId ?? summary.RunId ?? '')}</p>
        <p><strong>Function:</strong> ${escapeHtml(summary.functionName ?? summary.FunctionName ?? '')}</p>
        <p><strong>Route:</strong> ${escapeHtml(summary.routeKey ?? summary.RouteKey ?? '')}</p>
        <p><strong>Recorded:</strong> ${escapeHtml(formatDateTime(summary.recordedAt ?? summary.RecordedAt ?? ''))}</p>
        <p><strong>Model summary:</strong> ${escapeHtml(summary.modelSummary ?? summary.ModelSummary ?? '')}</p>
      </div>
      ${parsed}
    `);
  }

  window.ApeUiHistory = {
    loadHistoryList,
    openDetail
  };
}

export async function loadInitialHistory() {
  await loadHistoryList();
}

async function loadHistoryList() {
  const functionName = $('#history-function')?.value ?? 'gatekeeper';
  const take = Number($('#history-take')?.value || 30);
  const items = await fetchJson(`/api/ai-module/history/${encodeURIComponent(functionName)}?take=${take}`);
  state.historyMasterList = Array.isArray(items) ? items : [];
  refreshHistoryBrowser();
}

function refreshHistoryBrowser() {
  state.historyList = applyFilters(state.historyMasterList);
  setJson('history-list-json', state.historyList);
  renderHistoryCards();
  renderHistorySummary();
}

function renderHistoryCards() {
  if (state.historyList.length === 0) {
    setHtml('history-list-parsed', '<div class="placeholder">No history item matched the current filters.</div>');
    return;
  }

  const html = state.historyList.map((item) => {
    const runId = item.runId ?? item.RunId ?? '';
    return `
      <article class="list-card history-card" data-run-id="${escapeHtml(runId)}">
        <h4>${escapeHtml(runId)}</h4>
        <p><strong>Status:</strong> ${escapeHtml(item.status ?? item.Status ?? '')}</p>
        <p><strong>Recorded:</strong> ${escapeHtml(formatDateTime(item.recordedAt ?? item.RecordedAt ?? ''))}</p>
        <p><strong>File:</strong> ${escapeHtml(item.fileName ?? item.FileName ?? '')}</p>
        <p><strong>Model:</strong> ${escapeHtml(item.modelSummary ?? item.ModelSummary ?? '')}</p>
        <p><strong>Cost:</strong> ${escapeHtml(formatCurrency(item.totals?.totalCostUsd ?? item.Totals?.TotalCostUsd ?? 0))}</p>
      </article>
    `;
  }).join('');

  setHtml('history-list-parsed', html);
  document.querySelectorAll('.history-card').forEach((card) => {
    card.addEventListener('click', async () => {
      const functionName = $('#history-function')?.value ?? 'gatekeeper';
      const runId = card.dataset.runId;
      await window.ApeUiHistory.openDetail(functionName, runId);
    });
  });
}

function renderHistorySummary() {
  const totalRuns = state.historyList.length;
  const totalCost = state.historyList.reduce((sum, item) => sum + Number(item.totals?.totalCostUsd ?? item.Totals?.TotalCostUsd ?? 0), 0);
  const totalInput = state.historyList.reduce((sum, item) => sum + Number(item.totals?.inputTokens ?? item.Totals?.InputTokens ?? 0), 0);
  const totalOutput = state.historyList.reduce((sum, item) => sum + Number(item.totals?.outputTokens ?? item.Totals?.OutputTokens ?? 0), 0);
  renderSummaryCards('history-summary', [
    { label: 'Runs', value: totalRuns },
    { label: 'Input tokens', value: formatNumber(totalInput) },
    { label: 'Output tokens', value: formatNumber(totalOutput) },
    { label: 'Total cost', value: formatCurrency(totalCost) }
  ]);
}

function applyFilters(items) {
  const search = ($('#history-search')?.value ?? '').trim().toLowerCase();
  const status = ($('#history-status-filter')?.value ?? '').trim().toLowerCase();
  const model = ($('#history-model-filter')?.value ?? '').trim().toLowerCase();
  const from = $('#history-date-from')?.value ? new Date($('#history-date-from').value) : null;
  const to = $('#history-date-to')?.value ? new Date($('#history-date-to').value) : null;
  if (to) {
    to.setHours(23, 59, 59, 999);
  }

  return (items ?? []).filter((item) => {
    const haystack = [
      item.runId ?? item.RunId ?? '',
      item.fileName ?? item.FileName ?? '',
      item.modelSummary ?? item.ModelSummary ?? '',
      item.routeKey ?? item.RouteKey ?? ''
    ].join(' ').toLowerCase();

    const recorded = new Date(item.recordedAt ?? item.RecordedAt ?? 0);
    if (search && !haystack.includes(search)) return false;
    if (status && String(item.status ?? item.Status ?? '').toLowerCase() !== status) return false;
    if (model && !String(item.modelSummary ?? item.ModelSummary ?? '').toLowerCase().includes(model)) return false;
    if (from && recorded < from) return false;
    if (to && recorded > to) return false;
    return true;
  });
}

function clearFilters() {
  $('#history-search').value = '';
  $('#history-status-filter').value = '';
  $('#history-model-filter').value = '';
  $('#history-date-from').value = '';
  $('#history-date-to').value = '';
  refreshHistoryBrowser();
}

function buildTsv(summary) {
  return [
    summary.runId ?? summary.RunId ?? '',
    summary.functionName ?? summary.FunctionName ?? '',
    summary.routeKey ?? summary.RouteKey ?? '',
    summary.recordedAt ?? summary.RecordedAt ?? '',
    summary.status ?? summary.Status ?? '',
    summary.modelSummary ?? summary.ModelSummary ?? '',
    summary.fileName ?? summary.FileName ?? '',
    summary.totals?.inputTokens ?? summary.Totals?.InputTokens ?? 0,
    summary.totals?.outputTokens ?? summary.Totals?.OutputTokens ?? 0,
    summary.totals?.totalCostUsd ?? summary.Totals?.TotalCostUsd ?? 0,
    summary.totals?.latencyMs ?? summary.Totals?.LatencyMs ?? 0
  ].join('\t');
}
