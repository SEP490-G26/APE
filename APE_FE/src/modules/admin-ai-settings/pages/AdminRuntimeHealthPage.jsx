import { useEffect, useState } from "react";
import { Button } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { ROUTES } from "../../../lib/routes";
import { getRuntimeHealth } from "../../../services/adminAiSettingsService";

function getStatusLabel(status) {
  switch (status) {
    case "ready_with_fallback":
      return "Ready + Fallback";
    case "ready_primary_only_fallback_not_ready":
      return "Primary Ready, Fallback Down";
    case "ready_primary_only":
      return "Primary Ready";
    case "primary_not_ready":
      return "Primary Not Ready";
    default:
      return "Disabled";
  }
}

function formatDateTime(value) {
  return value ? new Date(value).toLocaleString("vi-VN") : "Unknown";
}

export function AdminRuntimeHealthPage() {
  const [records, setRecords] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  async function loadHealth() {
    setIsLoading(true);
    try {
      const response = await getRuntimeHealth();
      setRecords(response);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    loadHealth();
  }, []);

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminAiRuntimeHealth}
      heroIcon="grid"
      heroTitle="AI Settings"
      heroSubtitle="Runtime health"
      title="AI Runtime Health"
      subtitle="Parsed runtime readiness by agent role, primary provider, and fallback chain."
    >
      <section className="admin-table-panel">
        <div className="admin-ai-panel__header">
          <div>
            <h2>Runtime Status</h2>
            <p>Backend-ready parsed health states. Primary status is taken from runtime_status.</p>
          </div>
          <Button variant="secondary" onClick={loadHealth} disabled={isLoading}>
            {isLoading ? "Refreshing..." : "Refresh"}
          </Button>
        </div>

        <div className="admin-ai-health-grid">
          {!records.length && !isLoading ? <div className="admin-empty-state">No health records available.</div> : null}
          {records.map((record) => (
            <article key={record.agent_role} className="admin-ai-health-card">
              <div className="admin-ai-health-card__top">
                <div>
                  <strong>{record.agent_role}</strong>
                  <p>{record.provider} / {record.model_name}</p>
                </div>
                <span className={`admin-ai-badge ${record.runtime_status}`}>
                  {getStatusLabel(record.runtime_status)}
                </span>
              </div>

              <div className="admin-ai-health-meta">
                <div>
                  <span>Primary</span>
                  <strong>{record.primary_runtime_ready ? "Ready" : "Not ready"}</strong>
                </div>
                <div>
                  <span>Fallback</span>
                  <strong>{record.fallback_configured ? (record.fallback_runtime_ready ? "Ready" : "Not ready") : "Missing"}</strong>
                </div>
                <div>
                  <span>Route Source</span>
                  <strong>{record.agent_enabled ? "DB-first" : "Disabled"}</strong>
                </div>
              </div>

              <div className="admin-ai-meta-stack">
                <span>Primary provider enabled: {record.primary_provider_enabled ? "Yes" : "No"}</span>
                <span>Primary key present: {record.primary_provider_has_api_key ? "Yes" : "No"}</span>
                <span>Fallback provider: {record.fallback_provider || "None"}</span>
                <span>Updated: {formatDateTime(record.updated_at)}</span>
              </div>

              {record.has_warnings ? (
                <div className="admin-ai-warning-list">
                  {record.warnings.map((warning) => (
                    <p key={warning}>{warning}</p>
                  ))}
                </div>
              ) : (
                <div className="admin-ai-ok-note">No runtime warnings.</div>
              )}
            </article>
          ))}
        </div>
      </section>
    </AdminScaffold>
  );
}
