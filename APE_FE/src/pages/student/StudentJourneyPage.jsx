import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { DashboardIcon } from "../../components/dashboard/DashboardIcon";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getStudentAnalytics, getStudentStreak } from "../../services/studentInsightService";
import { getStudentDocuments } from "../../modules/student-documents/services/studentDocumentService";

const flowSteps = [
  {
    title: "Upload",
    detail: "Bring your own course materials into the workspace."
  },
  {
    title: "Generate",
    detail: "Pick document, chapters, and topics for question generation."
  },
  {
    title: "Practice Configuration",
    detail: "Open FE or PE setup and start the next session."
  }
];

const tracks = [
  {
    title: "Upload Documents",
    description: "Upload personal course materials, inspect extraction readiness, and manage your My Documents workspace.",
    href: ROUTES.studentByosDocuments,
    cta: "Open My Documents",
    icon: "monitor",
    meta: ["Upload + preview", "Extraction draft", "Personal documents"]
  },
  {
    title: "Generate Questions from Document",
    description: "Generate questions from your uploaded My Documents files by chapter and topic.",
    href: ROUTES.studentByosGenerateRoot,
    cta: "Generate Questions",
    icon: "spark",
    meta: ["Document + chapter", "Topic scope", "Student-owned content"]
  },
  {
    title: "Open Practice Configuration",
    description: "Open FE or PE practice setup and start from the existing question pool.",
    href: ROUTES.studentExams,
    cta: "Open Setup",
    icon: "chart",
    meta: ["FE or PE", "Question pool", "Result review"]
  }
];

export function StudentJourneyPage() {
  const [summary, setSummary] = useState(null);
  const [streak, setStreak] = useState(null);
  const [documents, setDocuments] = useState([]);

  useEffect(() => {
    let isMounted = true;

    Promise.all([
      getStudentAnalytics(),
      getStudentStreak(),
      getStudentDocuments({ page: 1, limit: 50 })
    ])
      .then(([analyticsResult, streakResult, documentResult]) => {
        if (!isMounted) {
          return;
        }

        setSummary(analyticsResult?.summary || null);
        setStreak(streakResult || null);
        setDocuments(Array.isArray(documentResult?.items) ? documentResult.items : []);
      })
      .catch(() => {
        if (!isMounted) {
          return;
        }

        setSummary({
          totalAttempted: 0,
          averageScore: 0,
          totalPracticeTime: "0m",
          currentStreak: 0
        });
        setStreak({ currentStreak: 0, highestStreak: 0, streakHistory: [] });
        setDocuments([]);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const documentSummary = useMemo(() => {
    const total = documents.length;
    const ready = documents.filter((item) => item.isReadyForGeneration).length;
    const processing = documents.filter((item) => item.ingestionStage === "processing").length;

    return { total, ready, processing };
  }, [documents]);

  const setupStats = [
    { label: "Documents", value: documentSummary.total, hint: "Uploaded to My Documents" },
    { label: "Ready", value: documentSummary.ready, hint: "Can generate now" }
  ];

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentJourney}
      title="Setup"
      subtitle="Choose how you want to start: upload materials, generate from My Documents, or open practice."
      showPageHeader={false}
    >
      <section className="journey-shell journey-shell--setup">
        <article className="journey-setup-hero">
          <div className="journey-setup-hero__copy">
            <p className="journey-hero__eyebrow">Setup</p>
            <h1>Choose the next study flow.</h1>
            <p>
              Start from your own documents, move into My Documents generation, or open FE and PE practice setup directly.
            </p>
          </div>

          <div className="journey-highlight-grid journey-highlight-grid--setup">
            {setupStats.map((item) => (
              <article key={item.label} className="journey-highlight-card">
                <span>{item.label}</span>
                <strong>{item.value}</strong>
                <small>{item.hint}</small>
              </article>
            ))}
          </div>
        </article>

        <article className="journey-panel journey-panel--setup-note">
          <div className="journey-panel__header">
            <div>
              <p className="ai-student-kicker">Flow</p>
              <h2>How this setup works</h2>
            </div>
          </div>

          <div className="journey-step-grid journey-step-grid--compact">
            {flowSteps.map((item, index) => (
              <article key={item.title} className="journey-step-card">
                <span className="journey-step-card__index">{`0${index + 1}`}</span>
                <strong>{item.title}</strong>
                <p>{item.detail}</p>
              </article>
            ))}
          </div>
        </article>

        <section className="journey-track-grid">
          {tracks.map((track) => (
            <article key={track.title} className="journey-track-card">
              <div className="journey-track-card__icon">
                <DashboardIcon kind={track.icon} />
              </div>
              <div className="journey-track-card__copy">
                <p className="ai-student-kicker">Track</p>
                <h2>{track.title}</h2>
                <p>{track.description}</p>
              </div>
              <div className="journey-track-card__meta">
                {track.meta.map((item) => (
                  <span key={item}>{item}</span>
                ))}
              </div>
              <Button
                variant="primary"
                className="journey-action-button journey-action-button--primary journey-track-card__cta"
                onClick={() => navigateTo(track.href)}
              >
                {track.cta}
              </Button>
            </article>
          ))}
        </section>
      </section>
    </StudentScaffold>
  );
}
