import { http } from "../../../services/http";

const AI_QUESTION_BASE = "/api/ai/questions";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

export async function publishGeneratedQuestion(questionId) {
  const payload = await http(`${AI_QUESTION_BASE}/generated/${encodeURIComponent(questionId)}/publish`, {
    method: "PUT"
  });

  return unwrapPayload(payload);
}

export async function deleteGeneratedQuestion(questionId) {
  const payload = await http(`${AI_QUESTION_BASE}/generated/${encodeURIComponent(questionId)}`, {
    method: "DELETE"
  });

  return unwrapPayload(payload);
}
