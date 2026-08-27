import {
  escapeHtml,
  parseJsonInput,
  exportJsonFile,
  postJson,
  renderJsonDetails,
  renderMetricCards,
  renderStageLogs,
  renderUsageLogs,
  readFileText
} from './shared/core.js';

export const generationFeature = {
  key: 'generation',
  historyKey: 'generation-review',
  formId: 'generation-form',
  endpoint: '/api/ai-module/generation-review',
  debugEndpoint: null,
  resultId: 'generation-result',
  jsonId: 'generation-json',
  buildPayload(form) {
    const mode = form.mode.value;
    const generatorModel = form.generatorModel.value;
    const reviewerModel = mode === 'SameModelDualRole' ? generatorModel : form.reviewerModel.value;
    return {
      userId: form.userId.value.trim(),
      courseId: form.courseId.value.trim() || null,
      documentId: form.documentId.value.trim() || null,
      isPublic: form.isPublic.value === 'true',
      subject: form.subject.value,
      difficulty: form.difficulty.value,
      questionType: form.questionType.value,
      count: Number(form.count.value || 1),
      mode,
      generatorModel,
      reviewerModel,
      maxAttempts: Number(form.maxAttempts.value || 1),
      chunks: normalizeChunks(parseJsonInput(form.chunks.value, []))
    };
  },
  renderResult(data) {
    const questions = data?.questions ?? data?.Questions ?? [];
    const review = data?.review ?? data?.Review ?? null;
    const mapping = data?.schemaMapping ?? data?.SchemaMapping ?? null;
    const difficulty = data?.difficultyAlignment ?? data?.DifficultyAlignment ?? null;
    return `
      <section class="result-section">
        <h4>Generation + review result</h4>
        <div class="summary-grid">
          <article class="summary-card"><span class="summary-label">Subject</span><strong class="summary-value">${escapeHtml(data?.subject ?? data?.Subject ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Questions</span><strong class="summary-value">${escapeHtml(String(questions.length))}</strong></article>
          <article class="summary-card"><span class="summary-label">Review status</span><strong class="summary-value">${review ? escapeHtml(review.reviewStatus ?? review.ReviewStatus ?? '') : 'n/a'}</strong></article>
          <article class="summary-card"><span class="summary-label">Attempts</span><strong class="summary-value">${escapeHtml(String(data?.attemptsUsed ?? data?.AttemptsUsed ?? 0))}</strong></article>
        </div>
        ${renderMetricCards(data)}
        ${renderQuestionBlocks(questions)}
        ${renderReviewBlock(review, mapping, difficulty)}
        ${renderStageLogs(data)}
        ${renderUsageLogs(data)}
      </section>
    `;
  },
  init() {
    const importInput = document.getElementById('generation-import');
    const chunksArea = document.querySelector('#generation-form textarea[name="chunks"]');
    if (importInput && chunksArea) {
      importInput.addEventListener('change', async () => {
        const file = importInput.files?.[0];
        if (!file) {
          return;
        }

        const text = await readFileText(file);
        const parsed = parseJsonInput(text, []);
        chunksArea.value = JSON.stringify(normalizeChunks(parsed), null, 2);
      });
    }

    document.getElementById('generation-mode')?.addEventListener('change', syncReviewerState);
    document.getElementById('generation-generator-model')?.addEventListener('change', syncReviewerState);
    syncReviewerState();

    document.querySelector('[data-run-export="generation"]')?.addEventListener('click', async () => {
      const form = document.getElementById('generation-form');
      if (!form) {
        return;
      }

      const payload = generationFeature.buildPayload(form);
      const result = await postJson('/api/ai-module/generation-review/export', payload);
      exportJsonFile('generation-review-package', result);
    });
  }
};

function syncReviewerState() {
  const form = document.getElementById('generation-form');
  if (!form) {
    return;
  }

  const sameModel = form.mode.value === 'SameModelDualRole';
  form.reviewerModel.disabled = sameModel;
  if (sameModel) {
    form.reviewerModel.value = form.generatorModel.value;
  }
}

