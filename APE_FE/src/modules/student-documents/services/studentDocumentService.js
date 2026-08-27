import { getApiBaseUrl } from "../../../lib/env";
import { getAccessToken } from "../../../lib/storage";
import { http } from "../../../services/http";

const STUDENT_DOCUMENTS_BASE = "/api/student/documents";
const STUDENT_COURSES_BASE = "/api/student/courses";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

function normalizeCourse(course) {
  return {
    id: course?.id ?? course?.Id ?? "",
    code: course?.code ?? course?.Code ?? "",
    name: course?.name ?? course?.Name ?? "Untitled course",
    description: course?.description ?? course?.Description ?? "",
    status: course?.status ?? course?.Status ?? ""
  };
}

function normalizeDocument(document) {
  return {
    id: document?.id ?? document?.Id ?? document?.documentId ?? document?.DocumentId ?? "",
    documentId: document?.documentId ?? document?.DocumentId ?? document?.id ?? document?.Id ?? "",
    courseId: document?.courseId ?? document?.CourseId ?? "",
    fileName: document?.fileName ?? document?.FileName ?? "",
    fileType: document?.fileType ?? document?.FileType ?? "",
    source: document?.source ?? document?.Source ?? "",
    subjectCode: document?.subjectCode ?? document?.SubjectCode ?? "",
    gatekeeperVerdict: document?.gatekeeperVerdict ?? document?.GatekeeperVerdict ?? "",
    ingestionStatus: document?.ingestionStatus ?? document?.IngestionStatus ?? document?.status ?? document?.Status ?? "",
    ingestionStage: document?.ingestionStage ?? document?.IngestionStage ?? "processing",
    isActive: Boolean(document?.isActive ?? document?.IsActive),
    hasExtractionDraft: Boolean(document?.hasExtractionDraft ?? document?.HasExtractionDraft),
    hasEmbeddedChunks: Boolean(document?.hasEmbeddedChunks ?? document?.HasEmbeddedChunks),
    isReadyForGeneration: Boolean(document?.isReadyForGeneration ?? document?.IsReadyForGeneration),
    hasTopics: Boolean(document?.hasTopics ?? document?.HasTopics),
    chunkCount: Number(document?.chunkCount ?? document?.ChunkCount ?? 0),
    tagCount: Number(document?.tagCount ?? document?.TagCount ?? 0),
    createdAt: document?.createdAt ?? document?.CreatedAt ?? null,
    lastExtractedAt: document?.lastExtractedAt ?? document?.LastExtractedAt ?? null,
    lastEmbeddedAt: document?.lastEmbeddedAt ?? document?.LastEmbeddedAt ?? null,
    lastExtractionDraftId: document?.lastExtractionDraftId ?? document?.LastExtractionDraftId ?? null,
    duplicateDetected: Boolean(document?.duplicateDetected ?? document?.DuplicateDetected),
    duplicateOfDocumentId: document?.duplicateOfDocumentId ?? document?.DuplicateOfDocumentId ?? null,
    duplicateMatchType: document?.duplicateMatchType ?? document?.DuplicateMatchType ?? null,
    previewText: document?.previewText ?? document?.PreviewText ?? "",
    cleanupWarnings: document?.cleanupWarnings ?? document?.CleanupWarnings ?? [],
    tags: document?.tags ?? document?.Tags ?? [],
    usdToVndRate: Number(document?.usdToVndRate ?? document?.UsdToVndRate ?? 0),
    chargedVnd: Number(document?.chargedVnd ?? document?.ChargedVnd ?? 0),
    actualDeductedVnd: Number(document?.actualDeductedVnd ?? document?.ActualDeductedVnd ?? 0),
    absorbedVnd: Number(document?.absorbedVnd ?? document?.AbsorbedVnd ?? 0),
    remainingBalanceVnd: Number(document?.remainingBalanceVnd ?? document?.RemainingBalanceVnd ?? 0)
  };
}

