import { http } from "./http";
import { getApiBaseUrl } from "../lib/env";
import { getAccessToken } from "../lib/storage";

const ADMIN_DOCUMENTS_BASE = "/api/admin/documents";

function unwrapPayload(payload) {
  return payload?.data || payload;
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
    rejectedHeadingCandidates: Array.isArray(draft?.rejectedHeadingCandidates ?? draft?.RejectedHeadingCandidates)
      ? (draft.rejectedHeadingCandidates ?? draft.RejectedHeadingCandidates)
      : [],
    cleanDisplayTitleCandidates: Array.isArray(draft?.cleanDisplayTitleCandidates ?? draft?.CleanDisplayTitleCandidates)
      ? (draft.cleanDisplayTitleCandidates ?? draft.CleanDisplayTitleCandidates)
      : [],
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

export function normalizeAdminDocument(document) {
  const fileName = document?.fileName ?? document?.FileName ?? "";
  const fileType = document?.fileType ?? document?.FileType ?? "";
  const ingestionStatus = String(document?.ingestionStatus ?? document?.IngestionStatus ?? document?.status ?? document?.Status ?? "processing").toLowerCase();
  const rawSubjectCode = document?.subjectCode ?? document?.SubjectCode ?? "";
  const rawGatekeeperVerdict = document?.gatekeeperVerdict ?? document?.GatekeeperVerdict ?? "";
  const rawSource = document?.source ?? document?.Source ?? "System";
  const rawFileSize = Number(document?.fileSizeBytes ?? document?.FileSizeBytes ?? document?.fileSize ?? document?.FileSize ?? 0);

  return {
    id: document?.id ?? document?.Id ?? "",
    name: fileName || "Untitled document",
    description: "",
    courseId: document?.courseId ?? document?.CourseId ?? "",
    fileName,
    fileExtension: fileType.replace(".", "").toLowerCase(),
    fileType,
    fileSize: rawFileSize,
    status: ingestionStatus,
    progress: ingestionStatus === "completed" ? 100 : ingestionStatus === "failed" ? 100 : 35,
    duplicateDetected: Boolean(document?.duplicateDetected ?? document?.DuplicateDetected),
    duplicateOfDocumentId: document?.duplicateOfDocumentId ?? document?.DuplicateOfDocumentId ?? null,
    duplicateMatchType: document?.duplicateMatchType ?? document?.DuplicateMatchType ?? null,
    gatekeeperVerdict: document?.gatekeeperVerdict ?? document?.GatekeeperVerdict ?? "",
    warning: "",
    chunkCount: Number(document?.chunkCount ?? document?.ChunkCount ?? 0),
    topicCount: Number(document?.tagCount ?? document?.TagCount ?? 0),
    source: rawSource,
    subjectCode: rawSubjectCode,
    gatekeeperVerdict: rawGatekeeperVerdict,
    ingestionStatus,
    extractionPreview: document?.previewText ?? document?.PreviewText ?? "",
    failureReason: ingestionStatus === "failed" ? "Upload failed during ingestion." : "",
    logs: [],
    lastExtractionDraftId: document?.lastExtractionDraftId ?? document?.LastExtractionDraftId ?? null,
    isReadyForGeneration: Boolean(document?.isReadyForGeneration ?? document?.IsReadyForGeneration),
    hasExtractionDraft: Boolean(document?.hasExtractionDraft ?? document?.HasExtractionDraft),
    hasEmbeddedChunks: Boolean(document?.hasEmbeddedChunks ?? document?.HasEmbeddedChunks),
    actualCostVnd: Number(document?.actualCostVnd ?? document?.ActualCostVnd ?? 0),
    chargedVnd: Number(document?.chargedVnd ?? document?.ChargedVnd ?? 0),
    actualDeductedVnd: Number(document?.actualDeductedVnd ?? document?.ActualDeductedVnd ?? 0),
    absorbedVnd: Number(document?.absorbedVnd ?? document?.AbsorbedVnd ?? 0),
    remainingBalanceVnd: Number(document?.remainingBalanceVnd ?? document?.RemainingBalanceVnd ?? 0),
    usdToVndRate: Number(document?.usdToVndRate ?? document?.UsdToVndRate ?? 0),
    createdAt: document?.createdAt ?? document?.CreatedAt ?? null
  };
}

function normalizeAdminPreview(document) {
  const rawTags = Array.isArray(document?.tags ?? document?.Tags) ? (document?.tags ?? document?.Tags) : [];
  return {
    ...normalizeAdminDocument(document),
    documentId: document?.documentId ?? document?.DocumentId ?? document?.id ?? document?.Id ?? "",
    previewText: document?.previewText ?? document?.PreviewText ?? "",
    tags: rawTags.map((tag) => (typeof tag === "string" ? { tag } : tag)),
    cleanupWarnings: Array.isArray(document?.cleanupWarnings ?? document?.CleanupWarnings)
      ? (document?.cleanupWarnings ?? document?.CleanupWarnings)
      : [],
    chapters: Array.isArray(document?.chapters ?? document?.Chapters) ? (document?.chapters ?? document?.Chapters) : [],
    createdAt: document?.createdAt ?? document?.CreatedAt ?? null,
    lastExtractedAt: document?.lastExtractedAt ?? document?.LastExtractedAt ?? null,
    lastEmbeddedAt: document?.lastEmbeddedAt ?? document?.LastEmbeddedAt ?? null
  };
}

export async function uploadAdminDocument({ file, courseId }) {
  if (!file) {
    throw new Error("Please choose a document file.");
  }

  if (!String(courseId || "").trim()) {
    throw new Error("Please choose a course.");
  }

  const formData = new FormData();
  formData.append("file", file);
  formData.append("courseId", String(courseId).trim());

  const payload = await http(`${ADMIN_DOCUMENTS_BASE}/upload`, {
    method: "POST",
    body: formData
  });

  return normalizeAdminDocument(unwrapPayload(payload));
}

export async function getAdminDocuments(params = {}) {
  const query = new URLSearchParams();
  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 50));

  if (params.courseId && params.courseId !== "all") {
    query.set("courseId", params.courseId);
  }

  const payload = await http(`${ADMIN_DOCUMENTS_BASE}?${query.toString()}`);
  const data = unwrapPayload(payload);
  const items = data?.items ?? data?.Items ?? [];

  return {
    ...data,
    items: Array.isArray(items) ? items.map(normalizeAdminDocument) : []
  };
}

