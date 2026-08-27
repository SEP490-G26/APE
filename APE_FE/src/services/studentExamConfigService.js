import { http } from "./http";

const STUDENT_EXAM_CONFIG_BASE = "/api/student/exam-config";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

function normalizeDocument(document) {
  const rawSource = document?.source ?? document?.Source ?? "";
  return {
    id: document?.id ?? document?.Id ?? "",
    fileName: document?.fileName ?? document?.FileName ?? "",
    source: String(rawSource).toUpperCase() === "BYOS" ? "BYOS" : "System",
    chunkCount: Number(document?.chunkCount ?? document?.ChunkCount ?? 0)
  };
}

function normalizeQuestion(question) {
  return {
    id: question?.id ?? question?.Id ?? "",
    title: question?.title ?? question?.Title ?? "",
    difficulty: question?.difficulty ?? question?.Difficulty ?? "Medium",
    topicTags: Array.isArray(question?.topicTags ?? question?.TopicTags)
      ? (question?.topicTags ?? question?.TopicTags)
      : []
  };
}

export async function getExamConfigDocuments(courseId, source = "System") {
  if (!courseId?.trim()) {
    throw new Error("courseId is required.");
  }

  const query = new URLSearchParams();
  query.set("source", source);
  const payload = await http(`${STUDENT_EXAM_CONFIG_BASE}/courses/${encodeURIComponent(courseId.trim())}/documents?${query.toString()}`);
  const data = unwrapPayload(payload);
  return Array.isArray(data) ? data.map(normalizeDocument) : [];
}

export async function getExamQuestionPool({ courseId, examType, documentIds, source = "System" }) {
  const query = new URLSearchParams();
  query.set("courseId", courseId);
  query.set("examType", examType);
  query.set("source", source);
  (documentIds || []).forEach((id) => query.append("documentIds", id));

  const payload = await http(`${STUDENT_EXAM_CONFIG_BASE}/question-pool?${query.toString()}`);
  const data = unwrapPayload(payload);

  return {
    ...data,
    availableCount: Number(data?.availableCount ?? data?.AvailableCount ?? 0),
    items: Array.isArray(data?.items ?? data?.Items) ? (data?.items ?? data?.Items).map(normalizeQuestion) : []
  };
}
