export const ROUTES = {
  login: "/login/",
  dashboard: "/",
  studentExams: "/exams/",
  studentJourney: "/student/setup/",
  studentPeExams: "/exams/",
  studentSubscription: "/wallet/",
  studentPracticeHistory: "/practicehistory/",
  studentPracticeLibrary: "/library/",
  studentSystemExamLibrary: "/library/system/",
  studentQuestionBank: "/question-bank/",
  studentAnalytics: "/analytics/",
  studentLeaderboard: "/leaderboard/",
  studentStreakBadges: "/gamification/streak-badges/",
  studentByosUpload: "/documents/byos/",
  studentByosDocuments: "/documents/byos/",
  studentByosGenerateRoot: "/documents/byos/generate/",
  studentDataPreview: "/documents/preview/",
  studentGenerateSystem: "/generate/system/",
  studentPurchaseHistory: "/purchase/history/",
  studentPractice: "/practice/",
  studentFeExam: "/exams/fe/live/",
  studentCodingPractice: "/practice/coding/",
  studentFlashcards: "/flashcards/",
  studentResultViewer: "/practice/result/",
  purchaseCredits: "/wallet/top-up/",
  adminDashboard: "/admin/dashboard/",
  courseManagement: "/admin/course-management/",
  examManagement: "/admin/examinations/",
  adminPracticeSetup: "/admin/practice-setup/",
  adminWalletPackages: "/admin/wallet-packages/",
  questionManagement: "/admin/questions/",
  userManagement: "/admin/user-registry/",
  documentManagement: "/admin/document-operations/",
  adminGenerateQuestions: "/admin/generate-questions/",
  adminDataPreview: "/admin/document-preview/",
  adminSystemPrompts: "/admin/ai/system-prompts/",
  adminModelRouting: "/admin/ai/model-routing/",
  adminApiParameters: "/admin/ai/api-parameters/",
  adminAiProviders: "/admin/ai/providers/",
  adminAiAgents: "/admin/ai/agents/",
  adminAiRuleArtifacts: "/admin/ai/rule-artifacts/",
  adminAiBillingConfig: "/admin/ai/billing-config/",
  adminAiRuntimeHealth: "/admin/ai/runtime-health/",
  adminAiUsageLogs: "/admin/ai/usage-logs/",
  adminAiMentorFeedbacks: "/admin/ai/mentor-feedbacks/",
  adminAiSmokeQuestionGenerationReview: "/admin/ai/smoke/question-generation-review/",
  adminAiSmokeRetrievalPlan: "/admin/ai/smoke/retrieval-plan/",
  adminAiSmokeCodeMentor: "/admin/ai/smoke/code-mentor/",
  adminAiSmokeEmbedding: "/admin/ai/smoke/embedding/",
  adminAiSmokeGatekeeper: "/admin/ai/smoke/gatekeeper/",
  adminAiSmokeExtractedContent: "/admin/ai/smoke/extracted-content/",
  adminAiApiProbe: "/admin/ai/api-probe/",
  adminProfile: "/admin/settings/profile/",
  adminQuestionImport: "/admin/questions/import/",
  profile: "/settings/profile/"
};

ROUTES.legacyDashboard = "/dashboard/";
ROUTES.legacyStudentJourney = "/student/journey/";
ROUTES.legacyStudentSetupRoot = "/student/";

ROUTES.studentByosPreview = (documentId) => `/documents/byos/${documentId}/preview/`;
ROUTES.studentByosExtractionDraft = (documentId) => `/documents/byos/${documentId}/extraction-draft/`;
ROUTES.studentByosGenerate = (documentId) => `/documents/byos/${documentId}/generate/`;
ROUTES.adminDocumentGenerate = (documentId) => `/admin/document-operations/${documentId}/generate/`;
ROUTES.adminDocumentExtractionDraft = (documentId) => `/admin/document-operations/${documentId}/extraction-draft/`;
ROUTES.adminQuestionDetail = (questionId) => `/admin/questions/${questionId}/`;

let navigationBlocker = null;
let currentPathname = typeof window !== "undefined" ? normalizePath(window.location.pathname) : "/";

export function normalizePath(pathname) {
  if (!pathname) {
    return "/";
  }

  return pathname.endsWith("/") ? pathname : `${pathname}/`;
}

export function getRouteParamAfterPrefix(pathname, prefix, suffix = "/") {
  const normalizedPath = normalizePath(pathname);
  const normalizedPrefix = normalizePath(prefix);

  if (!normalizedPath.startsWith(normalizedPrefix)) {
    return null;
  }

  const remainder = normalizedPath.slice(normalizedPrefix.length);

  if (!remainder) {
    return null;
  }

  const trimmed = suffix && remainder.endsWith(suffix)
    ? remainder.slice(0, -suffix.length)
    : remainder;

  return trimmed.split("/")[0] || null;
}

export function setNavigationBlocker(blocker) {
  navigationBlocker = blocker;

  return () => {
    if (navigationBlocker === blocker) {
      navigationBlocker = null;
    }
  };
}

if (typeof window !== "undefined" && !window.__apeNavigationBlockerInstalled) {
  window.__apeNavigationBlockerInstalled = true;

  window.addEventListener("popstate", () => {
    const nextPathname = normalizePath(window.location.pathname);

    if (typeof navigationBlocker === "function") {
      const shouldProceed = navigationBlocker(nextPathname);

      if (shouldProceed === false) {
        window.history.pushState({}, "", currentPathname);
        return;
      }
    }

    currentPathname = nextPathname;
  });
}

export function navigateTo(pathname, { replace = false } = {}) {
  const target = normalizePath(pathname);
  const shouldPreserveScroll = typeof window !== "undefined"
    && (currentPathname.startsWith("/admin/ai/smoke/") || target.startsWith("/admin/ai/smoke/"));
  const previousScrollY = shouldPreserveScroll && typeof window !== "undefined" ? window.scrollY : 0;

  if (typeof navigationBlocker === "function") {
    const shouldProceed = navigationBlocker(target);
    if (shouldProceed === false) {
      return false;
    }
  }

  if (replace) {
    window.history.replaceState({}, "", target);
  } else {
    window.history.pushState({}, "", target);
  }

  currentPathname = target;

  window.dispatchEvent(new PopStateEvent("popstate"));

  if (shouldPreserveScroll) {
    // Keep the vertical scroll position stable while switching between AI Smoke Test steps.
    // (Admin users frequently compare inputs/outputs across steps and should not lose context.)
    window.requestAnimationFrame(() => window.scrollTo(0, previousScrollY));
  }
  return true;
}
