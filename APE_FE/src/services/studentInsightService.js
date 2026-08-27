import { http } from "./http";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

function formatMinutes(totalMinutes) {
  const minutes = Math.max(0, Number(totalMinutes || 0));
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;

  if (hours <= 0) {
    return `${remainingMinutes}m`;
  }

  return `${hours}h ${remainingMinutes}m`;
}

function normalizeCourse(course) {
  return {
    id: course?.id ?? course?.Id ?? "",
    code: course?.code ?? course?.Code ?? "",
    name: course?.name ?? course?.Name ?? "Untitled course",
    description: course?.description ?? course?.Description ?? ""
  };
}

function formatScoreLabel(item) {
  const type = String(item?.examType ?? item?.ExamType ?? item?.type ?? "").toUpperCase();
  const correctCount = Number(item?.correctCount ?? item?.CorrectCount ?? 0);
  const totalQuestions = Number(item?.totalQuestions ?? item?.TotalQuestions ?? 0);
  const score = Number(item?.score ?? item?.Score ?? item?.totalScore ?? item?.TotalScore ?? 0);
  const maxScore = Number(item?.maxScore ?? item?.MaxScore ?? 0);

  if (type === "FE") {
    return `${correctCount}/${totalQuestions} correct`;
  }

  if (type === "PE" && maxScore > 0) {
    const normalizedScore = Number.isInteger(score) ? score : score.toFixed(2).replace(/\.?0+$/, "");
    const normalizedMaxScore = Number.isInteger(maxScore) ? maxScore : maxScore.toFixed(2).replace(/\.?0+$/, "");
    return `${normalizedScore}/${normalizedMaxScore}`;
  }

  return `${score}`;
}

function normalizePracticeHistoryItem(item) {
  const durationSeconds = Number(item?.durationSeconds ?? item?.DurationSeconds ?? 0);
  const type = String(item?.examType ?? item?.ExamType ?? item?.type ?? item?.Mode ?? "Unknown").toUpperCase();
  const score = Number(item?.score ?? item?.Score ?? item?.totalScore ?? item?.TotalScore ?? 0);
  const maxScore = Number(item?.maxScore ?? item?.MaxScore ?? 0);
  const correctCount = Number(item?.correctCount ?? item?.CorrectCount ?? 0);
  const totalQuestions = Number(item?.totalQuestions ?? item?.TotalQuestions ?? 0);
  const numericScore = maxScore > 0
    ? (score / maxScore) * 100
    : totalQuestions > 0
      ? (correctCount / totalQuestions) * 100
      : score;

  return {
    id: item?.sessionId ?? item?.SessionId ?? item?.id ?? item?.Id ?? "",
    sessionId: item?.sessionId ?? item?.SessionId ?? "",
    examId: item?.examId ?? item?.ExamId ?? "",
    latestPeSubmissionId: item?.latestPeSubmissionId ?? item?.LatestPeSubmissionId ?? "",
    latestPeVerdict: item?.latestPeVerdict ?? item?.LatestPeVerdict ?? "",
    date: item?.date ?? item?.Date ?? item?.startTime ?? item?.StartTime ?? new Date().toISOString(),
    courseId: item?.courseId ?? item?.CourseId ?? "",
    courseName: item?.courseName ?? item?.CourseName ?? "",
    examTitle: item?.examTitle ?? item?.ExamTitle ?? "Untitled exam",
    type,
    score,
    maxScore,
    correctCount,
    totalQuestions,
    scoreLabel: formatScoreLabel(item),
    numericScore,
    durationMinutes: Math.max(1, Math.round(durationSeconds / 60)),
    durationSeconds,
    status: item?.status ?? item?.Status ?? "",
    examDeleted: Boolean(item?.examDeleted ?? item?.ExamDeleted ?? false),
    topicTags: Array.isArray(item?.topicTags ?? item?.TopicTags) ? (item?.topicTags ?? item?.TopicTags) : []
  };
}