function normalizePreviewDocument(document) {
  return {
    ...normalizeDocument(document),
    documentId: document?.documentId ?? document?.DocumentId ?? document?.id ?? document?.Id ?? "",
    createdAt: document?.createdAt ?? document?.CreatedAt ?? null,
    lastExtractedAt: document?.lastExtractedAt ?? document?.LastExtractedAt ?? null,
    lastEmbeddedAt: document?.lastEmbeddedAt ?? document?.LastEmbeddedAt ?? null,
    tags: Array.isArray(document?.tags ?? document?.Tags) ? (document.tags ?? document.Tags) : [],
    cleanupWarnings: Array.isArray(document?.cleanupWarnings ?? document?.CleanupWarnings)
      ? (document.cleanupWarnings ?? document.CleanupWarnings)
      : []
  };
}

function normalizeExtractionDraft(draft) {
  return {
    draftId: draft?.draftId ?? draft?.DraftId ?? "",
    documentId: draft?.documentId ?? draft?.DocumentId ?? "",
    courseId: draft?.courseId ?? draft?.CourseId ?? "",
    sourceType: draft?.sourceType ?? draft?.SourceType ?? "",
    sourceName: draft?.sourceName ?? draft?.SourceName ?? "",
    language: draft?.language ?? draft?.Language ?? "en",
    subjectCode: draft?.subjectCode ?? draft?.SubjectCode ?? "",
    ingestParser: draft?.ingestParser ?? draft?.IngestParser ?? "",
    extractionMode: draft?.extractionMode ?? draft?.ExtractionMode ?? "",
    visionModel: draft?.visionModel ?? draft?.VisionModel ?? "",
    totalPages: Number(draft?.totalPages ?? draft?.TotalPages ?? 0),
    totalSegments: Number(draft?.totalSegments ?? draft?.TotalSegments ?? 0),
    detectedImagePlaceholderCount: Number(draft?.detectedImagePlaceholderCount ?? draft?.DetectedImagePlaceholderCount ?? 0),
    embeddedImageCount: Number(draft?.embeddedImageCount ?? draft?.EmbeddedImageCount ?? 0),
    visionEnrichmentAttemptedCount: Number(draft?.visionEnrichmentAttemptedCount ?? draft?.VisionEnrichmentAttemptedCount ?? 0),
    visionEnrichmentSucceededCount: Number(draft?.visionEnrichmentSucceededCount ?? draft?.VisionEnrichmentSucceededCount ?? 0),
    visionEnrichmentFailedCount: Number(draft?.visionEnrichmentFailedCount ?? draft?.VisionEnrichmentFailedCount ?? 0),
    unresolvedImagePlaceholderCount: Number(draft?.unresolvedImagePlaceholderCount ?? draft?.UnresolvedImagePlaceholderCount ?? 0),
    candidateTitles: Array.isArray(draft?.candidateTitles ?? draft?.CandidateTitles) ? (draft.candidateTitles ?? draft.CandidateTitles) : [],
    candidateChapterMarkers: Array.isArray(draft?.candidateChapterMarkers ?? draft?.CandidateChapterMarkers) ? (draft.candidateChapterMarkers ?? draft.CandidateChapterMarkers) : [],
    rejectedHeadingCandidates: Array.isArray(draft?.rejectedHeadingCandidates ?? draft?.RejectedHeadingCandidates) ? (draft.rejectedHeadingCandidates ?? draft.RejectedHeadingCandidates) : [],
    cleanDisplayTitleCandidates: Array.isArray(draft?.cleanDisplayTitleCandidates ?? draft?.CleanDisplayTitleCandidates) ? (draft.cleanDisplayTitleCandidates ?? draft.CleanDisplayTitleCandidates) : [],
    reviewStatus: draft?.reviewStatus ?? draft?.ReviewStatus ?? "",
    ingestionStage: draft?.ingestionStage ?? draft?.IngestionStage ?? "",
    cleanMarkdownPreview: draft?.cleanMarkdownPreview ?? draft?.CleanMarkdownPreview ?? "",
    cleanupWarnings: Array.isArray(draft?.cleanupWarnings ?? draft?.CleanupWarnings) ? (draft.cleanupWarnings ?? draft.CleanupWarnings) : [],
    approvedSegments: Number(draft?.approvedSegments ?? draft?.ApprovedSegments ?? 0),
    rejectedSegments: Number(draft?.rejectedSegments ?? draft?.RejectedSegments ?? 0),
    chunkingReady: Boolean(draft?.chunkingReady ?? draft?.ChunkingReady),
    hasEmbeddedChunks: Boolean(draft?.hasEmbeddedChunks ?? draft?.HasEmbeddedChunks),
    isReadyForGeneration: Boolean(draft?.isReadyForGeneration ?? draft?.IsReadyForGeneration),
    chunkCount: Number(draft?.chunkCount ?? draft?.ChunkCount ?? 0),
    lastEmbeddingRunId: draft?.lastEmbeddingRunId ?? draft?.LastEmbeddingRunId ?? "",
    createdAt: draft?.createdAt ?? draft?.CreatedAt ?? null,
    updatedAt: draft?.updatedAt ?? draft?.UpdatedAt ?? null
  };
}

