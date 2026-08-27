import { http } from "./http";

const STUDENT_COURSES_BASE = "/api/student/courses";
const STUDENT_EXAMS_BASE = "/api/student/exams";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

function normalizeCourse(course) {
  return {
    id: course?.id ?? course?.Id ?? "",
    code: course?.code ?? course?.Code ?? "",
    name: course?.name ?? course?.Name ?? "Untitled course",
    description: course?.description ?? course?.Description ?? "",
    topicTags:
      course?.topicTags ??
      course?.TopicTags ??
      course?.availableTopicTags ??
      course?.AvailableTopicTags ??
      []
  };
}

export async function getStudentCourses() {
  const payload = await http(STUDENT_COURSES_BASE);
  const data = unwrapPayload(payload);
  const items = data?.items ?? data?.Items ?? data ?? [];
  return Array.isArray(items) ? items.map(normalizeCourse) : [];
}

export async function getStudentCourseById(courseId) {
  if (!courseId) {
    throw new Error("courseId is required.");
  }

  const payload = await http(`${STUDENT_COURSES_BASE}/${courseId}`);
  return normalizeCourse(unwrapPayload(payload));
}

function normalizeExam(exam) {
  return {
    id: exam?.id ?? exam?.Id ?? "",
    title: exam?.title ?? exam?.Title ?? "Untitled exam",
    courseId: exam?.courseId ?? exam?.CourseId ?? "",
    examType: exam?.examType ?? exam?.ExamType ?? inferExamType(exam),
    mode: exam?.mode ?? exam?.Mode ?? "Practice",
    timeLimit: exam?.timeLimit ?? exam?.TimeLimit ?? null,
    visibility: exam?.visibility ?? exam?.Visibility ?? "Public",
    feQuestionCount: exam?.feQuestionCount ?? exam?.FEQuestionCount ?? 0,
    peQuestionCount: exam?.peQuestionCount ?? exam?.PEQuestionCount ?? 0,
    hasActiveSession: Boolean(exam?.hasActiveSession ?? exam?.HasActiveSession ?? false),
    activeSessionId: exam?.activeSessionId ?? exam?.ActiveSessionId ?? "",
    activeSessionPaused: Boolean(exam?.activeSessionPaused ?? exam?.ActiveSessionPaused ?? false),
    activeSessionElapsedSeconds: Number(exam?.activeSessionElapsedSeconds ?? exam?.ActiveSessionElapsedSeconds ?? 0),
    activeDraftCodeCount: Number(exam?.activeDraftCodeCount ?? exam?.ActiveDraftCodeCount ?? 0),
    activeSelectedQuestionId: exam?.activeSelectedQuestionId ?? exam?.ActiveSelectedQuestionId ?? ""
  };
}

function inferExamType(exam) {
  const feCount = Number(exam?.feQuestionCount ?? exam?.FEQuestionCount ?? 0);
  const peCount = Number(exam?.peQuestionCount ?? exam?.PEQuestionCount ?? 0);

  if (feCount > 0 && peCount === 0) return "FE";
  if (feCount === 0 && peCount > 0) return "PE";
  if (feCount > 0 && peCount > 0) return "Mixed";
  return "Mixed";
}

export async function getStudentExamLibrary(params = {}) {
  const query = new URLSearchParams();
  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 50));

  const payload = await http(`${STUDENT_EXAMS_BASE}/library?${query.toString()}`);
  const data = unwrapPayload(payload);
  const items = Array.isArray(data?.items) ? data.items.map(normalizeExam) : [];

  return {
    ...data,
    items
  };
}

export async function getStudentSystemExamLibrary(params = {}) {
  const query = new URLSearchParams();
  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 50));

  const payload = await http(`${STUDENT_EXAMS_BASE}/system-library?${query.toString()}`);
  const data = unwrapPayload(payload);
  const items = Array.isArray(data?.items) ? data.items.map(normalizeExam) : [];

  return {
    ...data,
    items
  };
}

export async function getStudentExams(params = {}) {
  if (!params.courseId) {
    throw new Error("courseId is required.");
  }

  const query = new URLSearchParams();
  query.set("courseId", params.courseId);
  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 20));

  const payload = await http(`${STUDENT_EXAMS_BASE}?${query.toString()}`);
  const data = unwrapPayload(payload);
  const items = Array.isArray(data?.items) ? data.items.map(normalizeExam) : [];

  return {
    ...data,
    items
  };
}

export async function deleteStudentExam(examId) {
  if (!String(examId || "").trim()) {
    throw new Error("examId is required.");
  }

  await http(`${STUDENT_EXAMS_BASE}/${examId}`, {
    method: "DELETE"
  });

  return true;
}
