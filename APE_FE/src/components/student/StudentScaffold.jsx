import { StudentWorkspaceShell } from "./StudentWorkspaceShell";
import { ROUTES } from "../../lib/routes";

function getSection(activeRoute) {
  if ([ROUTES.studentJourney, ROUTES.studentByosDocuments, ROUTES.studentByosUpload, ROUTES.studentByosGenerateRoot, ROUTES.studentExams, ROUTES.studentPeExams].includes(activeRoute)) {
    return "journey";
  }

  if ([ROUTES.studentGenerateSystem].includes(activeRoute)) {
    return "exam";
  }

  if ([ROUTES.studentPractice, ROUTES.studentPracticeHistory, ROUTES.studentPracticeLibrary, ROUTES.studentSystemExamLibrary, ROUTES.studentQuestionBank, ROUTES.studentCodingPractice, ROUTES.studentFlashcards, ROUTES.studentResultViewer].includes(activeRoute)) {
    return "practice";
  }

  if ([ROUTES.studentSubscription, ROUTES.purchaseCredits, ROUTES.studentPurchaseHistory].includes(activeRoute)) {
    return "wallet";
  }

  if ([ROUTES.studentAnalytics, ROUTES.studentLeaderboard, ROUTES.studentStreakBadges].includes(activeRoute)) {
    return "analytic";
  }

  return "dashboard";
}

export function StudentScaffold({ activeRoute, title, subtitle, children, actions, topbarActionLabel, topbarActionHref, showPageHeader = true, showFooter = true }) {
  return (
    <StudentWorkspaceShell
      activeRoute={activeRoute}
      title={title}
      subtitle={subtitle}
      actions={actions}
      topbarActionLabel={topbarActionLabel}
      topbarActionHref={topbarActionHref}
      mainClassName={`student-exam-main--${getSection(activeRoute)}`}
      showPageHeader={showPageHeader}
      showFooter={showFooter}
    >
      {children}
    </StudentWorkspaceShell>
  );
}