function normalizeTopicsPayload(payload) {
  return {
    ...payload,
    scope: payload?.scope ?? payload?.Scope ?? "",
    courseId: payload?.courseId ?? payload?.CourseId ?? null,
    documentId: payload?.documentId ?? payload?.DocumentId ?? null,
    documentName: payload?.documentName ?? payload?.DocumentName ?? null,
    chapterKey: payload?.chapterKey ?? payload?.ChapterKey ?? null,
    chapterKeys: payload?.chapterKeys ?? payload?.ChapterKeys ?? [],
    chapterTitle: payload?.chapterTitle ?? payload?.ChapterTitle ?? null,
    subject: payload?.subject ?? payload?.Subject ?? "",
    totalChunks: Number(payload?.totalChunks ?? payload?.TotalChunks ?? 0),
    distinctTopicCount: Number(payload?.distinctTopicCount ?? payload?.DistinctTopicCount ?? 0),
    topics: Array.isArray(payload?.topics ?? payload?.Topics)
      ? (payload.topics ?? payload.Topics).map((topic) => ({
          tag: topic?.tag ?? topic?.Tag ?? "",
          chunkCount: Number(topic?.chunkCount ?? topic?.ChunkCount ?? 0),
          documentCount: Number(topic?.documentCount ?? topic?.DocumentCount ?? 0),
          estimatedTokens: Number(topic?.estimatedTokens ?? topic?.EstimatedTokens ?? 0),
          sampleSectionTitles: topic?.sampleSectionTitles ?? topic?.SampleSectionTitles ?? [],
          sourceChapterKeys: topic?.sourceChapterKeys ?? topic?.SourceChapterKeys ?? []
        }))
      : []
  };
}

function normalizeChaptersPayload(payload) {
  return {
    ...payload,
    scope: payload?.scope ?? payload?.Scope ?? "",
    courseId: payload?.courseId ?? payload?.CourseId ?? null,
    documentId: payload?.documentId ?? payload?.DocumentId ?? null,
    documentName: payload?.documentName ?? payload?.DocumentName ?? null,
    subject: payload?.subject ?? payload?.Subject ?? "",
    totalChunks: Number(payload?.totalChunks ?? payload?.TotalChunks ?? 0),
    distinctChapterCount: Number(payload?.distinctChapterCount ?? payload?.DistinctChapterCount ?? 0),
    chapters: Array.isArray(payload?.chapters ?? payload?.Chapters)
      ? (payload.chapters ?? payload.Chapters).map((chapter) => ({
          chapterKey: chapter?.chapterKey ?? chapter?.ChapterKey ?? "",
          chapterTitle: chapter?.chapterTitle ?? chapter?.ChapterTitle ?? "",
          chapterOrder: chapter?.chapterOrder ?? chapter?.ChapterOrder ?? null,
          chunkCount: Number(chapter?.chunkCount ?? chapter?.ChunkCount ?? 0),
          estimatedTokens: Number(chapter?.estimatedTokens ?? chapter?.EstimatedTokens ?? 0),
          coveredTopics: chapter?.coveredTopics ?? chapter?.CoveredTopics ?? [],
          sampleSectionTitles: chapter?.sampleSectionTitles ?? chapter?.SampleSectionTitles ?? [],
          overviewShort: chapter?.overviewShort ?? chapter?.OverviewShort ?? ""
        }))
      : []
  };
}

