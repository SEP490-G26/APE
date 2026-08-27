import { http } from "../../../services/http";

const AI_BASE = "/api/ai";
const AI_QUESTION_BASE = "/api/ai/questions";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

function normalizeSourceDocument(document) {
  return {
    documentId: document?.documentId ?? document?.DocumentId ?? "",
    courseId: document?.courseId ?? document?.CourseId ?? "",
    fileName: document?.fileName ?? document?.FileName ?? "",
    fileType: document?.fileType ?? document?.FileType ?? "",
    source: document?.source ?? document?.Source ?? "",
    subjectCode: document?.subjectCode ?? document?.SubjectCode ?? "",
    gatekeeperVerdict: document?.gatekeeperVerdict ?? document?.GatekeeperVerdict ?? "",
    chunkCount: Number(document?.chunkCount ?? document?.ChunkCount ?? 0),
    distinctChapterCount: Number(document?.distinctChapterCount ?? document?.DistinctChapterCount ?? 0),
    distinctTopicCount: Number(document?.distinctTopicCount ?? document?.DistinctTopicCount ?? 0),
    createdAt: document?.createdAt ?? document?.CreatedAt ?? null,
    lastEmbeddedAt: document?.lastEmbeddedAt ?? document?.LastEmbeddedAt ?? null
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

export async function getSystemCourseDocuments(courseId) {
  if (!courseId?.trim()) {
    throw new Error("courseId is required.");
  }

  const payload = await http(`${AI_BASE}/courses/${encodeURIComponent(courseId.trim())}/documents`);
  const data = unwrapPayload(payload);
  return Array.isArray(data) ? data.map(normalizeSourceDocument) : [];
}

export async function getSystemDocumentChapters(courseId, documentId) {
  if (!courseId?.trim()) {
    throw new Error("courseId is required.");
  }

  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  const payload = await http(
    `${AI_BASE}/courses/${encodeURIComponent(courseId.trim())}/documents/${encodeURIComponent(documentId.trim())}/chapters`
  );
  return normalizeChaptersPayload(unwrapPayload(payload));
}

export async function getSystemDocumentChapterTopics(courseId, documentId, chapterKey) {
  if (!courseId?.trim()) {
    throw new Error("courseId is required.");
  }

  if (!documentId?.trim()) {
    throw new Error("documentId is required.");
  }

  if (!chapterKey?.trim()) {
    throw new Error("chapterKey is required.");
  }

  const payload = await http(
    `${AI_BASE}/courses/${encodeURIComponent(courseId.trim())}/documents/${encodeURIComponent(documentId.trim())}/chapters/${encodeURIComponent(chapterKey.trim())}/topics`
  );
  return normalizeTopicsPayload(unwrapPayload(payload));
}

export async function getSystemDocumentMultiChapterTopics(courseId, documentId, chapterKeys = []) {
  if (!courseId?.trim()) {
    throw new Error("courseId is required.");
  }

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

  const payload = await http(
    `${AI_BASE}/courses/${encodeURIComponent(courseId.trim())}/documents/${encodeURIComponent(documentId.trim())}/chapter-topics?${query.toString()}`
  );
  return normalizeTopicsPayload(unwrapPayload(payload));
}

export async function generateReviewFromSystem(request) {
  const payload = await http(`${AI_QUESTION_BASE}/generate-review/system`, {
    method: "POST",
    body: JSON.stringify(request)
  });

  return unwrapPayload(payload);
}

export async function generateReviewFromAdminSystem(request) {
  const payload = await http(`${AI_QUESTION_BASE}/generate-review`, {
    method: "POST",
    body: JSON.stringify({
      courseId: request?.courseId,
      documentId: request?.documentId,
      subject: request?.subject,
      difficultyMode: request?.difficultyMode ?? "single",
      difficultyProfile: request?.difficultyProfile ?? [],
      difficulty: request?.difficulty ?? "Medium",
      questionType: request?.questionType ?? "FE",
      count: request?.count ?? 1,
      mode: request?.mode ?? "SameModelDualRole",
      maxAttempts: request?.maxAttempts ?? 2,
      persistQuestions: Boolean(request?.persistQuestions),
      isPublic: Boolean(request?.isPublic),
      generatorOverride: request?.generatorOverride ?? null,
      reviewerOverride: request?.reviewerOverride ?? null,
      retrieval: {
        courseId: request?.courseId,
        documentId: request?.documentId,
        chapterKey: request?.chapterKey ?? null,
        chapterKeys: Array.isArray(request?.chapterKeys) ? request.chapterKeys : [],
        subject: request?.subject,
        questionType: request?.questionType ?? "FE",
        difficulty: request?.difficulty ?? "Medium",
        requestedQuestionCount: request?.count ?? 1,
        generationMode: request?.mode ?? "SameModelDualRole",
        targetTopics: Array.isArray(request?.targetTopics) ? request.targetTopics : [],
        sourceScope: "SYSTEM",
        language: request?.language ?? null,
        maxPackedTokens: request?.maxPackedTokens ?? 2400,
        retrievalQuery: request?.retrievalQuery ?? null,
        allowExtendedScope: true
      }
    })
  });

  return unwrapPayload(payload);
}
