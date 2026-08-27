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
      sourceFiles: normalizeSourceFiles(parseJsonInput(form.sourceFiles.value, [])),
      mentorModel: form.mentorModel.value
    };
  },
  renderResult(data) {
    const feedback = data?.feedback ?? data?.Feedback ?? {};
    const quality = feedback.qualityScore ?? feedback.QualityScore ?? {};
    const performance = feedback.performanceSummary ?? feedback.PerformanceSummary ?? {};
    const errors = feedback.errorAnalysis ?? feedback.ErrorAnalysis ?? [];
    const suggestions = feedback.improvementSuggestions ?? feedback.ImprovementSuggestions ?? [];
    return `
      <section class="result-section">
        <h4>Mentor result</h4>
        <div class="summary-grid">
          <article class="summary-card"><span class="summary-label">Verdict</span><strong class="summary-value">${escapeHtml(feedback.verdict ?? feedback.Verdict ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Question type</span><strong class="summary-value">${escapeHtml(feedback.questionType ?? feedback.QuestionType ?? 'PE')}</strong></article>
          <article class="summary-card"><span class="summary-label">Quality score</span><strong class="summary-value">${escapeHtml(String(quality.overall ?? quality.Overall ?? ''))}</strong></article>
          <article class="summary-card"><span class="summary-label">Confidence</span><strong class="summary-value">${escapeHtml(String(quality.confidence ?? quality.Confidence ?? ''))}</strong></article>
          <article class="summary-card"><span class="summary-label">Model</span><strong class="summary-value">${escapeHtml(feedback.modelName ?? feedback.ModelName ?? '')}</strong></article>
        </div>
        <div class="kv-block">
          <p><strong>Feedback text:</strong> ${escapeHtml(feedback.feedbackText ?? feedback.FeedbackText ?? '(none)')}</p>
          <p><strong>Issue categories:</strong> ${escapeHtml((feedback.issueCategories ?? feedback.IssueCategories ?? []).join(', ') || '(none)')}</p>
          <p><strong>Suggested complexity:</strong> ${escapeHtml(feedback.suggestedComplexity ?? feedback.SuggestedComplexity ?? '(none)')}</p>
        </div>
        <div class="summary-grid">
          <article class="summary-card"><span class="summary-label">Correctness</span><strong class="summary-value">${escapeHtml(String(quality.correctness ?? quality.Correctness ?? ''))}</strong></article>
          <article class="summary-card"><span class="summary-label">Robustness</span><strong class="summary-value">${escapeHtml(String(quality.robustness ?? quality.Robustness ?? ''))}</strong></article>
          <article class="summary-card"><span class="summary-label">Code quality</span><strong class="summary-value">${escapeHtml(String(quality.codeQuality ?? quality.CodeQuality ?? ''))}</strong></article>
          <article class="summary-card"><span class="summary-label">Efficiency</span><strong class="summary-value">${escapeHtml(String(quality.efficiency ?? quality.Efficiency ?? ''))}</strong></article>
        </div>
        <div class="kv-block">
          <p><strong>Performance summary:</strong> ${escapeHtml(performance.summary ?? performance.Summary ?? '(none)')}</p>
          <p><strong>Time complexity:</strong> ${escapeHtml(performance.timeComplexity ?? performance.TimeComplexity ?? '(none)')}</p>
          <p><strong>Space complexity:</strong> ${escapeHtml(performance.spaceComplexity ?? performance.SpaceComplexity ?? '(none)')}</p>
          <p><strong>Performance notes:</strong> ${escapeHtml((performance.notes ?? performance.Notes ?? []).join(' | ') || '(none)')}</p>
        </div>
        <div class="kv-block">
          <p><strong>Error analysis:</strong> ${escapeHtml(formatErrorItems(errors) || '(none)')}</p>
          <p><strong>Improvement suggestions:</strong> ${escapeHtml(formatSuggestionItems(suggestions) || '(none)')}</p>
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

function formatErrorItems(items) {
  return (items ?? [])
    .map((item) => {
      const category = item.category ?? item.Category ?? '';
      const severity = item.severity ?? item.Severity ?? '';
      const title = item.title ?? item.Title ?? '';
      const detail = item.detail ?? item.Detail ?? '';
      const scenarios = (item.failingScenarios ?? item.FailingScenarios ?? []).join(', ');
      return [category && `[${category}]`, severity && `(${severity})`, title, detail, scenarios && `Scenarios: ${scenarios}`]
        .filter(Boolean)
        .join(' ');
    })
    .join(' | ');
}

function formatSuggestionItems(items) {
  return (items ?? [])
    .map((item) => {
      const priority = item.priority ?? item.Priority ?? '';
      const title = item.title ?? item.Title ?? '';
      const detail = item.detail ?? item.Detail ?? '';
      const impact = item.expectedImpact ?? item.ExpectedImpact ?? '';
      return [priority && `[${priority}]`, title, detail, impact && `Impact: ${impact}`]
        .filter(Boolean)
        .join(' ');
    })
    .join(' | ');
}

function normalizeSourceFiles(payload) {
  const files = Array.isArray(payload)
    ? payload
    : Array.isArray(payload?.sourceFiles)
      ? payload.sourceFiles
      : Array.isArray(payload?.SourceFiles)
        ? payload.SourceFiles
        : [];

  return files
    .map((file, index) => normalizeSourceFileItem(file, index))
    .filter(Boolean);
}

function normalizeSourceFileItem(file, index) {
  if (!file || typeof file !== 'object') {
    return null;
  }

  const fileName = file.fileName ?? file.FileName ?? file.filename ?? file.Filename ?? `file-${index + 1}.txt`;
  const content = file.content ?? file.Content ?? '';
  const isReadonly = file.isReadonly ?? file.IsReadonly ?? file.is_readonly ?? false;

  if (!String(content).trim()) {
    return null;
  }

  return {
    fileName: String(fileName),
    content: String(content),
    isReadonly: Boolean(isReadonly)
  };
}
