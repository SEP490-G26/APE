import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { DashboardIcon } from "../../components/dashboard/DashboardIcon";
import { StudentWorkspaceShell } from "../../components/student/StudentWorkspaceShell";
import { dashboardSolutions } from "../../data/dashboardContent";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getAuthSession } from "../../lib/storage";
import { getStudentAnalytics, getStudentLeaderboard, getStudentStreak } from "../../services/studentInsightService";

function buildDashboardProfile() {
  const user = getAuthSession()?.user;

  return {
    fullName: user?.fullName || "APE Student",
    email: user?.email || "student@fpt.edu.vn",
    avatarUrl: user?.avatarUrl || "",
    aiWalletBalanceVnd: Number(user?.aiWalletBalanceVnd ?? 0),
    currentStreak: Number(user?.currentStreak ?? 0),
    highestStreak: Number(user?.highestStreak ?? 0)
  };
}

function formatVnd(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} VND`;
}

function buildMetrics(profile, summary) {
  return [
    { label: "Current Streak", value: `${Number(profile?.currentStreak || 0)} Days`, tone: "warm", icon: "activity" },
    { label: "AI Wallet", value: formatVnd(profile.aiWalletBalanceVnd), tone: "cool", icon: "bag" }
  ];
}

const quickActions = [
  {
    icon: "spark",
    title: "Practice MCQ",
    description: "Generate targeted FE questions by topic and difficulty.",
    cta: "Open FE Setup",
    href: ROUTES.studentExams
  },
  {
    icon: "monitor",
    title: "Coding Lab",
    description: "Enter the PE workspace with grading and AI review support.",
    cta: "Open PE Setup",
    href: ROUTES.studentPeExams
  },
  {
    icon: "chart",
    title: "Practice History",
    description: "Review previous practice sessions and reopen your recent results.",
    cta: "Open History",
    href: ROUTES.studentPracticeHistory
  },
  {
    icon: "bag",
    title: "Top Up Wallet",
    description: "Open your AI wallet and add VND balance for AI features.",
    cta: "Open Wallet",
    href: ROUTES.studentSubscription
  }
];

export function DashboardPage() {
  const profile = buildDashboardProfile();
  const [summary, setSummary] = useState({
    totalAttempted: 0,
    averageScore: 0,
    totalPracticeTime: "0m",
    currentStreak: 0
  });
  const [streak, setStreak] = useState({ currentStreak: 0, highestStreak: 0, streakHistory: [] });
  const [leaderboard, setLeaderboard] = useState([]);

  useEffect(() => {
    Promise.all([
      getStudentAnalytics(),
      getStudentStreak(),
      getStudentLeaderboard("Global")
    ])
      .then(([analytics, streakData, leaderboardData]) => {
        setSummary(analytics?.summary || {
          totalAttempted: 0,
          averageScore: 0,
          totalPracticeTime: "0m",
          currentStreak: 0
        });

        const currentUserId = getAuthSession()?.user?.id || getAuthSession()?.user?.userId || "";
        setLeaderboard(
          (leaderboardData || []).map((item) => ({
            ...item,
            isCurrentUser: Boolean(currentUserId) && item.id === currentUserId
          }))
        );
        setStreak(streakData || { currentStreak: 0, highestStreak: 0, streakHistory: [] });
      })
      .catch(() => {
        setSummary({
          totalAttempted: 0,
          averageScore: 0,
          totalPracticeTime: "0m",
          currentStreak: 0
        });
        setLeaderboard([]);
        setStreak({ currentStreak: 0, highestStreak: 0, streakHistory: [] });
      });
  }, []);

  const metrics = useMemo(() => buildMetrics(profile, summary), [profile, summary]);

  return (
    <StudentWorkspaceShell
      activeRoute={ROUTES.dashboard}
      topbarActionLabel="Open Practice"
      topbarActionHref={ROUTES.studentPractice}
      showPageHeader={false}
      mainClassName="student-exam-main--dashboard"
    >
      <section className="dashboard-main dashboard-main--workspace dashboard-main--shell">
        <section className="dashboard-hero-panel">
          <div className="dashboard-welcome-row dashboard-welcome-row--shell">
            <div className="dashboard-hero-copy">
              <p className="dashboard-hero-eyebrow">Student command center</p>
              <h1>Hello, {profile.fullName}.</h1>
              <p>Your learning cockpit is ready with active practice paths, AI guidance, and upcoming targets.</p>
              <div className="dashboard-hero-actions">
                <Button variant="primary" onClick={() => navigateTo(ROUTES.studentJourney)}>
                  Open Setup
                </Button>
                <Button variant="secondary" onClick={() => navigateTo(ROUTES.studentAnalytics)}>
                  Open Analytics
                </Button>
              </div>
            </div>

            <section className="dashboard-stats-grid dashboard-stats-grid--hero">
              {metrics.map((metric) => (
                <article key={metric.label} className={`dashboard-stat-card is-${metric.tone}`}>
                  <div className="dashboard-stat-card__icon">
                    <DashboardIcon kind={metric.icon} />
                  </div>
                  <strong>{metric.value}</strong>
                  <span>{metric.label}</span>
                </article>
              ))}
            </section>
          </div>
        </section>

        <section className="dashboard-solutions-panel">
          <div className="dashboard-solutions-panel__header">
            <h2>Designed for Educational Excellence</h2>
            <span />
          </div>

          <div className="dashboard-solutions-grid">
            {dashboardSolutions.map((solution) => (
              <article
                key={solution.title}
                className={`dashboard-solution-feature-card${solution.accent ? " is-accent" : ""}`}
              >
                <div className="dashboard-solution-feature-card__icon">
                  <DashboardIcon kind={solution.icon} />
                </div>
                <strong>{solution.title}</strong>
                <ul>
                  {solution.points.map((point) => (
                    <li key={point}>{point}</li>
                  ))}
                </ul>
              </article>
            ))}
          </div>
        </section>

        <section className="dashboard-quick-actions dashboard-quick-actions--shell">
          <div className="dashboard-section-heading">
            <h2>Quick Actions</h2>
          </div>
          <div className="dashboard-quick-actions__grid">
            {quickActions.map((item) => (
              <button
                key={item.title}
                type="button"
                className="dashboard-quick-card dashboard-quick-card--link"
                onClick={() => navigateTo(item.href)}
              >
                <div className="dashboard-quick-card__icon">
                  <DashboardIcon kind={item.icon} />
                </div>
                <strong>{item.title}</strong>
                <p>{item.description}</p>
                <span className="dashboard-quick-card__cta">
                  {item.cta}
                </span>
              </button>
            ))}
          </div>
        </section>
      </section>
    </StudentWorkspaceShell>
  );
}
