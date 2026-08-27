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
  renderBadge,
  renderJsonDetails,
  renderMetricCards,
  renderSummaryCards,
  renderTextBlock,
  setHtml,
  setJson,
  stringifyJson
} from './shared/core.js';

export function initHistory(renderers) {
  $('#history-load-list')?.addEventListener('click', () => loadHistoryList());
  $('#history-apply-filters')?.addEventListener('click', refreshHistoryBrowser);
  $('#history-clear-filters')?.addEventListener('click', clearFilters);
  $('#history-copy-parsed')?.addEventListener('click', async () => {
    const text = $('#history-detail-result')?.innerText?.trim();
    if (text) {
      await copyText(text);
    }
  });
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
    const parsed = renderHistoryDetailParsed(routeKey, response, renderer);
    setHtml('history-detail-result', `
      <div class="kv-block">
        <p><strong>Run ID:</strong> ${escapeHtml(summary.runId ?? summary.RunId ?? '')}</p>
        <p><strong>Function:</strong> ${escapeHtml(summary.functionName ?? summary.FunctionName ?? '')}</p>
        <p><strong>Route:</strong> ${escapeHtml(summary.routeKey ?? summary.RouteKey ?? '')}</p>
        <p><strong>Recorded:</strong> ${escapeHtml(formatDateTime(summary.recordedAt ?? summary.RecordedAt ?? ''))}</p>
        <p><strong>Status:</strong> ${renderBadge(summary.status ?? summary.Status ?? 'n/a')}</p>
        <p><strong>File:</strong> ${escapeHtml(summary.fileName ?? summary.FileName ?? '')}</p>
        <p><strong>Model summary:</strong> ${escapeHtml(summary.modelSummary ?? summary.ModelSummary ?? '')}</p>
        <p><strong>Usage source:</strong> ${escapeHtml(summary.usageSource ?? summary.UsageSource ?? '(none)')}</p>
      </div>
      ${renderMetricCards(response)}
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
    const functionName = item.functionName ?? item.FunctionName ?? '';
    const routeKey = normalizeFunctionName(item.routeKey ?? item.RouteKey ?? functionName);
    return renderHistoryListCard(item, runId, functionName, routeKey);
  }).join('');

  setHtml('history-list-parsed', html);
  document.querySelectorAll('.history-card').forEach((card) => {
    card.addEventListener('click', async () => {
      const functionName = card.dataset.functionName || $('#history-function')?.value || 'gatekeeper';
      const runId = card.dataset.runId;
      await window.ApeUiHistory.openDetail(functionName, runId);
    });
  });
}

function renderHistoryListCard(item, runId, functionName, routeKey) {
  if (isRetrievalHistoryRoute(routeKey)) {
    const packId = item.packId ?? item.PackId ?? item.contextPackId ?? item.ContextPackId ?? '';
    const packedTokens = item.packedTokens ?? item.PackedTokens ?? item.tokenCount ?? item.TokenCount ?? item.totals?.inputTokens ?? item.Totals?.InputTokens ?? 0;
    const compressionRatio = item.compressionRatio ?? item.CompressionRatio ?? '';
    return `
      <article class="list-card history-card" data-run-id="${escapeHtml(runId)}" data-function-name="${escapeHtml(functionName)}">
        <h4>${escapeHtml(runId)}</h4>
        <p><strong>Status:</strong> ${renderBadge(item.status ?? item.Status ?? 'n/a')}</p>
        <p><strong>Function:</strong> ${escapeHtml(functionName)}</p>
        <p><strong>Route:</strong> ${escapeHtml(item.routeKey ?? item.RouteKey ?? '')}</p>
        <p><strong>Pack ID:</strong> ${escapeHtml(packId || '(generated at runtime)')}</p>
        <p><strong>Packed tokens:</strong> ${escapeHtml(formatNumber(packedTokens))}</p>
        <p><strong>Compression:</strong> ${escapeHtml(String(compressionRatio || '(n/a)'))}</p>
        <p><strong>Recorded:</strong> ${escapeHtml(formatDateTime(item.recordedAt ?? item.RecordedAt ?? ''))}</p>
        <p><strong>Model:</strong> ${escapeHtml(item.modelSummary ?? item.ModelSummary ?? '')}</p>
        <p><strong>Cost:</strong> ${escapeHtml(formatCurrency(item.totals?.totalCostUsd ?? item.Totals?.TotalCostUsd ?? 0))}</p>
      </article>
    `;
  }

  if (isContextPackHistoryRoute(routeKey)) {
    const packId = item.packId ?? item.PackId ?? '';
    const status = item.packStatus ?? item.PackStatus ?? item.status ?? item.Status ?? 'n/a';
    const tokenCount = item.tokenCount ?? item.TokenCount ?? item.totals?.inputTokens ?? item.Totals?.InputTokens ?? 0;
    return `
      <article class="list-card history-card" data-run-id="${escapeHtml(runId)}" data-function-name="${escapeHtml(functionName)}">
        <h4>${escapeHtml(runId)}</h4>
        <p><strong>Status:</strong> ${renderBadge(status)}</p>
        <p><strong>Pack ID:</strong> ${escapeHtml(packId || '(none)')}</p>
        <p><strong>Route:</strong> ${escapeHtml(item.routeKey ?? item.RouteKey ?? '')}</p>
        <p><strong>Token count:</strong> ${escapeHtml(formatNumber(tokenCount))}</p>
        <p><strong>Recorded:</strong> ${escapeHtml(formatDateTime(item.recordedAt ?? item.RecordedAt ?? ''))}</p>
        <p><strong>Model:</strong> ${escapeHtml(item.modelSummary ?? item.ModelSummary ?? '')}</p>
      </article>
    `;
  }

  return `
    <article class="list-card history-card" data-run-id="${escapeHtml(runId)}" data-function-name="${escapeHtml(functionName)}">
      <h4>${escapeHtml(runId)}</h4>
      <p><strong>Status:</strong> ${renderBadge(item.status ?? item.Status ?? 'n/a')}</p>
      <p><strong>Function:</strong> ${escapeHtml(functionName)}</p>
      <p><strong>Route:</strong> ${escapeHtml(item.routeKey ?? item.RouteKey ?? '')}</p>
      <p><strong>Recorded:</strong> ${escapeHtml(formatDateTime(item.recordedAt ?? item.RecordedAt ?? ''))}</p>
      <p><strong>File:</strong> ${escapeHtml(item.fileName ?? item.FileName ?? '')}</p>
      <p><strong>Model:</strong> ${escapeHtml(item.modelSummary ?? item.ModelSummary ?? '')}</p>
      <p><strong>Cost:</strong> ${escapeHtml(formatCurrency(item.totals?.totalCostUsd ?? item.Totals?.TotalCostUsd ?? 0))}</p>
    </article>
  `;
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

function renderHistoryDetailParsed(routeKey, response, fallbackRenderer) {
  if (isRetrievalHistoryRoute(routeKey)) {
    return renderRetrievalHistoryDetail(response);
  }

  if (isContextPackHistoryRoute(routeKey)) {
    return renderContextPackHistoryDetail(response);
  }

  return fallbackRenderer ? fallbackRenderer(response) : `<pre class="json-view">${escapeHtml(stringifyJson(response))}</pre>`;
}

function renderRetrievalHistoryDetail(response) {
  const contextPack = response?.contextPack ?? response?.ContextPack ?? null;
  const tokenBudget = response?.tokenBudget ?? response?.TokenBudget ?? null;
  const candidateStats = response?.candidateStats ?? response?.CandidateStats ?? null;
  const selectedChunks = response?.selectedChunks ?? response?.SelectedChunks ?? [];
  return `
    <section class="result-section">
      <h4>Retrieval run</h4>
      <div class="summary-grid">
        <article class="summary-card"><span class="summary-label">Pack ID</span><strong class="summary-value">${escapeHtml(contextPack?.packId ?? contextPack?.PackId ?? '(none)')}</strong></article>
        <article class="summary-card"><span class="summary-label">Source tokens</span><strong class="summary-value">${escapeHtml(formatNumber(tokenBudget?.estimatedSourceTokens ?? tokenBudget?.EstimatedSourceTokens ?? contextPack?.sourceTokenCount ?? contextPack?.SourceTokenCount ?? 0))}</strong></article>
        <article class="summary-card"><span class="summary-label">Packed tokens</span><strong class="summary-value">${escapeHtml(formatNumber(tokenBudget?.estimatedPackedTokens ?? tokenBudget?.EstimatedPackedTokens ?? contextPack?.tokenCount ?? contextPack?.TokenCount ?? 0))}</strong></article>
        <article class="summary-card"><span class="summary-label">Compression</span><strong class="summary-value">${escapeHtml(String(tokenBudget?.compressionRatio ?? tokenBudget?.CompressionRatio ?? contextPack?.compressionRatio ?? contextPack?.CompressionRatio ?? 0))}</strong></article>
      </div>
      ${candidateStats ? renderJsonDetails('Candidate stats', candidateStats, true) : ''}
      ${renderSelectedChunkTable(selectedChunks)}
      ${renderContextPackSummary(contextPack)}
    </section>
  `;
}

function renderContextPackHistoryDetail(response) {
  const pack = response?.contextPack ?? response?.ContextPack ?? response;
  return `
    <section class="result-section">
      <h4>Context pack run</h4>
      <div class="summary-grid">
        <article class="summary-card"><span class="summary-label">Pack ID</span><strong class="summary-value">${escapeHtml(pack?.packId ?? pack?.PackId ?? '(none)')}</strong></article>
        <article class="summary-card"><span class="summary-label">Status</span><strong class="summary-value">${renderBadge(pack?.packStatus ?? pack?.PackStatus ?? 'n/a')}</strong></article>
        <article class="summary-card"><span class="summary-label">Token count</span><strong class="summary-value">${escapeHtml(formatNumber(pack?.tokenCount ?? pack?.TokenCount ?? 0))}</strong></article>
        <article class="summary-card"><span class="summary-label">Updated</span><strong class="summary-value">${escapeHtml(formatDateTime(pack?.updatedAt ?? pack?.UpdatedAt ?? ''))}</strong></article>
      </div>
      ${renderContextPackSummary(pack)}
    </section>
  `;
}

function renderContextPackSummary(contextPack) {
  if (!contextPack) {
    return '<div class="placeholder">No context pack payload was saved for this run.</div>';
  }

  return `
    <details class="details-block" open>
      <summary>Context pack summary</summary>
      <div class="kv-block">
        <p><strong>Target topics:</strong> ${escapeHtml(((contextPack.targetTopics ?? contextPack.TargetTopics ?? [])).join(', ') || '(none)')}</p>
        <p><strong>Question type:</strong> ${escapeHtml(contextPack.questionType ?? contextPack.QuestionType ?? '(n/a)')}</p>
        <p><strong>Difficulty:</strong> ${escapeHtml(contextPack.targetDifficulty ?? contextPack.TargetDifficulty ?? '(n/a)')}</p>
        <p><strong>Source scope:</strong> ${escapeHtml(contextPack.sourceScope ?? contextPack.SourceScope ?? '(n/a)')}</p>
        <p><strong>Usage count:</strong> ${escapeHtml(formatNumber(contextPack.usageCount ?? contextPack.UsageCount ?? 0))}</p>
        <p><strong>Expires at:</strong> ${escapeHtml(formatDateTime(contextPack.expiresAt ?? contextPack.ExpiresAt ?? ''))}</p>
      </div>
      ${renderTextBlock('Packed summary text', contextPack.packedSummaryText ?? contextPack.PackedSummaryText ?? '', true)}
      ${renderTextBlock('Packed context text', contextPack.packedContextText ?? contextPack.PackedContextText ?? '', false)}
    </details>
  `;
}

function renderSelectedChunkTable(selectedChunks) {
  if (!Array.isArray(selectedChunks) || selectedChunks.length === 0) {
    return '';
  }

  const rows = selectedChunks.map((chunk) => `
    <tr>
      <td>${escapeHtml(chunk.chunkId ?? chunk.ChunkId ?? '')}</td>
      <td>${escapeHtml(chunk.selectionRole ?? chunk.SelectionRole ?? '')}</td>
      <td>${escapeHtml(chunk.topicPrimary ?? chunk.TopicPrimary ?? '')}</td>
      <td>${escapeHtml(formatNumber(chunk.tokenCount ?? chunk.TokenCount ?? 0))}</td>
      <td>${escapeHtml(String(chunk.finalScore ?? chunk.FinalScore ?? 0))}</td>
    </tr>
  `).join('');

  return `
    <details class="details-block" open>
      <summary>Selected chunks</summary>
      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Chunk ID</th>
              <th>Role</th>
              <th>Primary topic</th>
              <th>Tokens</th>
              <th>Final score</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>
    </details>
  `;
}

function isRetrievalHistoryRoute(routeKey) {
  return routeKey === 'retrieval' || routeKey === 'retrieval-plan' || routeKey === 'retrieval-plan-debug';
}

function isContextPackHistoryRoute(routeKey) {
  return routeKey === 'context-packs' || routeKey === 'context-pack-build' || routeKey === 'context-pack-mark-stale';
}
