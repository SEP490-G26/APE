import {
  DashboardPage,
  KnowledgeAnalyticsPage,
  LeaderboardPage,
  PracticeHistoryPage,
  ProfilePage,
  PurchaseCreditsPage,
  PurchaseHistoryPage,
  ResultViewerPage,
  StreakBadgesPage,
  StudentQuestionBankPage,
  StudentExamConfigPage,
  StudentFlashcardViewerPage,
  StudentJourneyPage,
  StudentLaunchPage,
  StudentPracticeLibraryPage,
  StudentSystemExamLibraryPage,
  StudentMultipleChoiceExamPage,
  StudentSubscriptionPage
} from "../pages/student";
import {
  StudentByosDocumentPreviewPage,
  StudentByosDocumentsPage,
  StudentByosExtractionDraftPage
} from "../modules/student-documents";
import { GenerateFromByosPage } from "../modules/question-generation-byos";
import { GenerateFromSystemPage } from "../modules/question-generation-system";
import { DataApprovalPreviewPage } from "../pages/shared";
import { ROUTES, getRouteParamAfterPrefix } from "../lib/routes";

export const STUDENT_AUTH_ROUTES = [
  ROUTES.dashboard,
  ROUTES.profile,
  ROUTES.studentExams,
  ROUTES.studentJourney,
  ROUTES.studentSubscription,
  ROUTES.studentPracticeHistory,
  ROUTES.studentPracticeLibrary,
  ROUTES.studentSystemExamLibrary,
  ROUTES.studentQuestionBank,
  ROUTES.studentAnalytics,
  ROUTES.studentLeaderboard,
  ROUTES.studentStreakBadges,
  ROUTES.studentByosUpload,
  ROUTES.studentByosGenerateRoot,
  ROUTES.studentGenerateSystem,
  ROUTES.studentDataPreview,
  ROUTES.studentPurchaseHistory,
  ROUTES.studentPractice,
  ROUTES.studentFeExam,
  ROUTES.studentCodingPractice,
  ROUTES.studentFlashcards,
  ROUTES.studentResultViewer,
  ROUTES.purchaseCredits
];

export const STUDENT_MEMBER_ROUTES = [
  ROUTES.studentExams,
  ROUTES.studentJourney,
  ROUTES.studentSubscription,
  ROUTES.studentPracticeHistory,
  ROUTES.studentPracticeLibrary,
  ROUTES.studentSystemExamLibrary,
  ROUTES.studentQuestionBank,
  ROUTES.studentAnalytics,
  ROUTES.studentLeaderboard,
  ROUTES.studentStreakBadges,
  ROUTES.studentByosUpload,
  ROUTES.studentByosGenerateRoot,
  ROUTES.studentGenerateSystem,
  ROUTES.studentDataPreview,
  ROUTES.studentPurchaseHistory,
  ROUTES.studentPractice,
  ROUTES.studentFeExam,
  ROUTES.studentCodingPractice,
  ROUTES.studentFlashcards,
  ROUTES.studentResultViewer,
  ROUTES.purchaseCredits
];

export function renderStudentRoute(pathname, routeProps) {
  if (pathname === ROUTES.dashboard) {
    return <DashboardPage {...routeProps} />;
  }

  if (pathname === ROUTES.legacyDashboard) {
    return <DashboardPage {...routeProps} />;
  }

  if (pathname === ROUTES.profile) {
    return <ProfilePage {...routeProps} />;
  }

  if (pathname === ROUTES.studentExams) {
    return <StudentExamConfigPage {...routeProps} />;
  }

  if (pathname === ROUTES.studentJourney) {
    return <StudentJourneyPage {...routeProps} />;
  }

  if (pathname === ROUTES.legacyStudentJourney) {
    return <StudentJourneyPage {...routeProps} />;
  }

  if (pathname === ROUTES.studentPractice) {
    return <StudentPracticeLibraryPage {...routeProps} />;
  }

  if (pathname === ROUTES.studentFeExam) {
    return <StudentMultipleChoiceExamPage />;
  }

  if (pathname === ROUTES.studentCodingPractice) {
    return <StudentLaunchPage mode="Coding" />;
  }

  if (pathname === ROUTES.studentFlashcards) {
    return <StudentFlashcardViewerPage />;
  }

  if (pathname === ROUTES.studentResultViewer) {
    return <ResultViewerPage />;
  }

  if (pathname === ROUTES.studentByosDocuments) {
    return <StudentByosDocumentsPage />;
  }

  if (pathname === ROUTES.studentByosGenerateRoot) {
    return <GenerateFromByosPage />;
  }

  if (pathname === ROUTES.studentGenerateSystem) {
    return <GenerateFromSystemPage />;
  }

  const byosPreviewDocumentId = getRouteParamAfterPrefix(pathname, ROUTES.studentByosDocuments, "/preview/");
  if (byosPreviewDocumentId && pathname === ROUTES.studentByosPreview(byosPreviewDocumentId)) {
    return <StudentByosDocumentPreviewPage documentId={byosPreviewDocumentId} />;
  }

  const byosDraftDocumentId = getRouteParamAfterPrefix(pathname, ROUTES.studentByosDocuments, "/extraction-draft/");
  if (byosDraftDocumentId && pathname === ROUTES.studentByosExtractionDraft(byosDraftDocumentId)) {
    return <StudentByosExtractionDraftPage documentId={byosDraftDocumentId} />;
  }

  const byosGenerateDocumentId = getRouteParamAfterPrefix(pathname, ROUTES.studentByosDocuments, "/generate/");
  if (byosGenerateDocumentId && pathname === ROUTES.studentByosGenerate(byosGenerateDocumentId)) {
    return <GenerateFromByosPage documentId={byosGenerateDocumentId} />;
  }

  if (pathname === ROUTES.studentDataPreview) {
    return <DataApprovalPreviewPage />;
  }

  if (pathname === ROUTES.studentStreakBadges) {
    return <StreakBadgesPage />;
  }

  if (pathname === ROUTES.purchaseCredits) {
    return <PurchaseCreditsPage />;
  }

  if (pathname === ROUTES.studentSubscription) {
    return <StudentSubscriptionPage />;
  }

  if (pathname === ROUTES.studentPracticeHistory) {
    return <PracticeHistoryPage />;
  }

  if (pathname === ROUTES.studentQuestionBank) {
    return <StudentQuestionBankPage />;
  }

  if (pathname === ROUTES.studentPracticeLibrary) {
    return <StudentPracticeLibraryPage />;
  }

  if (pathname === ROUTES.studentSystemExamLibrary) {
    return <StudentSystemExamLibraryPage />;
  }

  if (pathname === ROUTES.studentAnalytics) {
    return <KnowledgeAnalyticsPage />;
  }

  if (pathname === ROUTES.studentLeaderboard) {
    return <LeaderboardPage />;
  }

  if (pathname === ROUTES.studentPurchaseHistory) {
    return <PurchaseHistoryPage />;
  }

  return null;
}

