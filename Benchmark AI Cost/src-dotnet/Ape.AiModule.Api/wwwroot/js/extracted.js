import {
  escapeHtml,
  renderJsonDetails,
  renderMetricCards,
  renderStageLogs,
  renderUsageLogs,
  readFileText,
  uploadFile
} from './shared/core.js';

export const extractedFeature = {
  key: 'extracted',
  historyKey: 'extracted-content',
  formId: 'extracted-form',
  endpoint: '/api/ai-module/extracted-content',
  debugEndpoint: '/api/ai-module/extracted-content/debug',
  resultId: 'extracted-result',
  jsonId: 'extracted-json',
  buildPayload(form) {
    return {
      userId: form.userId.value.trim(),
      fileName: form.fileName.value.trim(),
      subject: form.subject.value,
      language: form.language.value,
      rawContent: form.rawContent.value,
      mode: form.mode.value,
      visionModel: form.visionModel.value || null
    };
  },
  renderResult(data) {
    const extracted = data?.extracted ?? data?.Extracted ?? {};
    return `
      <section class="result-section">
        <h4>Extraction summary</h4>
        <div class="summary-grid">
          <article class="summary-card"><span class="summary-label">Parser</span><strong class="summary-value">${escapeHtml(extracted.parserName ?? extracted.ParserName ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Vision model</span><strong class="summary-value">${escapeHtml(extracted.visionModelName ?? extracted.VisionModelName ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Words</span><strong class="summary-value">${escapeHtml(String(extracted.wordCount ?? extracted.WordCount ?? 0))}</strong></article>
          <article class="summary-card"><span class="summary-label">Embedded images</span><strong class="summary-value">${escapeHtml(String(extracted.embeddedImageCount ?? extracted.EmbeddedImageCount ?? 0))}</strong></article>
        </div>
        ${renderMetricCards(data)}
        ${renderTextBlocks(extracted)}
        ${renderStageLogs(data)}
        ${renderUsageLogs(data)}
        ${renderJsonDetails('Policy', data?.policy ?? data?.Policy)}
        ${renderJsonDetails('Rubric', data?.rubric ?? data?.Rubric)}
        ${renderJsonDetails('Ground truth', data?.groundTruth ?? data?.GroundTruth)}
      </section>
    `;
  },
  init() {
    const upload = document.getElementById('extracted-upload');
    const form = document.getElementById('extracted-form');
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
      form.mode.value = result.suggestedMode ?? form.mode.value;
      form.rawContent.value = result.suggestedRawContent ?? await readFileText(file);
    });
  }
};

function renderTextBlocks(extracted) {
  const rawText = extracted.rawText ?? extracted.RawText ?? '';
  const markdown = extracted.normalizedMarkdown ?? extracted.NormalizedMarkdown ?? '';
  const warnings = extracted.warnings ?? extracted.Warnings ?? [];
  const imageRefs = extracted.detectedImageReferences ?? extracted.DetectedImageReferences ?? [];

  return `
    <details class="details-block" open>
      <summary>Normalized markdown</summary>
      <pre class="json-view">${escapeHtml(markdown)}</pre>
    </details>
    <details class="details-block">
      <summary>Raw text</summary>
      <pre class="json-view">${escapeHtml(rawText)}</pre>
    </details>
    <div class="kv-block">
      <p><strong>Image refs:</strong> ${escapeHtml(imageRefs.join(', ') || '(none)')}</p>
      <p><strong>Warnings:</strong> ${escapeHtml(warnings.join(' | ') || '(none)')}</p>
    </div>
  `;
}
