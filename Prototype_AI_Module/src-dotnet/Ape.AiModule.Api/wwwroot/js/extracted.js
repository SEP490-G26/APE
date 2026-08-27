import {
  escapeHtml,
  postJson,
  renderBadge,
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
      visionModel: form.visionModel.value || null,
      courseId: form.courseId.value.trim() || null,
      ownershipType: form.ownershipType.value.trim() || 'byos',
      sourceStoragePath: form.sourceStoragePath.value.trim() || null,
      sourceSizeBytes: Number(form.sourceSizeBytes.value || 0) || null
    };
  },
  renderResult(data) {
    const extracted = data?.extracted ?? data?.Extracted ?? {};
    const draft = data?.draft ?? data?.Draft ?? {};
    const contents = data?.contents ?? data?.Contents ?? [];
    const visionModel = extracted.visionModelName ?? extracted.VisionModelName ?? draft.visionModel ?? draft.VisionModel ?? '';
    return `
      <section class="result-section">
        <h4>Extraction summary</h4>
        <div class="summary-grid">
          <article class="summary-card"><span class="summary-label">Parser</span><strong class="summary-value">${escapeHtml(extracted.parserName ?? extracted.ParserName ?? '')}</strong></article>
          <article class="summary-card"><span class="summary-label">Vision model</span><strong class="summary-value">${escapeHtml(visionModel || '(none)')}</strong></article>
          <article class="summary-card"><span class="summary-label">Words</span><strong class="summary-value">${escapeHtml(String(extracted.wordCount ?? extracted.WordCount ?? 0))}</strong></article>
          <article class="summary-card"><span class="summary-label">Embedded images</span><strong class="summary-value">${escapeHtml(String(extracted.embeddedImageCount ?? extracted.EmbeddedImageCount ?? 0))}</strong></article>
        </div>
        ${renderMetricCards(data)}
        ${renderDraftSummary(draft)}
        ${renderTextBlocks(extracted)}
        ${renderDraftContents(contents)}
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
      form.sourceStoragePath.value = result.savedPath ?? '';
      form.sourceSizeBytes.value = String(result.sizeBytes ?? file.size ?? '');
      form.rawContent.value = result.suggestedRawContent ?? await readFileText(file);
    });

    document.getElementById('extracted-approve-draft')?.addEventListener('click', async () => {
      const latest = getLastExtractedResult();
      const draft = latest?.draft ?? latest?.Draft;
      if (!draft?.draftId && !draft?.DraftId) {
        return;
      }

      const draftId = draft.draftId ?? draft.DraftId;
      const userId = form.userId.value.trim();
      const approval = await postJson(`/api/ai-module/extraction-drafts/${draftId}/approve`, {
        userId,
        reviewerId: userId,
        approveAll: true,
        humanNotes: 'Approved from prototype UI.',
        segments: null
      });

      const merged = mergeApprovalIntoExtracted(latest, approval);
      persistExtractedResult(merged);
    });

    document.getElementById('extracted-load-to-embedding')?.addEventListener('click', async () => {
      const result = await ensureDraftApproved(form);
      const draft = result?.draft ?? result?.Draft;
      const draftId = draft?.draftId ?? draft?.DraftId;
      if (!draftId) {
        return;
      }

      const embeddingForm = document.getElementById('embedding-form');
      if (!embeddingForm) {
        return;
      }

      embeddingForm.extractionDraftId.value = draftId;
      embeddingForm.fileName.value = draft.sourceName ?? draft.SourceName ?? embeddingForm.fileName.value;
      embeddingForm.subject.value = form.subject.value;
      embeddingForm.language.value = form.language.value;
    });
  }
};

async function ensureDraftApproved(form) {
  const latest = getLastExtractedResult();
  const draft = latest?.draft ?? latest?.Draft;
  const draftId = draft?.draftId ?? draft?.DraftId;
  if (!draftId) {
    return null;
  }

  const reviewStatus = String(draft.reviewStatus ?? draft.ReviewStatus ?? '').toLowerCase();
  if (reviewStatus === 'approved' || reviewStatus === 'partially_approved' || reviewStatus === 'embedded') {
    return latest;
  }

  const userId = form.userId.value.trim();
  const approval = await postJson(`/api/ai-module/extraction-drafts/${draftId}/approve`, {
    userId,
    reviewerId: userId,
    approveAll: true,
    humanNotes: 'Auto-approved from prototype UI handoff.',
    segments: null
  });

  const merged = mergeApprovalIntoExtracted(latest, approval);
  persistExtractedResult(merged);
  return merged;
}

function getLastExtractedResult() {
  return window.ApeUi?.state?.lastResults?.extracted
    ?? window.ApeUi?.state?.lastResults?.lastApprovedExtractionDraft
    ?? null;
}

function mergeApprovalIntoExtracted(previous, approval) {
  if (!previous) {
    return approval;
  }

  const previousStageLogs = previous?.stageLogs ?? previous?.StageLogs ?? [];
  const approvalStageLogs = approval?.stageLogs ?? approval?.StageLogs ?? [];
  const previousUsageLogs = previous?.usageLogs ?? previous?.UsageLogs ?? [];
  const approvalUsageLogs = approval?.usageLogs ?? approval?.UsageLogs ?? [];
  const mergedStageLogs = [...previousStageLogs, ...approvalStageLogs];
  const mergedUsageLogs = [...previousUsageLogs, ...approvalUsageLogs];
  const previousTotals = previous?.totals ?? previous?.Totals ?? {};
  const approvalTotals = approval?.totals ?? approval?.Totals ?? {};
  const mergedTotals = mergeTotals(previousTotals, approvalTotals);

  return {
    ...previous,
    draft: approval?.draft ?? approval?.Draft ?? previous?.draft ?? previous?.Draft ?? {},
    Draft: approval?.Draft ?? approval?.draft ?? previous?.Draft ?? previous?.draft ?? {},
    contents: approval?.contents ?? approval?.Contents ?? previous?.contents ?? previous?.Contents ?? [],
    Contents: approval?.Contents ?? approval?.contents ?? previous?.Contents ?? previous?.contents ?? [],
    stageLogs: mergedStageLogs,
    StageLogs: mergedStageLogs,
    usageLogs: mergedUsageLogs,
    UsageLogs: mergedUsageLogs,
    totals: mergedTotals,
    Totals: mergedTotals
  };
}

function persistExtractedResult(result) {
  window.ApeUi.state.lastResults.extracted = result;
  window.ApeUi.state.lastResults.lastApprovedExtractionDraft = result;
  document.getElementById('extracted-json').textContent = JSON.stringify(result, null, 2);
  document.getElementById('extracted-result').innerHTML = extractedFeature.renderResult(result);
}

function mergeTotals(previousTotals, approvalTotals) {
  const prevInput = Number(previousTotals?.inputTokens ?? previousTotals?.InputTokens ?? 0);
  const prevOutput = Number(previousTotals?.outputTokens ?? previousTotals?.OutputTokens ?? 0);
  const prevInputCost = Number(previousTotals?.inputCostUsd ?? previousTotals?.InputCostUsd ?? 0);
  const prevOutputCost = Number(previousTotals?.outputCostUsd ?? previousTotals?.OutputCostUsd ?? 0);
  const prevTotalCost = Number(previousTotals?.totalCostUsd ?? previousTotals?.TotalCostUsd ?? 0);
  const prevLatency = Number(previousTotals?.latencyMs ?? previousTotals?.LatencyMs ?? 0);

  const approvalInput = Number(approvalTotals?.inputTokens ?? approvalTotals?.InputTokens ?? 0);
  const approvalOutput = Number(approvalTotals?.outputTokens ?? approvalTotals?.OutputTokens ?? 0);
  const approvalInputCost = Number(approvalTotals?.inputCostUsd ?? approvalTotals?.InputCostUsd ?? 0);
  const approvalOutputCost = Number(approvalTotals?.outputCostUsd ?? approvalTotals?.OutputCostUsd ?? 0);
  const approvalTotalCost = Number(approvalTotals?.totalCostUsd ?? approvalTotals?.TotalCostUsd ?? 0);
  const approvalLatency = Number(approvalTotals?.latencyMs ?? approvalTotals?.LatencyMs ?? 0);

  return {
    inputTokens: prevInput + approvalInput,
    outputTokens: prevOutput + approvalOutput,
    inputCostUsd: Number((prevInputCost + approvalInputCost).toFixed(6)),
    outputCostUsd: Number((prevOutputCost + approvalOutputCost).toFixed(6)),
    totalCostUsd: Number((prevTotalCost + approvalTotalCost).toFixed(6)),
    latencyMs: Number((prevLatency + approvalLatency).toFixed(2))
  };
}

function renderTextBlocks(extracted) {
  const rawText = extracted.rawText ?? extracted.RawText ?? '';
  const markdown = extracted.normalizedMarkdown ?? extracted.NormalizedMarkdown ?? '';
  const warnings = extracted.warnings ?? extracted.Warnings ?? [];
  const imageRefs = extracted.detectedImageReferences ?? extracted.DetectedImageReferences ?? [];
  const visionBlockCount = countVisionBlocks(markdown);

  return `
    <details class="details-block" open>
      <summary>Normalized markdown${visionBlockCount > 0 ? ` | OCR image blocks: ${escapeHtml(String(visionBlockCount))}` : ''}</summary>
      <div class="markdown-preview">${renderMarkdownPreview(markdown)}</div>
      <details class="details-block">
        <summary>Normalized markdown source</summary>
        <pre class="json-view">${escapeHtml(markdown)}</pre>
      </details>
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

function countVisionBlocks(markdown) {
  return String(markdown ?? '').match(/^### Vision Extracted From Image\b/gm)?.length ?? 0;
}

function renderMarkdownPreview(markdown) {
  const normalized = String(markdown ?? '').replace(/\r\n/g, '\n').trim();
  if (!normalized) {
    return '<p class="markdown-empty">(empty)</p>';
  }

  const blocks = normalized
    .split(/\n\s*\n/)
    .map((block) => block.trim())
    .filter(Boolean);

  return blocks.map(renderMarkdownBlock).join('');
}

function renderMarkdownBlock(block) {
  if (/^\[(image|img|figure):.+\]$/i.test(block)) {
    return `<div class="markdown-image-ref">${escapeHtml(block)}</div>`;
  }

  const heading = block.match(/^(#{1,6})\s+(.+)$/);
  if (heading) {
    const level = Math.min(heading[1].length, 6);
    const text = escapeHtml(heading[2]);
    return `<h${level} class="markdown-h${level}">${text}</h${level}>`;
  }

  return `<p>${escapeHtml(block).replace(/\n/g, '<br>')}</p>`;
}

function renderDraftSummary(draft) {
  if (!draft || typeof draft !== 'object' || Object.keys(draft).length === 0) {
    return '';
  }

  return `
    <div class="summary-grid">
      <article class="summary-card"><span class="summary-label">Draft ID</span><strong class="summary-value">${escapeHtml(draft.draftId ?? draft.DraftId ?? '')}</strong></article>
      <article class="summary-card"><span class="summary-label">Review status</span><strong class="summary-value">${renderBadge(draft.reviewStatus ?? draft.ReviewStatus ?? 'needs_review')}</strong></article>
      <article class="summary-card"><span class="summary-label">Segments</span><strong class="summary-value">${escapeHtml(String(draft.totalSegments ?? draft.TotalSegments ?? 0))}</strong></article>
      <article class="summary-card"><span class="summary-label">Approved</span><strong class="summary-value">${escapeHtml(String(draft.approvedSegments ?? draft.ApprovedSegments ?? 0))}</strong></article>
    </div>
  `;
}

function renderDraftContents(contents) {
  if (!Array.isArray(contents) || contents.length === 0) {
    return '';
  }

  return `
    <details class="details-block" open>
      <summary>Draft segments (${escapeHtml(String(contents.length))})</summary>
      <div class="card-stack">
        ${contents.map((item, index) => `
          <details class="details-block">
            <summary>Segment ${escapeHtml(String(item.segmentIndex ?? item.SegmentIndex ?? index + 1))} | ${escapeHtml(String(item.reviewStatus ?? item.ReviewStatus ?? ''))}</summary>
            <div class="kv-block">
              <p><strong>Content ID:</strong> ${escapeHtml(item.contentId ?? item.ContentId ?? '')}</p>
              <p><strong>Pages:</strong> ${escapeHtml(String(item.sourcePageFrom ?? item.SourcePageFrom ?? ''))}-${escapeHtml(String(item.sourcePageTo ?? item.SourcePageTo ?? ''))}</p>
              <p><strong>Status:</strong> ${renderBadge(item.reviewStatus ?? item.ReviewStatus ?? 'needs_review')}</p>
            </div>
            <pre class="json-view">${escapeHtml(item.approvedMarkdown ?? item.ApprovedMarkdown ?? item.cleanMarkdown ?? item.CleanMarkdown ?? '')}</pre>
          </details>
        `).join('')}
      </div>
    </details>
  `;
}