function normalizeChunks(payload) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (Array.isArray(payload?.chunks)) {
    return payload.chunks;
  }

  if (Array.isArray(payload?.Chunks)) {
    return payload.Chunks;
  }

  if (Array.isArray(payload?.document?.chunks)) {
    return payload.document.chunks;
  }

  if (Array.isArray(payload?.Document?.Chunks)) {
    return payload.Document.Chunks;
  }

  return [];
}

function renderQuestionBlocks(questions) {
  if (!Array.isArray(questions) || questions.length === 0) {
    return '<div class="placeholder">No question returned.</div>';
  }

  return questions.map((question, index) => `
    <details class="details-block" open>
      <summary>Question ${index + 1} | ${escapeHtml(question.type ?? question.Type ?? '')} | ${escapeHtml(question.difficulty ?? question.Difficulty ?? '')}</summary>
      <div class="kv-block">
        <p><strong>Title:</strong> ${escapeHtml(question.title ?? question.Title ?? '')}</p>
        <p><strong>Topic tags:</strong> ${escapeHtml((question.topicTags ?? question.TopicTags ?? []).join(', ') || '(none)')}</p>
        <p><strong>Source chunk IDs:</strong> ${escapeHtml((question.sourceChunkIds ?? question.SourceChunkIds ?? []).join(', ') || '(none)')}</p>
      </div>
      <pre class="json-view">${escapeHtml(question.description ?? question.Description ?? '')}</pre>
      ${renderQuestionExtras(question)}
    </details>
  `).join('');
}

function renderQuestionExtras(question) {
  const options = question.options ?? question.Options ?? null;
  const answers = question.correctAnswer ?? question.CorrectAnswer ?? null;
  const explanation = question.explanation ?? question.Explanation ?? null;
  const skeleton = question.skeletonCode ?? question.SkeletonCode ?? null;
  const solution = question.solutionCode ?? question.SolutionCode ?? null;
  const testCases = question.testCases ?? question.TestCases ?? null;

  return `
    ${options ? renderJsonDetails('Options', options, true) : ''}
    ${answers ? renderJsonDetails('Correct answer', answers, true) : ''}
    ${explanation ? `<div class="kv-block"><p><strong>Explanation:</strong> ${escapeHtml(explanation)}</p></div>` : ''}
    ${skeleton ? renderJsonDetails('Skeleton code', skeleton) : ''}
    ${solution ? renderJsonDetails('Solution code', solution) : ''}
    ${testCases ? renderJsonDetails('Test cases', testCases) : ''}
  `;
}

function renderReviewBlock(review, mapping, difficulty) {
  return `
    ${review ? `
      <details class="details-block" open>
        <summary>Reviewer decision</summary>
        <div class="kv-block">
          <p><strong>Status:</strong> ${escapeHtml(review.reviewStatus ?? review.ReviewStatus ?? '')}</p>
          <p><strong>Schema valid:</strong> ${escapeHtml(String(review.schemaValid ?? review.SchemaValid ?? false))}</p>
          <p><strong>Content grounded:</strong> ${escapeHtml(String(review.contentGrounded ?? review.ContentGrounded ?? false))}</p>
          <p><strong>Needs revision:</strong> ${escapeHtml(String(review.needsRevision ?? review.NeedsRevision ?? false))}</p>
          <p><strong>Score:</strong> ${escapeHtml(String(review.score ?? review.Score ?? ''))}</p>
          <p><strong>Issues:</strong> ${escapeHtml((review.issues ?? review.Issues ?? []).join(' | ') || '(none)')}</p>
          <p><strong>Suggestions:</strong> ${escapeHtml((review.suggestions ?? review.Suggestions ?? []).join(' | ') || '(none)')}</p>
        </div>
      </details>
    ` : ''}
    ${mapping ? renderJsonDetails('Schema mapping', mapping) : ''}
    ${difficulty ? renderJsonDetails('Difficulty alignment', difficulty) : ''}
  `;
}
