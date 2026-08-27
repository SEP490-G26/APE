import { http } from "./http";

const ADMIN_QUESTIONS_BASE = "/api/admin/questions";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

export async function getQuestions(params = {}) {
  const query = new URLSearchParams();

  if (params.courseId && params.courseId !== "all") {
    query.set("courseId", params.courseId);
  }

  if (params.type && params.type !== "all") {
    query.set("type", params.type.toUpperCase());
  }

  if (params.difficulty && params.difficulty !== "all") {
    query.set("difficulty", params.difficulty);
  }

  if (params.status && params.status !== "all") {
    query.set("status", params.status);
  }

  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 20));

  const payload = await http(`${ADMIN_QUESTIONS_BASE}?${query.toString()}`);
  return unwrapPayload(payload);
}

export async function getQuestionDetail(questionId) {
  const payload = await http(`${ADMIN_QUESTIONS_BASE}/${questionId}`);
  return unwrapPayload(payload);
}

export async function updateQuestion(questionId, updates) {
  const payload = await http(`${ADMIN_QUESTIONS_BASE}/${questionId}`, {
    method: "PUT",
    body: JSON.stringify(updates)
  });

  return unwrapPayload(payload);
}

export async function toggleQuestionDisabled(questionId, reason) {
  const payload = await http(`${ADMIN_QUESTIONS_BASE}/${questionId}/disable`, {
    method: "PUT",
    body: JSON.stringify({ reason })
  });

  return unwrapPayload(payload);
}

export async function publishQuestion(questionId) {
  const payload = await http(`${ADMIN_QUESTIONS_BASE}/${questionId}/publish`, {
    method: "PUT"
  });

  return unwrapPayload(payload);
}

export async function publishAllQuestions(params = {}) {
  const query = new URLSearchParams();

  if (params.courseId && params.courseId !== "all") {
    query.set("courseId", params.courseId);
  }

  if (params.type && params.type !== "all") {
    query.set("type", params.type.toUpperCase());
  }

  if (params.difficulty && params.difficulty !== "all") {
    query.set("difficulty", params.difficulty);
  }

  if (params.status && params.status !== "all") {
    query.set("status", params.status);
  }

  const suffix = query.toString() ? `?${query.toString()}` : "";
  const payload = await http(`${ADMIN_QUESTIONS_BASE}/publish-all${suffix}`, {
    method: "PUT"
  });

  return unwrapPayload(payload);
}

export async function deleteQuestion(questionId) {
  const payload = await http(`${ADMIN_QUESTIONS_BASE}/${questionId}`, {
    method: "DELETE"
  });

  return unwrapPayload(payload);
}

export async function importQuestionsExcel(file) {
  const formData = new FormData();
  formData.append("file", file);

  const payload = await http(`${ADMIN_QUESTIONS_BASE}/import`, {
    method: "POST",
    body: formData
  });

  return unwrapPayload(payload);
}