export async function getStudentPracticeHistory(filters = {}) {
  const query = new URLSearchParams();
  query.set("page", String(filters.page || 1));
  query.set("limit", String(filters.limit || 10));

  if (filters.courseId && filters.courseId !== "all") {
    query.set("courseId", filters.courseId);
  }

  if (filters.type && filters.type !== "All") {
    query.set("type", filters.type);
  }

  const payload = await http(`/api/student/practice/history?${query.toString()}`);
  const data = unwrapPayload(payload);
  const items = Array.isArray(data?.items ?? data?.Items) ? (data?.items ?? data?.Items) : [];

  return {
    items: items.map(normalizePracticeHistoryItem),
    total: Number(data?.total ?? data?.Total ?? items.length),
    page: Number(data?.page ?? data?.Page ?? filters.page ?? 1),
    limit: Number(data?.limit ?? data?.Limit ?? filters.limit ?? 10),
    totalPages: Number(data?.totalPages ?? data?.TotalPages ?? 1)
  };
}

export async function getStudentAnalytics() {
  const [summaryPayload, profilePayload] = await Promise.all([
    http("/api/student/analytics/summary"),
    http("/api/student/profile")
  ]);
  const data = unwrapPayload(summaryPayload);
  const profile = unwrapPayload(profilePayload);

  return {
    summary: {
      totalAttempted: Number(data?.totalProblemsSolved ?? data?.TotalProblemsSolved ?? 0),
      averageScore: Number(data?.averageScore ?? data?.AverageScore ?? 0),
      totalPracticeTime: formatMinutes(data?.totalTimeSpentMinutes ?? data?.TotalTimeSpentMinutes ?? 0),
      currentStreak: Number(data?.currentStreak ?? data?.CurrentStreak ?? 0)
    },
    subscription: {
      balance: Number(profile?.aiWalletBalanceVnd ?? profile?.AiWalletBalanceVnd ?? 0)
    },
    profile: {
      totalPracticeSessions: Number(profile?.totalPracticeSessions ?? profile?.TotalPracticeSessions ?? 0),
      totalFeSubmissions: Number(profile?.totalFESubmissions ?? profile?.TotalFESubmissions ?? 0),
      totalFeCorrect: Number(profile?.totalFECorrect ?? profile?.TotalFECorrect ?? 0),
      feCorrectRate: Number(profile?.feCorrectRate ?? profile?.FECorrectRate ?? 0),
      totalPeSubmissions: Number(profile?.totalPESubmissions ?? profile?.TotalPESubmissions ?? 0),
      totalPePassed: Number(profile?.totalPEPassed ?? profile?.TotalPEPassed ?? 0),
      averagePeScore: Number(profile?.averagePEScore ?? profile?.AveragePEScore ?? 0)
    }
  };
}

export async function getStudentLeaderboard(scope = "Global") {
  const normalizedScope = String(scope || "Global");
  const isCourseScope = normalizedScope !== "Global";
  const query = new URLSearchParams();
  query.set("scope", isCourseScope ? "Course" : "Global");

  if (isCourseScope) {
    query.set("courseId", normalizedScope);
  }

  const payload = await http(`/api/student/leaderboard?${query.toString()}`);
  const data = unwrapPayload(payload);
  const items = Array.isArray(data) ? data : [];

  return items.map((item) => ({
    id: item?.userId ?? item?.UserId ?? "",
    rank: Number(item?.rank ?? item?.Rank ?? 0),
    name: item?.fullName ?? item?.FullName ?? "Unknown student",
    score: Number(item?.totalScore ?? item?.TotalScore ?? 0),
    problemsSolved: Number(item?.problemsSolved ?? item?.ProblemsSolved ?? 0),
    course: isCourseScope ? normalizedScope : "Global",
    isCurrentUser: false
  }));
}

export async function getStudentStreak() {
  const payload = await http("/api/student/streak");
  const data = unwrapPayload(payload);
  const history = Array.isArray(data?.streakHistory ?? data?.StreakHistory) ? (data?.streakHistory ?? data?.StreakHistory) : [];

  return {
    currentStreak: Number(data?.currentStreak ?? data?.CurrentStreak ?? 0),
    highestStreak: Number(data?.highestStreak ?? data?.HighestStreak ?? 0),
    streakHistory: history.map((item) => new Date(item))
  };
}

export async function getStudentInsightCourses() {
  const payload = await http("/api/student/courses?page=1&limit=100");
  const data = unwrapPayload(payload);
  const items = Array.isArray(data?.items ?? data?.Items) ? (data?.items ?? data?.Items) : [];
  return items.map(normalizeCourse);
}