export async function getStudentCourses(params = {}) {
  const query = new URLSearchParams();

  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 50));

  const payload = await http(`${STUDENT_COURSES_BASE}?${query.toString()}`);
  const data = unwrapPayload(payload);
  const items = data?.items ?? data?.Items ?? data ?? [];

  return {
    ...data,
    items: Array.isArray(items) ? items.map(normalizeCourse) : []
  };
}

export async function getStudentDocuments(params = {}) {
  const query = new URLSearchParams();

  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 20));

  const payload = await http(`${STUDENT_DOCUMENTS_BASE}?${query.toString()}`);
  const data = unwrapPayload(payload);
  const items = data?.items ?? data?.Items ?? data ?? [];

  return {
    ...data,
    items: Array.isArray(items) ? items.map(normalizeDocument) : []
  };
}

export async function uploadByosDocument({ file, courseId }) {
  if (!file) {
    throw new Error("Please choose a document file.");
  }

  if (!courseId?.trim()) {
    throw new Error("courseId is required.");
  }

  const formData = new FormData();
  formData.append("file", file);
  formData.append("courseId", courseId.trim());

  const payload = await http(`${STUDENT_DOCUMENTS_BASE}/byos`, {
    method: "POST",
    body: formData
  });

  return normalizeDocument(unwrapPayload(payload));
}

export async function getStudentDocumentPreview(documentId) {
  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  const payload = await http(`${STUDENT_DOCUMENTS_BASE}/${documentId}/preview`);
  return normalizePreviewDocument(unwrapPayload(payload));
}

export async function getStudentDocumentExtractionDraft(documentId) {
  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  const payload = await http(`${STUDENT_DOCUMENTS_BASE}/${documentId}/extraction-draft`);
  return normalizeExtractionDraft(unwrapPayload(payload));
}

export async function deleteStudentDocument(documentId) {
  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  await http(`${STUDENT_DOCUMENTS_BASE}/${documentId}`, {
    method: "DELETE"
  });

  return true;
}

export async function downloadStudentDocument(item) {
  const documentId = item?.id ?? item?.documentId ?? "";
  if (!String(documentId).trim()) {
    throw new Error("documentId is required.");
  }

  const accessToken = getAccessToken();
  const response = await fetch(`${getApiBaseUrl()}${STUDENT_DOCUMENTS_BASE}/${documentId}/download`, {
    headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : {}
  });

  if (!response.ok) {
    let payload = null;
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    throw new Error(payload?.error || payload?.message || "Unable to download this document.");
  }

  const blob = await response.blob();
  const downloadUrl = window.URL.createObjectURL(blob);
  const anchor = window.document.createElement("a");
  anchor.href = downloadUrl;
  anchor.download = item?.fileName || "document";
  window.document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.URL.revokeObjectURL(downloadUrl);
  return true;
}

export async function getStudentDocumentTopics(documentId) {
  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  const payload = await http(`${STUDENT_DOCUMENTS_BASE}/${documentId}/topics`);
  return normalizeTopicsPayload(unwrapPayload(payload));
}

export async function getStudentDocumentChapters(documentId) {
  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  const payload = await http(`${STUDENT_DOCUMENTS_BASE}/${documentId}/chapters`);
  return normalizeChaptersPayload(unwrapPayload(payload));
}

export async function getStudentDocumentChapterTopics(documentId, chapterKey) {
  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  if (!chapterKey?.trim()) {
    throw new Error("chapterKey is required.");
  }

  const payload = await http(`${STUDENT_DOCUMENTS_BASE}/${documentId}/chapters/${encodeURIComponent(chapterKey.trim())}/topics`);
  return normalizeTopicsPayload(unwrapPayload(payload));
}

export async function getStudentDocumentMultiChapterTopics(documentId, chapterKeys = []) {
  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  const normalizedKeys = Array.from(new Set(
    chapterKeys
      .map((item) => item?.trim())
      .filter(Boolean)
  ));

  if (!normalizedKeys.length) {
    throw new Error("At least one chapterKey is required.");
  }

  const query = new URLSearchParams();
  normalizedKeys.forEach((key) => query.append("chapterKeys", key));

  const payload = await http(`${STUDENT_DOCUMENTS_BASE}/${documentId}/chapter-topics?${query.toString()}`);
  return normalizeTopicsPayload(unwrapPayload(payload));
}
