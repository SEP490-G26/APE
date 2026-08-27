import { http } from "./http";

const STUDENT_QUESTION_BANK_BASE = "/api/student/question-bank";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

function normalizeQuestionListItem(item) {
  return {
    id: item?.id ?? item?.Id ?? "",
    type: item?.type ?? item?.Type ?? "",
    title: item?.title ?? item?.Title ?? "",
    status: item?.status ?? item?.Status ?? "",
    canPublish: Boolean(item?.canPublish ?? item?.CanPublish ?? false),
    difficulty: item?.difficulty ?? item?.Difficulty ?? "Medium",
    topicTags: Array.isArray(item?.topicTags ?? item?.TopicTags) ? (item?.topicTags ?? item?.TopicTags) : [],
    courseId: item?.courseId ?? item?.CourseId ?? "",
    courseCode: item?.courseCode ?? item?.CourseCode ?? "",
    courseName: item?.courseName ?? item?.CourseName ?? "",
    lastModifiedAt: item?.lastModifiedAt ?? item?.LastModifiedAt ?? null
  };
}

function normalizeQuestionDetail(item) {
  return {
    id: item?.id ?? item?.Id ?? "",
    type: item?.type ?? item?.Type ?? "",
    title: item?.title ?? item?.Title ?? "",
    status: item?.status ?? item?.Status ?? "",
    canPublish: Boolean(item?.canPublish ?? item?.CanPublish ?? false),
    description: item?.description ?? item?.Description ?? "",
    difficulty: item?.difficulty ?? item?.Difficulty ?? "Medium",
    topicTags: Array.isArray(item?.topicTags ?? item?.TopicTags) ? (item?.topicTags ?? item?.TopicTags) : [],
    courseId: item?.courseId ?? item?.CourseId ?? "",
    courseCode: item?.courseCode ?? item?.CourseCode ?? "",
    courseName: item?.courseName ?? item?.CourseName ?? "",
    options: Array.isArray(item?.options ?? item?.Options) ? (item?.options ?? item?.Options) : [],
    correctAnswer: Array.isArray(item?.correctAnswer ?? item?.CorrectAnswer) ? (item?.correctAnswer ?? item?.CorrectAnswer) : [],
    explanation: item?.explanation ?? item?.Explanation ?? "",
    skeletonCode: Array.isArray(item?.skeletonCode ?? item?.SkeletonCode) ? (item?.skeletonCode ?? item?.SkeletonCode) : [],
    sampleTestCases: Array.isArray(item?.sampleTestCases ?? item?.SampleTestCases) ? (item?.sampleTestCases ?? item?.SampleTestCases) : []
  };
}

export async function getStudentQuestionBank(params = {}) {
  const query = new URLSearchParams();
  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 10));

  if (params.courseId && params.courseId !== "all") {
    query.set("courseId", params.courseId);
  }

  if (params.type && params.type !== "all") {
    query.set("type", params.type);
  }

  if (params.difficulty && params.difficulty !== "all") {
    query.set("difficulty", params.difficulty);
  }

  if (params.status && params.status !== "all") {
    query.set("status", params.status);
  }

  if (params.keyword?.trim()) {
    query.set("keyword", params.keyword.trim());
  }

  (params.topicTags || []).forEach((tag) => {
    if (tag?.trim()) {
      query.append("topicTags", tag.trim());
    }
  });

  const payload = await http(`${STUDENT_QUESTION_BANK_BASE}?${query.toString()}`);
  const data = unwrapPayload(payload);

  return {
    ...data,
    items: Array.isArray(data?.items ?? data?.Items) ? (data?.items ?? data?.Items).map(normalizeQuestionListItem) : []
  };
}

export async function getStudentQuestionBankDetail(questionId) {
  const payload = await http(`${STUDENT_QUESTION_BANK_BASE}/${encodeURIComponent(questionId)}`);
  return normalizeQuestionDetail(unwrapPayload(payload));
}

export async function getStudentQuestionBankTopicTags(courseId = "") {
  const query = new URLSearchParams();
  if (courseId && courseId !== "all") {
    query.set("courseId", courseId);
  }

  const suffix = query.toString() ? `?${query.toString()}` : "";
  const payload = await http(`${STUDENT_QUESTION_BANK_BASE}/topic-tags${suffix}`);
  const data = unwrapPayload(payload);
  return Array.isArray(data) ? data : [];
}

export async function deleteStudentQuestion(questionId) {
  const payload = await http(`${STUDENT_QUESTION_BANK_BASE}/${encodeURIComponent(questionId)}`, {
    method: "DELETE"
  });

  return unwrapPayload(payload);
}

export async function publishStudentQuestion(questionId) {
  const payload = await http(`${STUDENT_QUESTION_BANK_BASE}/${encodeURIComponent(questionId)}/publish`, {
    method: "PUT"
  });

  return unwrapPayload(payload);
}

export async function publishAllStudentQuestions(params = {}) {
  const query = new URLSearchParams();

  if (params.courseId && params.courseId !== "all") {
    query.set("courseId", params.courseId);
  }

  if (params.type && params.type !== "all") {
    query.set("type", params.type);
  }

  if (params.difficulty && params.difficulty !== "all") {
    query.set("difficulty", params.difficulty);
  }

  if (params.status && params.status !== "all") {
    query.set("status", params.status);
  }

  if (params.keyword?.trim()) {
    query.set("keyword", params.keyword.trim());
  }

  (params.topicTags || []).forEach((tag) => {
    if (tag?.trim()) {
      query.append("topicTags", tag.trim());
    }
  });

  const suffix = query.toString() ? `?${query.toString()}` : "";
  const payload = await http(`${STUDENT_QUESTION_BANK_BASE}/publish-all${suffix}`, {
    method: "PUT"
  });

  return unwrapPayload(payload);
}
