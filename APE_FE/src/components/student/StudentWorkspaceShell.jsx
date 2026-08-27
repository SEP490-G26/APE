import { ROUTES, navigateTo } from "../../lib/routes";
import { footerLinks } from "../../data/landingContent";
import { Button } from "../common";
import { AppFooter } from "../layout/AppFooter";
import { StudentTopbar } from "./StudentTopbar";

function getShellSection(activeRoute) {
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

function getTopbarSection(activeRoute) {
  if (activeRoute === ROUTES.profile) {
    return "profile";
  }

  if (activeRoute === ROUTES.studentFeExam) {
    return "practice";
  }

  return getShellSection(activeRoute);
}

export function StudentWorkspaceShell({
  activeRoute,
  title,
  subtitle,
  children,
  actions,
  showPageHeader = true,
  mainClassName = "",
  showFooter = true
}) {
  const activeSection = getShellSection(activeRoute);
  const topbarSection = getTopbarSection(activeRoute);
  const mainClasses = ["student-exam-main", mainClassName].filter(Boolean).join(" ");

  return (
    <div className={`student-exam-shell student-exam-shell--workspace student-exam-shell--${activeSection}`}>
      <StudentTopbar
        activeSection={topbarSection}
      />

      <main className={mainClasses}>
        {showPageHeader ? (
          <section className="student-exam-page-header">
            <div>
              <h1>{title}</h1>
              <p>{subtitle}</p>
            </div>
          </section>
        ) : null}
        <div className="student-page-stack">
          {children}
        </div>
        {showFooter ? <AppFooter links={footerLinks} className="app-footer--workspace" /> : null}
      </main>
    </div>
  );
}
