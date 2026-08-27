import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES } from "../../lib/routes";

const badges = [
  { id: "B1", name: "7-Day Fire", description: "Maintain a 7-day streak", earned: true, progress: "7/7" },
  { id: "B2", name: "Century Solver", description: "Solve 100 problems", earned: false, progress: "45/100" },
  { id: "B3", name: "PE Grinder", description: "Submit 25 PE attempts", earned: false, progress: "12/25" },
  { id: "B4", name: "Review Seeker", description: "Use AI review 10 times", earned: true, progress: "10/10" }
];

const heatmap = [1, 0, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1];

export function StreakBadgesPage() {
  return (
    <StudentScaffold
      activeRoute={ROUTES.studentStreakBadges}
      title="Streak & Badges"
      subtitle="Track daily momentum, review earned milestones, and see what badge criteria is closest."
    >
      <section className="student-metric-grid">
        <article className="student-panel"><span>Current Streak</span><strong>6 days</strong></article>
        <article className="student-panel"><span>Highest Streak</span><strong>14 days</strong></article>
        <article className="student-panel"><span>Earned Badges</span><strong>2 / 4</strong></article>
      </section>

      <section className="student-page-grid">
        <article className="student-panel">
          <div className="student-panel__header"><div><h2>Practice Heatmap</h2><p>Recent activity</p></div></div>
          <div className="heatmap-grid">
            {heatmap.map((value, index) => <span key={index} className={`heatmap-grid__cell${value ? " is-active" : ""}`} />)}
          </div>
        </article>
        <article className="student-panel">
          <div className="student-panel__header"><div><h2>Badge Collection</h2><p>Criteria and progress</p></div></div>
          <div className="student-badge-grid">
            {badges.map((badge) => (
              <article key={badge.id} className={`student-badge-card${badge.earned ? " is-earned" : ""}`}>
                <strong>{badge.name}</strong>
                <p>{badge.description}</p>
                <span>{badge.progress}</span>
              </article>
            ))}
          </div>
        </article>
      </section>
    </StudentScaffold>
  );
}

