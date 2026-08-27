import { http } from "../../../services/http";

const AI_QUESTION_BASE = "/api/ai/questions";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

function normalizeGenerationQuestion(question) {
  return {
    ...question,
    title: question?.title ?? question?.Title ?? "",
    description: question?.description ?? question?.Description ?? "",
    topicTags: Array.isArray(question?.topicTags ?? question?.TopicTags) ? (question?.topicTags ?? question?.TopicTags) : [],
    options: Array.isArray(question?.options ?? question?.Options) ? (question?.options ?? question?.Options) : [],
    correctAnswer: Array.isArray(question?.correctAnswer ?? question?.CorrectAnswer)
      ? (question?.correctAnswer ?? question?.CorrectAnswer)
      : (question?.correctAnswer ?? question?.CorrectAnswer ? [question?.correctAnswer ?? question?.CorrectAnswer] : []),
    explanation: question?.explanation ?? question?.Explanation ?? "",
    skeletonCode: Array.isArray(question?.skeletonCode ?? question?.SkeletonCode) ? (question?.skeletonCode ?? question?.SkeletonCode) : [],
    testCases: Array.isArray(question?.testCases ?? question?.TestCases) ? (question?.testCases ?? question?.TestCases) : [],
    persistedQuestionId: question?.persistedQuestionId ?? question?.PersistedQuestionId ?? "",
    persistedStatus: question?.persistedStatus ?? question?.PersistedStatus ?? ""
  };
}

function normalizeGenerationResult(data) {
  const metrics = data?.metrics ?? data?.Metrics ?? {};
  const persistence = data?.persistence ?? data?.Persistence ?? {};
  const shortfallReport = data?.shortfallReport ?? data?.ShortfallReport ?? null;

  return {
    ...data,
    sourceScope: data?.sourceScope ?? data?.SourceScope ?? "",
    generatorModel: data?.generatorModel ?? data?.GeneratorModel ?? "",
    reviewerModel: data?.reviewerModel ?? data?.ReviewerModel ?? "",
    subject: data?.subject ?? data?.Subject ?? "",
    questionType: data?.questionType ?? data?.QuestionType ?? "",
    difficulty: data?.difficulty ?? data?.Difficulty ?? "",
    difficultyProfile: Array.isArray(data?.difficultyProfile ?? data?.DifficultyProfile)
      ? (data?.difficultyProfile ?? data?.DifficultyProfile)
      : [],
    questions: Array.isArray(data?.questions ?? data?.Questions)
      ? (data?.questions ?? data?.Questions).map(normalizeGenerationQuestion)
      : [],
    metrics: {
      ...metrics,
      chargedVnd: Number(metrics?.chargedVnd ?? metrics?.ChargedVnd ?? 0),
      actualDeductedVnd: Number(metrics?.actualDeductedVnd ?? metrics?.ActualDeductedVnd ?? 0),
      remainingBalanceVnd: Number(metrics?.remainingBalanceVnd ?? metrics?.RemainingBalanceVnd ?? 0)
    },
    persistence: {
      ...persistence,
      persisted: Boolean(persistence?.persisted ?? persistence?.Persisted),
      persistedCount: Number(persistence?.persistedCount ?? persistence?.PersistedCount ?? 0),
      persistedQuestionIds: Array.isArray(persistence?.persistedQuestionIds ?? persistence?.PersistedQuestionIds)
        ? (persistence?.persistedQuestionIds ?? persistence?.PersistedQuestionIds)
        : [],
      feQuestionIds: Array.isArray(persistence?.feQuestionIds ?? persistence?.FeQuestionIds)
        ? (persistence?.feQuestionIds ?? persistence?.FeQuestionIds)
        : [],
      peQuestionIds: Array.isArray(persistence?.peQuestionIds ?? persistence?.PeQuestionIds)
        ? (persistence?.peQuestionIds ?? persistence?.PeQuestionIds)
        : []
    },
    shortfallReport: shortfallReport
      ? {
          ...shortfallReport,
          requestedCount: Number(shortfallReport?.requestedCount ?? shortfallReport?.RequestedCount ?? 0),
          generatedCount: Number(shortfallReport?.generatedCount ?? shortfallReport?.GeneratedCount ?? 0),
          stopReason: shortfallReport?.stopReason ?? shortfallReport?.StopReason ?? "",
          summary: shortfallReport?.summary ?? shortfallReport?.Summary ?? "",
          suggestedActions: Array.isArray(shortfallReport?.suggestedActions ?? shortfallReport?.SuggestedActions)
            ? (shortfallReport?.suggestedActions ?? shortfallReport?.SuggestedActions)
            : []
        }
      : null
  };
}

export async function generateReviewFromByos(request) {
  const payload = await http(`${AI_QUESTION_BASE}/generate-review/byos`, {
    method: "POST",
    body: JSON.stringify(request)
  });

  return normalizeGenerationResult(unwrapPayload(payload));
}
