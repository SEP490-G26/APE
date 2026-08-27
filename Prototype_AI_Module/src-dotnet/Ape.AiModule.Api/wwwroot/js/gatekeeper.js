import {
  escapeHtml,
  parseJsonInput,
  renderJsonDetails,
  renderMetricCards,
  renderStageLogs,
  renderUsageLogs,
  renderBadge,
  readFileText,
  uploadFile
} from './shared/core.js';

export const gatekeeperFeature = {
  key: 'gatekeeper',
  historyKey: 'gatekeeper',
  formId: 'gatekeeper-form',
  endpoint: '/api/ai-module/gatekeeper',
  debugEndpoint: '/api/ai-module/gatekeeper/debug',
  resultId: 'gatekeeper-result',
  jsonId: 'gatekeeper-json',
  buildPayload(form) {
    return {
      userId: form.userId.value.trim(),
      fileName: form.fileName.value.trim(),
      subject: form.subject.value,
      language: form.language.value,
      rawContent: form.rawContent.value,
      model: form.model.value
    };
  },
  renderResult(data) {
    const verdict = data?.verdict ?? data?.Verdict ?? {};
    return `
      <section class="result-section">
        <h4>Decision</h4>
        <div class="summary-grid">
          <article class="summary-card"><span class="summary-label">Verdict</span><strong class="summary-value">${renderBadge(verdict.verdict ?? verdict.Verdict ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Supported</span><strong class="summary-value">${escapeHtml(String(verdict.isSupported ?? verdict.IsSupported ?? false))}</strong></article>
          <article class="summary-card"><span class="summary-label">Primary domain</span><strong class="summary-value">${escapeHtml(verdict.primaryDomain ?? verdict.PrimaryDomain ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Confidence</span><strong class="summary-value">${escapeHtml(String(verdict.confidence ?? verdict.Confidence ?? ''))}</strong></article>
        </div>
        <div class="kv-block">
          <p><strong>Matched subjects:</strong> ${escapeHtml((verdict.matchedSubjects ?? verdict.MatchedSubjects ?? []).join(', ') || '(none)')}</p>
          <p><strong>Detected topics:</strong> ${escapeHtml((verdict.detectedTopics ?? verdict.DetectedTopics ?? []).join(', ') || '(none)')}</p>
          <p><strong>Reason:</strong> ${escapeHtml(verdict.reason ?? verdict.Reason ?? '')}</p>
          <p><strong>Model:</strong> ${escapeHtml(verdict.modelName ?? verdict.ModelName ?? '')}</p>
        </div>
        ${renderMetricCards(data)}
        ${renderStageLogs(data)}
        ${renderUsageLogs(data)}
        ${renderJsonDetails('Policy', data?.policy ?? data?.Policy)}
        ${renderJsonDetails('Rubric', data?.rubric ?? data?.Rubric)}
        ${renderJsonDetails('Ground truth', data?.groundTruth ?? data?.GroundTruth)}
      </section>
    `;
  },
  init() {
    const upload = document.getElementById('gatekeeper-upload');
    const form = document.getElementById('gatekeeper-form');
    if (!upload || !form) {
      return;
    }

    upload.addEventListener('change', async () => {
      const file = upload.files?.[0];
      if (!file) {
        return;
      }

      const result = await uploadFile(file);
      form.fileName.value = result.fileName ?? file.name;
      form.rawContent.value = result.suggestedRawContent ?? await readFileText(file);
    });
  }
};
