import { useEffect, useMemo, useState } from "react";
import { Button, Field } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { ROUTES } from "../../../lib/routes";
import { getAdminMentorFeedbackLogs } from "../../../services/adminAiMonitoringService";

function formatDateTime(value) {
  return value ? new Date(value).toLocaleString("vi-VN") : "Unknown";
}

export function AdminMentorFeedbackLogsPage() {
  const [filters, setFilters] = useState({
    submissionId: "",
    verdict: "",
    fromDate: "",
    toDate: ""
  });
  const [rows, setRows] = useState([]);
  const [selectedId, setSelectedId] = useState("");
  const [isLoading, setIsLoading] = useState(true);

  async function loadRows(nextFilters = filters) {
    setIsLoading(true);
    try {
      const response = await getAdminMentorFeedbackLogs(nextFilters);
      setRows(response);
      setSelectedId(response[0]?.id || "");
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    loadRows(filters);
  }, []);

  const selectedRow = useMemo(
    () => rows.find((item) => item.id === selectedId) || null,
    [rows, selectedId]
  );

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminAiMentorFeedbacks}
      heroIcon="edit"
      heroTitle="AI Monitoring"
      heroSubtitle="Mentor feedback logs"
      title="AI Mentor Feedback Logs"
      subtitle="Review mentor verdict history and inspect expanded feedback detail for each submission."
    >
      <section className="admin-filter-panel admin-filter-panel--stack">
        <div className="admin-ai-filter-grid">
          <Field label="Submission Id">
            <input className="ui-text-input" value={filters.submissionId} onChange={(event) => setFilters((current) => ({ ...current, submissionId: event.target.value }))} />
          </Field>
          <Field label="Verdict">
            <input className="ui-text-input" value={filters.verdict} onChange={(event) => setFilters((current) => ({ ...current, verdict: event.target.value }))} />
          </Field>
          <Field label="From Date">
            <input className="ui-text-input" type="date" value={filters.fromDate} onChange={(event) => setFilters((current) => ({ ...current, fromDate: event.target.value }))} />
          </Field>
          <Field label="To Date">
            <input className="ui-text-input" type="date" value={filters.toDate} onChange={(event) => setFilters((current) => ({ ...current, toDate: event.target.value }))} />
          </Field>
        </div>
        <div className="admin-ai-actions admin-ai-actions--flush">
          <span className="admin-ai-inline-note">{rows.length} feedback entries</span>
          <Button variant="primary" onClick={() => loadRows(filters)} disabled={isLoading}>
            {isLoading ? "Loading..." : "Apply Filters"}
          </Button>
        </div>
      </section>

      <section className="admin-ai-monitoring-layout">
        <section className="admin-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Feedback History</h2>
              <p>Submission-level mentor outcomes.</p>
            </div>
          </div>

          <div className="admin-ai-log-list">
            {!rows.length && !isLoading ? <div className="admin-empty-state">No mentor feedback matches the current filters.</div> : null}
            {rows.map((row) => (
              <button
                key={row.id}
                type="button"
                className={`admin-ai-log-row${selectedId === row.id ? " is-active" : ""}`}
                onClick={() => setSelectedId(row.id)}
              >
                <div>
                  <strong>{row.submission_id}</strong>
                  <p>{row.question_type} • {row.verdict} • {row.model_name}</p>
                </div>
                <div className="admin-ai-log-row__metrics">
                  <span>Score {row.quality_score}</span>
                  <span>{row.date}</span>
                  <span>{formatDateTime(row.created_at)}</span>
                </div>
              </button>
            ))}
          </div>
        </section>

        <section className="admin-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Feedback Detail</h2>
              <p>{selectedRow ? `${selectedRow.submission_id} • ${selectedRow.verdict}` : "Select one feedback entry."}</p>
            </div>
          </div>

          {selectedRow ? (
            <div className="admin-ai-detail-panel">
              <div className="admin-ai-health-meta admin-ai-health-meta--summary">
                <div>
                  <span>Quality Score</span>
                  <strong>{selectedRow.quality_score}</strong>
                </div>
                <div>
                  <span>Question Type</span>
                  <strong>{selectedRow.question_type}</strong>
                </div>
                <div>
                  <span>Suggested Complexity</span>
                  <strong>{selectedRow.suggested_complexity}</strong>
                </div>
              </div>

              <div className="admin-ai-feedback-copy">
                <section>
                  <h3>Performance Summary</h3>
                  <p>{selectedRow.performance_summary}</p>
                </section>
                <section>
                  <h3>Error Analysis</h3>
                  <p>{selectedRow.error_analysis}</p>
                </section>
                <section>
                  <h3>Improvement Suggestions</h3>
                  <p>{selectedRow.improvement_suggestions}</p>
                </section>
                <section>
                  <h3>Feedback Text</h3>
                  <p>{selectedRow.feedback_text}</p>
                </section>
              </div>
            </div>
          ) : (
            <div className="admin-empty-state">Pick one feedback row to inspect the expanded mentor detail.</div>
          )}
        </section>
      </section>
    </AdminScaffold>
  );
}
