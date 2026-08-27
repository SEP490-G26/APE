import {
  escapeHtml,
  parseJsonInput,
  renderJsonDetails,
  renderMetricCards,
  renderStageLogs,
  renderUsageLogs
} from './shared/core.js';

export const mentorFeature = {
  key: 'mentor',
  historyKey: 'code-mentor',
  formId: 'mentor-form',
  endpoint: '/api/ai-module/code-mentor',
  debugEndpoint: '/api/ai-module/code-mentor/debug',
  resultId: 'mentor-result',
  jsonId: 'mentor-json',
  buildPayload(form) {
    return {
      submissionId: form.submissionId.value.trim(),
      userId: form.userId.value.trim(),
      subject: form.subject.value,
      language: form.language.value,
      problem: form.problem.value,
      code: form.code.value || null,
      sourceFiles: parseJsonInput(form.sourceFiles.value, []),
      mentorModel: form.mentorModel.value
    };
  },
  renderResult(data) {
    const feedback = data?.feedback ?? data?.Feedback ?? {};
    return `
      <section class="result-section">
        <h4>Mentor result</h4>
        <div class="summary-grid">
          <article class="summary-card"><span class="summary-label">Verdict</span><strong class="summary-value">${escapeHtml(feedback.verdict ?? feedback.Verdict ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Confidence</span><strong class="summary-value">${escapeHtml(String(feedback.confidence ?? feedback.Confidence ?? ''))}</strong></article>
          <article class="summary-card"><span class="summary-label">Complexity</span><strong class="summary-value">${escapeHtml(feedback.complexity ?? feedback.Complexity ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Model</span><strong class="summary-value">${escapeHtml(feedback.modelName ?? feedback.ModelName ?? '')}</strong></article>
        </div>
        <div class="kv-block">
          <p><strong>Issue categories:</strong> ${escapeHtml((feedback.issueCategories ?? feedback.IssueCategories ?? []).join(', ') || '(none)')}</p>
          <p><strong>Issues:</strong> ${escapeHtml((feedback.issues ?? feedback.Issues ?? []).join(' | ') || '(none)')}</p>
          <p><strong>Suggestions:</strong> ${escapeHtml((feedback.suggestions ?? feedback.Suggestions ?? []).join(' | ') || '(none)')}</p>
          <p><strong>Failing scenarios:</strong> ${escapeHtml((feedback.failingScenarios ?? feedback.FailingScenarios ?? []).join(' | ') || '(none)')}</p>
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
  init() {}
};