export async function getAdminDocumentPreview(documentId) {
  if (!String(documentId || "").trim()) {
    throw new Error("documentId is required.");
  }

  const payload = await http(`${ADMIN_DOCUMENTS_BASE}/${documentId}/preview`);
  return normalizeAdminPreview(unwrapPayload(payload));
}

export async function getAdminDocumentExtractionDraft(documentId) {
  if (!String(documentId || "").trim()) {
    throw new Error("documentId is required.");
  }

  const payload = await http(`${ADMIN_DOCUMENTS_BASE}/${documentId}/extraction-draft`);
  return normalizeExtractionDraft(unwrapPayload(payload));
}

export async function deleteAdminDocument(documentId) {
  if (!String(documentId || "").trim()) {
    throw new Error("documentId is required.");
  }

  await http(`${ADMIN_DOCUMENTS_BASE}/${documentId}`, {
    method: "DELETE"
  });

  return true;
}

export async function downloadAdminDocument(item) {
  const documentId = item?.id ?? item?.documentId ?? "";
  if (!String(documentId).trim()) {
    throw new Error("documentId is required.");
  }

  const accessToken = getAccessToken();
  const response = await fetch(`${getApiBaseUrl()}${ADMIN_DOCUMENTS_BASE}/${documentId}/download`, {
    headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : {}
  });

  if (!response.ok) {
    let payload = null;
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    throw new Error(payload?.message || payload?.Message || "Could not download the document.");
  }

  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const anchor = window.document.createElement("a");
  anchor.href = url;
  anchor.download = item?.fileName || "document";
  anchor.click();
  window.URL.revokeObjectURL(url);

  return true;
}
