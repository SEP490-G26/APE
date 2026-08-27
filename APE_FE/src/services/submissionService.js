import { http } from "./http";

const STUDENT_SUBMISSIONS_BASE = "/api/student/submissions";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

export async function requestMentorFeedback(submissionId, runtimeOverride = null) {
  if (!submissionId?.trim()) {
    throw new Error("submissionId is required.");
  }

  const payload = await http(`${STUDENT_SUBMISSIONS_BASE}/${submissionId.trim()}/mentor`, {
    method: "POST",
    body: JSON.stringify({
      runtimeOverride
    })
  });

  return unwrapPayload(payload);
}

export async function getLatestMentorFeedback(submissionId) {
  if (!submissionId?.trim()) {
    throw new Error("submissionId is required.");
  }

  const payload = await http(`${STUDENT_SUBMISSIONS_BASE}/${submissionId.trim()}/mentor-feedback`);
  return unwrapPayload(payload);
}
