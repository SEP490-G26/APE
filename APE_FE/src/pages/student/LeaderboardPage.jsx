import { useEffect, useState } from "react";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES } from "../../lib/routes";
import { getStudentLeaderboard } from "../../services/studentInsightService";

export function LeaderboardPage() {
  const [scope, setScope] = useState("Global");
  const [rows, setRows] = useState([]);

  useEffect(() => {
    getStudentLeaderboard(scope).then(setRows);
  }, [scope]);

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentLeaderboard}
      title="Leaderboard"
      subtitle="Compare weekly momentum and all-time standing across the platform."
    >
      <section className="student-filter-strip">
        <label className="student-filter-field">
          <span>Scope</span>
          <select value={scope} onChange={(event) => setScope(event.target.value)}>
            <option value="Global">Global</option>
            <option value="PRO192">PRO192</option>
            <option value="CSD201">CSD201</option>
          </select>
        </label>
      </section>

      <section className="student-panel student-table-panel">
        <div className="student-table">
          <div className="student-table__head">
            <span>Rank</span>
            <span>Name</span>
            <span>Scope</span>
            <span>Total Score</span>
          </div>
          <div className="student-table__body">
            {rows.map((item) => (
              <article key={item.id} className={`student-table__row${item.isCurrentUser ? " is-highlighted" : ""}`}>
                <strong>#{item.rank}</strong>
                <span>{item.name}</span>
                <span>{item.course}</span>
                <strong>{item.score}</strong>
              </article>
            ))}
          </div>
        </div>
      </section>
    </StudentScaffold>
  );
}

