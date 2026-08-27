import {
  AdminAiAgentsPage,
  AdminRuleArtifactsPage,
  AdminBillingConfigPage,
  // AdminApiProbePage,
  AdminDashboardPage,
  AdminDocumentManagementPage,
  AdminExtractionDraftPage,
  AdminGenerateQuestionsPage,
  AdminProfilePage,
  AdminPracticeSetupPage,
  AdminProviderCredentialsPage,
  AdminQuestionsPage,
  AdminWalletPackagesPage,
  SmokeCodeMentorPage,
  SmokeEmbeddingPage,
  SmokeExtractedContentPage,
  SmokeGatekeeperPage,
  SmokeQuestionGenerationReviewPage,
  AdminUsageLogsPage,
  AdminUsersPage,
  CourseManagementPage
} from "../pages/admin";
import { ROUTES, getRouteParamAfterPrefix } from "../lib/routes";

export const ADMIN_ROUTES = [
  ROUTES.adminDashboard,
  ROUTES.courseManagement,
  ROUTES.adminPracticeSetup,
  ROUTES.adminWalletPackages,
  ROUTES.questionManagement,
  ROUTES.adminQuestionImport,
  ROUTES.userManagement,
  ROUTES.documentManagement,
  ROUTES.adminGenerateQuestions,
  ROUTES.adminProfile,
  ROUTES.adminAiProviders,
  ROUTES.adminAiAgents,
  ROUTES.adminAiRuleArtifacts,
  ROUTES.adminAiBillingConfig,
  // ROUTES.adminAiRuntimeHealth,
  ROUTES.adminAiUsageLogs,
  // ROUTES.adminAiMentorFeedbacks,
  ROUTES.adminAiSmokeQuestionGenerationReview,
  // ROUTES.adminAiSmokeRetrievalPlan,
  ROUTES.adminAiSmokeCodeMentor,
  // ROUTES.adminAiApiProbe,
  ROUTES.adminAiSmokeEmbedding,
  ROUTES.adminAiSmokeGatekeeper,
  ROUTES.adminAiSmokeExtractedContent
];

export function renderAdminRoute(pathname, routeProps) {
  const questionId = getRouteParamAfterPrefix(pathname, ROUTES.questionManagement);
  if (questionId && pathname !== ROUTES.questionManagement) {
    return <AdminQuestionsPage {...routeProps} />;
  }

  const documentGenerateId = getRouteParamAfterPrefix(pathname, ROUTES.documentManagement, "/generate/");
  if (documentGenerateId && pathname === ROUTES.adminDocumentGenerate(documentGenerateId)) {
    return <AdminGenerateQuestionsPage documentId={documentGenerateId} {...routeProps} />;
  }

  const documentExtractionDraftId = getRouteParamAfterPrefix(pathname, ROUTES.documentManagement, "/extraction-draft/");
  if (documentExtractionDraftId && pathname === ROUTES.adminDocumentExtractionDraft(documentExtractionDraftId)) {
    return <AdminExtractionDraftPage {...routeProps} />;
  }

  if (pathname === ROUTES.adminDashboard) {
    return <AdminDashboardPage {...routeProps} />;
  }

  if (pathname === ROUTES.courseManagement) {
    return <CourseManagementPage {...routeProps} />;
  }

  if (pathname === ROUTES.adminPracticeSetup) {
    return <AdminPracticeSetupPage {...routeProps} />;
  }

  if (pathname === ROUTES.adminWalletPackages) {
    return <AdminWalletPackagesPage {...routeProps} />;
  }

  if (pathname === ROUTES.questionManagement) {
    return <AdminQuestionsPage {...routeProps} />;
  }

  if (pathname === ROUTES.adminQuestionImport) {
    return <AdminQuestionsPage {...routeProps} />;
  }

  if (pathname === ROUTES.userManagement) {
    return <AdminUsersPage {...routeProps} />;
  }

  if (pathname === ROUTES.documentManagement) {
    return <AdminDocumentManagementPage {...routeProps} />;
  }

  if (pathname === ROUTES.adminGenerateQuestions) {
    return <AdminGenerateQuestionsPage {...routeProps} />;
  }

  if (pathname === ROUTES.adminProfile) {
    return <AdminProfilePage {...routeProps} />;
  }

  if (pathname === ROUTES.adminAiProviders) {
    return <AdminProviderCredentialsPage />;
  }

  if (pathname === ROUTES.adminAiAgents) {
    return <AdminAiAgentsPage />;
  }

  if (pathname === ROUTES.adminAiRuleArtifacts) {
    return <AdminRuleArtifactsPage />;
  }

  if (pathname === ROUTES.adminAiBillingConfig) {
    return <AdminBillingConfigPage />;
  }

  // Runtime health route is not in use yet.
  // if (pathname === ROUTES.adminAiRuntimeHealth) {
  //   return <AdminRuntimeHealthPage />;
  // }

  if (pathname === ROUTES.adminAiUsageLogs) {
    return <AdminUsageLogsPage />;
  }

  // Mentor feedback logs belong to student-facing flows, not admin routes.
  // if (pathname === ROUTES.adminAiMentorFeedbacks) {
  //   return <AdminMentorFeedbackLogsPage />;
  // }

  if (pathname === ROUTES.adminAiSmokeQuestionGenerationReview) {
    return <SmokeQuestionGenerationReviewPage />;
  }

  // Retrieval plan smoke route is not in use yet.
  // if (pathname === ROUTES.adminAiSmokeRetrievalPlan) {
  //   return <SmokeRetrievalPlanPage />;
  // }

  if (pathname === ROUTES.adminAiSmokeCodeMentor) {
    return <SmokeCodeMentorPage />;
  }

  // API probe route is temporarily disabled.
  // if (pathname === ROUTES.adminAiApiProbe) {
  //   return <AdminApiProbePage />;
  // }

  if (pathname === ROUTES.adminAiSmokeEmbedding) {
    return <SmokeEmbeddingPage />;
  }

  if (pathname === ROUTES.adminAiSmokeGatekeeper) {
    return <SmokeGatekeeperPage />;
  }

  if (pathname === ROUTES.adminAiSmokeExtractedContent) {
    return <SmokeExtractedContentPage />;
  }

  return null;
}

