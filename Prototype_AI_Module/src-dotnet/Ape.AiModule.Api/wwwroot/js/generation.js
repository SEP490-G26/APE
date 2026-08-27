import {
  state,
  escapeHtml,
  parseJsonInput,
  exportJsonFile,
  postJson,
  renderJsonDetails,
  renderMetricCards,
  renderStageLogs,
  renderUsageLogs,
  readFileText,
  renderTextBlock
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
    const strategy = form.inputStrategy.value;
    const chunks = resolveChunks(form);
    const contextPackId = form.contextPackId.value.trim() || null;
    const retrieval = strategy === 'retrieval' ? buildRetrievalPayload(form) : null;

    if (strategy === 'chunks' && !chunks.length) {
      throw new Error('Generation + Review requires at least one chunk when input strategy is Chunks JSON.');
    }

    if (strategy === 'contextPack' && !contextPackId) {
      throw new Error('Context Pack ID is required when input strategy is Context Pack ID.');
    }

    if (strategy === 'retrieval' && !retrieval) {
      throw new Error('Retrieval request could not be built. Check target topics or retrieval query.');
    }

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
      chunks: strategy === 'chunks' ? chunks : [],
      contextPackId: strategy === 'contextPack' ? contextPackId : null,
      retrieval
    };
  },
  renderResult(data) {
    const isRetrievalPlan = Boolean(data?.contextPack && data?.selectedChunks && !data?.questions && !data?.Questions);
    if (isRetrievalPlan) {
      return renderRetrievalPlan(data);
    }

    const questions = data?.questions ?? data?.Questions ?? [];
    const review = data?.review ?? data?.Review ?? null;
    const mapping = data?.schemaMapping ?? data?.SchemaMapping ?? null;
    const difficulty = data?.difficultyAlignment ?? data?.DifficultyAlignment ?? null;
    const contextPack = data?.contextPack ?? data?.ContextPack ?? null;
    const retrievalPlan = data?.retrievalPlan ?? data?.RetrievalPlan ?? null;
    const inputSource = detectInputSource(data);
    return `
      <section class="result-section">
        <h4>Generation + review result</h4>
        <div class="summary-grid">
          <article class="summary-card"><span class="summary-label">Subject</span><strong class="summary-value">${escapeHtml(data?.subject ?? data?.Subject ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Questions</span><strong class="summary-value">${escapeHtml(String(questions.length))}</strong></article>
          <article class="summary-card"><span class="summary-label">Review status</span><strong class="summary-value">${review ? escapeHtml(review.reviewStatus ?? review.ReviewStatus ?? '') : 'n/a'}</strong></article>
          <article class="summary-card"><span class="summary-label">Attempts</span><strong class="summary-value">${escapeHtml(String(data?.attemptsUsed ?? data?.AttemptsUsed ?? 0))}</strong></article>
          <article class="summary-card"><span class="summary-label">Input source</span><strong class="summary-value">${escapeHtml(inputSource)}</strong></article>
        </div>
        ${renderMetricCards(data)}
        ${renderContextPackBlock(contextPack, retrievalPlan)}
        ${retrievalPlan ? renderSelectedChunks(retrievalPlan.selectedChunks ?? retrievalPlan.SelectedChunks ?? []) : ''}
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
        document.getElementById('generation-input-strategy').value = 'chunks';
        syncInputStrategy();
      });
    }

    document.getElementById('generation-mode')?.addEventListener('change', syncReviewerState);
    document.getElementById('generation-generator-model')?.addEventListener('change', syncReviewerState);
    document.getElementById('generation-input-strategy')?.addEventListener('change', syncInputStrategy);
    syncReviewerState();
    syncInputStrategy();

    document.getElementById('generation-use-last-embedding')?.addEventListener('click', () => {
      const form = document.getElementById('generation-form');
      if (!form) {
        return;
      }

      const chunks = normalizeChunks(state.lastResults.embedding ?? state.lastResults.extracted ?? state.lastResults.generationPlan ?? null);
      form.chunks.value = JSON.stringify(chunks, null, 2);
      form.inputStrategy.value = 'chunks';
      syncInputStrategy();
    });

    document.getElementById('generation-use-last-pack')?.addEventListener('click', () => {
      const form = document.getElementById('generation-form');
      if (!form) {
        return;
      }

      const packId = state.lastResults.generationPlan?.contextPack?.packId
        ?? state.lastResults.generationPlan?.ContextPack?.PackId
        ?? state.lastResults.generation?.contextPack?.packId
        ?? state.lastResults.generation?.ContextPack?.PackId
        ?? '';

      if (!packId) {
        const note = document.getElementById('generation-flow-note');
        if (note) {
          note.textContent = 'Chưa có context pack trong session hiện tại. Hãy bấm Plan Retrieval Pack trước.';
        }
        return;
      }

      form.contextPackId.value = packId;
      form.inputStrategy.value = 'contextPack';
      syncInputStrategy();
    });

    document.getElementById('generation-plan-retrieval')?.addEventListener('click', async () => {
      const form = document.getElementById('generation-form');
      if (!form) {
        return;
      }

      const payload = buildRetrievalPayload(form);
      const result = await postJson('/api/ai-module/retrieval/plan', payload);
      state.lastResults.generation = result;
      state.lastResults.generationPlan = result;
      if (result?.contextPack?.packId || result?.ContextPack?.PackId) {
        form.contextPackId.value = result?.contextPack?.packId ?? result?.ContextPack?.PackId ?? '';
        form.inputStrategy.value = 'contextPack';
        syncInputStrategy();
      }
      setGenerationResult(result);
    });

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

function setGenerationResult(result) {
  state.lastResults.generation = result;
  const jsonId = generationFeature.jsonId;
  const resultId = generationFeature.resultId;
  document.getElementById(jsonId).textContent = JSON.stringify(result, null, 2);
  document.getElementById(resultId).innerHTML = generationFeature.renderResult(result);
}

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

function syncInputStrategy() {
  const form = document.getElementById('generation-form');
  if (!form) {
    return;
  }

  const strategy = form.inputStrategy.value;
  const isChunks = strategy === 'chunks';
  const isContextPack = strategy === 'contextPack';
  const isRetrieval = strategy === 'retrieval';

  form.contextPackId.disabled = !isContextPack;
  form.targetTopics.disabled = !isRetrieval;
  form.sourceScope.disabled = !isRetrieval;
  form.useCachedPack.disabled = !isRetrieval;
  form.forceRebuildPack.disabled = !isRetrieval;
  form.maxCandidateCount.disabled = !isRetrieval;
  form.maxPackedTokens.disabled = !isRetrieval;
  form.preferredChapterIds.disabled = !isRetrieval;
  form.retrievalQuery.disabled = !isRetrieval;
  form.chunks.disabled = !isChunks;
  document.getElementById('generation-import').disabled = !isChunks;
  const note = document.getElementById('generation-flow-note');
  if (note) {
    note.textContent = isChunks
      ? 'Chunks JSON: dùng khi bạn muốn benchmark trực tiếp từ dữ liệu embedding/tagging hoặc import file chunk JSON.'
      : isContextPack
        ? 'Context Pack ID: dùng lại pack đã plan trước đó để cost gần production hơn và tránh re-pack không cần thiết.'
        : 'Retrieval Planner: nhận target topics + retrieval query, tự chọn chunk phù hợp rồi build context pack trước khi generation/review chạy.';
  }
}

function resolveChunks(form) {
  const textareaValue = form.chunks.value;
  const importedPayload = textareaValue.trim() ? parseJsonInput(textareaValue, []) : null;
  const fallbackPayload = importedPayload ?? state.lastResults.embedding ?? state.lastResults.extracted ?? null;
  return normalizeChunks(fallbackPayload);
}

function buildRetrievalPayload(form) {
  const topics = splitCsv(form.targetTopics.value);
  const preferredChapterIds = splitCsv(form.preferredChapterIds.value);
  const chunks = resolveChunks(form);

  return {
    userId: form.userId.value.trim(),
    courseId: form.courseId.value.trim() || null,
    documentId: form.documentId.value.trim() || null,
    subject: form.subject.value,
    questionType: form.questionType.value,
    difficulty: form.difficulty.value,
    requestedQuestionCount: Number(form.count.value || 1),
    generationMode: inferGenerationMode(form.count.value),
    targetTopics: topics,
    preferredChapterIds,
    sourceScope: form.sourceScope.value,
    language: 'en',
    useCachedPack: form.useCachedPack.value === 'true',
    forceRebuildPack: form.forceRebuildPack.value === 'true',
    includeChunkText: false,
    maxCandidateCount: Number(form.maxCandidateCount.value || 40),
    maxPackedTokens: Number(form.maxPackedTokens.value || 7000),
    retrievalQuery: form.retrievalQuery.value.trim() || buildDefaultRetrievalQuery(form, topics),
    seedChunks: chunks.length ? chunks : null
  };
}

function detectInputSource(data) {
  if (data?.retrievalPlan || data?.RetrievalPlan) {
    return 'retrieval-derived context pack';
  }

  if (data?.contextPack || data?.ContextPack) {
    return 'context pack';
  }

  return 'chunks json';
}

function buildDefaultRetrievalQuery(form, topics) {
  const topicText = topics.length ? topics.join(', ') : 'core lesson concepts';
  return `Create ${form.count.value || 1} ${form.difficulty.value} ${form.questionType.value} question(s) about ${topicText} in ${form.subject.value}`;
}

function inferGenerationMode(countValue) {
  const count = Number(countValue || 1);
  if (count <= 1) {
    return 'single_question_precise';
  }

  if (count <= 5) {
    return 'small_batch';
  }

  return 'coverage_exam';
}

function splitCsv(value) {
  return String(value ?? '')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}

function normalizeChunks(payload) {
  const rawChunks = Array.isArray(payload)
    ? payload
    : Array.isArray(payload?.chunks)
      ? payload.chunks
      : Array.isArray(payload?.Chunks)
        ? payload.Chunks
        : Array.isArray(payload?.document?.chunks)
          ? payload.document.chunks
          : Array.isArray(payload?.Document?.Chunks)
            ? payload.Document.Chunks
            : Array.isArray(payload?.contextPack?.chunks)
              ? payload.contextPack.chunks
              : Array.isArray(payload?.ContextPack?.Chunks)
                ? payload.ContextPack.Chunks
                : [];

  return rawChunks
    .map((chunk, index) => normalizeChunkItem(chunk, index))
    .filter(Boolean);
}

function normalizeChunkItem(chunk, index) {
  if (!chunk || typeof chunk !== 'object') {
    return null;
  }

  const chunkId = chunk.chunkId ?? chunk.ChunkId ?? chunk.id ?? chunk.Id ?? `chunk-${index + 1}`;
  const contentText = chunk.contentText
    ?? chunk.ContentText
    ?? chunk.normalizedText
    ?? chunk.NormalizedText
    ?? chunk.markdownText
    ?? chunk.MarkdownText
    ?? chunk.rawText
    ?? chunk.RawText
    ?? '';
  const topicTags = Array.isArray(chunk.topicTags)
    ? chunk.topicTags
    : Array.isArray(chunk.TopicTags)
      ? chunk.TopicTags
      : Array.isArray(chunk.topic_tags)
        ? chunk.topic_tags
        : [];
  const sectionTitle = chunk.sectionTitle
    ?? chunk.SectionTitle
    ?? chunk.section_title
    ?? chunk.chapterTitle
    ?? chunk.ChapterTitle
    ?? chunk.chunkType
    ?? chunk.ChunkType
    ?? chunk.chunk_type
    ?? null;
  const language = chunk.language ?? chunk.Language ?? 'en';

  if (!String(contentText).trim()) {
    return null;
  }

  return {
    chunkId: String(chunkId),
    contentText: String(contentText),
    topicTags: topicTags.map((item) => String(item)).filter(Boolean),
    sectionTitle: sectionTitle ? String(sectionTitle) : null,
    language: String(language)
  };
}

function renderRetrievalPlan(data) {
  const contextPack = data?.contextPack ?? data?.ContextPack ?? null;
  const selectedChunks = data?.selectedChunks ?? data?.SelectedChunks ?? [];
  const tokenBudget = data?.tokenBudget ?? data?.TokenBudget ?? null;
  const candidateStats = data?.candidateStats ?? data?.CandidateStats ?? null;
  return `
    <section class="result-section">
      <h4>Retrieval plan</h4>
      <div class="summary-grid">
        <article class="summary-card"><span class="summary-label">Pack ID</span><strong class="summary-value">${escapeHtml(contextPack?.packId ?? contextPack?.PackId ?? '(none)')}</strong></article>
        <article class="summary-card"><span class="summary-label">Selected chunks</span><strong class="summary-value">${escapeHtml(String(selectedChunks.length))}</strong></article>
        <article class="summary-card"><span class="summary-label">Packed tokens</span><strong class="summary-value">${escapeHtml(String(tokenBudget?.estimatedPackedTokens ?? tokenBudget?.EstimatedPackedTokens ?? 0))}</strong></article>
        <article class="summary-card"><span class="summary-label">Compression ratio</span><strong class="summary-value">${escapeHtml(String(tokenBudget?.compressionRatio ?? tokenBudget?.CompressionRatio ?? 0))}</strong></article>
      </div>
      ${renderMetricCards(data)}
      ${candidateStats ? renderJsonDetails('Candidate stats', candidateStats, true) : ''}
      ${renderContextPackBlock(contextPack, data)}
      ${renderSelectedChunks(selectedChunks)}
      ${renderStageLogs(data)}
      ${renderUsageLogs(data)}
    </section>
  `;
}

function renderContextPackBlock(contextPack, retrievalPlan) {
  if (!contextPack) {
    return '';
  }

  const planBudget = retrievalPlan?.tokenBudget ?? retrievalPlan?.TokenBudget ?? null;
  return `
    <details class="details-block" open>
      <summary>Context pack</summary>
      <div class="kv-block">
        <p><strong>Pack ID:</strong> ${escapeHtml(contextPack.packId ?? contextPack.PackId ?? '')}</p>
        <p><strong>Strategy:</strong> ${escapeHtml(contextPack.packStrategy ?? contextPack.PackStrategy ?? '')}</p>
        <p><strong>Status:</strong> ${escapeHtml(contextPack.packStatus ?? contextPack.PackStatus ?? '')}</p>
        <p><strong>Target topics:</strong> ${escapeHtml(((contextPack.targetTopics ?? contextPack.TargetTopics ?? [])).join(', ') || '(none)')}</p>
        <p><strong>Recommended question count:</strong> ${escapeHtml(String(contextPack.recommendedQuestionCount ?? contextPack.RecommendedQuestionCount ?? 0))}</p>
        <p><strong>Source token count:</strong> ${escapeHtml(String(contextPack.sourceTokenCount ?? contextPack.SourceTokenCount ?? 0))}</p>
        <p><strong>Packed token count:</strong> ${escapeHtml(String(contextPack.tokenCount ?? contextPack.TokenCount ?? 0))}</p>
        <p><strong>Compression ratio:</strong> ${escapeHtml(String(planBudget?.compressionRatio ?? planBudget?.CompressionRatio ?? contextPack.compressionRatio ?? contextPack.CompressionRatio ?? 0))}</p>
      </div>
      ${renderTextBlock('Packed summary text', contextPack.packedSummaryText ?? contextPack.PackedSummaryText ?? '', true)}
      ${renderTextBlock('Packed context text', contextPack.packedContextText ?? contextPack.PackedContextText ?? '', false)}
    </details>
  `;
}

function renderSelectedChunks(selectedChunks) {
  if (!Array.isArray(selectedChunks) || selectedChunks.length === 0) {
    return '';
  }

  const rows = selectedChunks.map((chunk) => `
    <tr>
      <td>${escapeHtml(chunk.chunkId ?? chunk.ChunkId ?? '')}</td>
      <td>${escapeHtml(chunk.selectionRole ?? chunk.SelectionRole ?? '')}</td>
      <td>${escapeHtml(chunk.topicPrimary ?? chunk.TopicPrimary ?? '')}</td>
      <td>${escapeHtml((chunk.topicTags ?? chunk.TopicTags ?? []).join(', '))}</td>
      <td>${escapeHtml(String(chunk.tokenCount ?? chunk.TokenCount ?? 0))}</td>
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
              <th>Topic tags</th>
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
