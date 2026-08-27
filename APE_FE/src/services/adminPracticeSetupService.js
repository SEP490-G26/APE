import { http } from "./http";

const ADMIN_PRACTICE_SETUP_BASE = "/api/admin/practice-setup";

function unwrapPayload(payload) {
  return payload?.data || payload;
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

function normalizePresetScope(value) {
  return String(value || "").trim().toLowerCase() === "course" ? "Course" : "Course";
}

function normalizeExam(exam) {
  return {
    id: exam?.id ?? exam?.Id ?? "",
    title: exam?.title ?? exam?.Title ?? "Untitled setup",
    courseId: exam?.courseId ?? exam?.CourseId ?? "",
    examType: exam?.examType ?? exam?.ExamType ?? "FE",
    presetScope: normalizePresetScope(exam?.presetScope ?? exam?.PresetScope ?? "Course"),
    mode: exam?.mode ?? exam?.Mode ?? "Practice",
    visibility: exam?.visibility ?? exam?.Visibility ?? "Public",
    feQuestionCount: Number(exam?.feQuestionCount ?? exam?.FEQuestionCount ?? 0),
    peQuestionCount: Number(exam?.peQuestionCount ?? exam?.PEQuestionCount ?? 0),
    createdAt: exam?.createdAt ?? exam?.CreatedAt ?? null,
    updatedAt: exam?.updatedAt ?? exam?.UpdatedAt ?? null
  };
}

function normalizeExamDetail(exam) {
  return {
    ...normalizeExam(exam),
    totalQuestionCount: Number(exam?.totalQuestionCount ?? exam?.TotalQuestionCount ?? 0),
    feQuestions: Array.isArray(exam?.feQuestions ?? exam?.FEQuestions)
      ? (exam?.feQuestions ?? exam?.FEQuestions).map((item) => ({
        id: item?.id ?? item?.Id ?? "",
        title: item?.title ?? item?.Title ?? "",
        description: item?.description ?? item?.Description ?? "",
        difficulty: item?.difficulty ?? item?.Difficulty ?? "Medium",
        topicTags: Array.isArray(item?.topicTags ?? item?.TopicTags)
          ? (item?.topicTags ?? item?.TopicTags)
          : []
      }))
      : [],
    peQuestions: Array.isArray(exam?.peQuestions ?? exam?.PEQuestions)
      ? (exam?.peQuestions ?? exam?.PEQuestions).map((item) => ({
        id: item?.id ?? item?.Id ?? "",
        title: item?.title ?? item?.Title ?? "",
        description: item?.description ?? item?.Description ?? "",
        skeletonCode: Array.isArray(item?.skeletonCode ?? item?.SkeletonCode)
          ? (item?.skeletonCode ?? item?.SkeletonCode)
          : [],
        testCases: Array.isArray(item?.testCases ?? item?.TestCases)
          ? (item?.testCases ?? item?.TestCases)
          : []
      }))
      : []
  };
}

export async function getAdminPracticeSetupQuestionPool({ courseId, examType }) {
  const query = new URLSearchParams();
  query.set("courseId", courseId);
  query.set("examType", examType);

  const payload = await http(`${ADMIN_PRACTICE_SETUP_BASE}/question-pool?${query.toString()}`);
  const data = unwrapPayload(payload);

  return {
    ...data,
    availableCount: Number(data?.availableCount ?? data?.AvailableCount ?? 0),
    items: Array.isArray(data?.items ?? data?.Items) ? (data?.items ?? data?.Items).map(normalizeQuestion) : []
  };
}

export async function getAdminPracticeSetups(params = {}) {
  const query = new URLSearchParams();
  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 10));
  if (params.courseId && params.courseId !== "all") {
    query.set("courseId", params.courseId);
  }
  if (params.examType && params.examType !== "all") {
    query.set("examType", params.examType);
  }
  if (params.mode && params.mode !== "all") {
    query.set("mode", params.mode);
  }

  const payload = await http(`${ADMIN_PRACTICE_SETUP_BASE}?${query.toString()}`);
  const data = unwrapPayload(payload);

  return {
    ...data,
    items: Array.isArray(data?.items ?? data?.Items) ? (data?.items ?? data?.Items).map(normalizeExam) : []
  };
}

export async function getAdminPracticeSetupById(setupId) {
  const payload = await http(`${ADMIN_PRACTICE_SETUP_BASE}/${setupId}`);
  return normalizeExamDetail(unwrapPayload(payload));
}

export async function createAdminPracticeSetup(body) {
  const payload = await http(ADMIN_PRACTICE_SETUP_BASE, {
    method: "POST",
    body: JSON.stringify(body)
  });

  return normalizeExamDetail(unwrapPayload(payload));
}

export async function deleteAdminPracticeSetup(setupId) {
  if (!String(setupId || "").trim()) {
    throw new Error("setupId is required.");
  }

  await http(`${ADMIN_PRACTICE_SETUP_BASE}/${setupId}`, {
    method: "DELETE"
  });

  return true;
}
