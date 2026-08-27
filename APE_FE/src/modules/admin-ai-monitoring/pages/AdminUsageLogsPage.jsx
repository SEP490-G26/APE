import { useEffect, useMemo, useState } from "react";
import { Button, Field } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { ROUTES } from "../../../lib/routes";
import { getUsageLogFilterOptions, getUsageLogs } from "../../../services/adminAiMonitoringService";

function formatDateTime(value) {
  return value ? new Date(value).toLocaleString("vi-VN") : "Unknown";
}

function formatVnd(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} VND`;
}

function resolveUsageAmountVnd(log) {
  if (Number(log?.chargedVnd || 0) > 0) {
    return Number(log.chargedVnd);
  }

  if (Number(log?.actualCostVnd || 0) > 0) {
    return Number(log.actualCostVnd);
  }

  return Math.ceil(Number(log?.costUsd || 0) * 26000);
}

function distinctOptions(items, selector) {
  return Array.from(
    new Map(
      items
        .map(selector)
        .filter((item) => item && item.value)
        .map((item) => [item.value, item])
    ).values()
  );
}

export function AdminUsageLogsPage() {
  const [filters, setFilters] = useState({
    triggeredBy: "",
    feature: "",
    provider: "",
    model: "",
    fromDate: "",
    toDate: ""
  });
  const [appliedFilters, setAppliedFilters] = useState({
    triggeredBy: "",
    feature: "",
    provider: "",
    model: "",
    fromDate: "",
    toDate: ""
  });
  const [logs, setLogs] = useState([]);
  const [selectedLogId, setSelectedLogId] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [pagination, setPagination] = useState({ page: 1, limit: 20, total: 0, totalPages: 1 });
  const [filterOptions, setFilterOptions] = useState({
    triggeredBy: [],
    feature: [],
    provider: [],
    model: []
  });

  async function loadLogs(nextFilters = filters, nextPage = pagination.page, nextLimit = pagination.limit) {
    setIsLoading(true);
    try {
      const response = await getUsageLogs({
        ...nextFilters,
        page: nextPage,
        limit: nextLimit
      });
      const nextLogs = Array.isArray(response) ? response : response?.items || [];
      setLogs(nextLogs);
      setSelectedLogId((current) => (nextLogs.some((item) => item.id === current) ? current : nextLogs[0]?.id || ""));
      setPagination({
        page: Number(response?.page ?? nextPage),
        limit: Number(response?.limit ?? nextLimit),
        total: Number(response?.total ?? 0),
        totalPages: Number(response?.totalPages ?? 1)
      });
    } finally {
      setIsLoading(false);
    }
  }

  function handleInstantFilterChange(key, value) {
    setFilters((current) => {
      const next = { ...current, [key]: value };
      setAppliedFilters((applied) => ({ ...applied, [key]: value }));
      setPagination((pageState) => ({ ...pageState, page: 1 }));
      return next;
    });
  }

  useEffect(() => {
    async function loadFilterOptions() {
      try {
        const response = await getUsageLogFilterOptions({
          fromDate: appliedFilters.fromDate || undefined,
          toDate: appliedFilters.toDate || undefined
        });
        setFilterOptions({
          triggeredBy: distinctOptions(response?.triggeredBy || [], (item) => item),
          feature: distinctOptions(response?.feature || [], (item) => item),
          provider: distinctOptions(response?.provider || [], (item) => item),
          model: distinctOptions(response?.model || [], (item) => item)
        });
      } catch {
        setFilterOptions({
          triggeredBy: [],
          feature: [],
          provider: [],
          model: []
        });
      }
    }

    loadFilterOptions();
  }, [appliedFilters.fromDate, appliedFilters.toDate]);

  useEffect(() => {
    loadLogs(appliedFilters, pagination.page, pagination.limit);
  }, [appliedFilters, pagination.page, pagination.limit]);

  const selectedLog = useMemo(
    () => logs.find((item) => item.id === selectedLogId) || null,
    [logs, selectedLogId]
  );

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminAiUsageLogs}
      heroIcon="filter"
      heroTitle="AI Monitoring"
      heroSubtitle="Usage logs"
      title="AI Usage Logs"
      subtitle="Trace each AI call with parsed payload metadata, token usage, and fallback visibility."
    >
      <section className="admin-filter-panel admin-filter-panel--stack admin-usage-log-filter-panel">
        <div className="admin-ai-filter-grid admin-usage-log-filter-grid">
          <Field label="Triggered By">
            <select className="ui-text-input" value={filters.triggeredBy} onChange={(event) => handleInstantFilterChange("triggeredBy", event.target.value)}>
              <option value="">All users</option>
              {filterOptions.triggeredBy.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Feature">
            <select className="ui-text-input" value={filters.feature} onChange={(event) => handleInstantFilterChange("feature", event.target.value)}>
              <option value="">All features</option>
              {filterOptions.feature.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Provider">
            <select className="ui-text-input" value={filters.provider} onChange={(event) => handleInstantFilterChange("provider", event.target.value)}>
              <option value="">All providers</option>
              {filterOptions.provider.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Model">
            <select className="ui-text-input" value={filters.model} onChange={(event) => handleInstantFilterChange("model", event.target.value)}>
              <option value="">All models</option>
              {filterOptions.model.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="From Date">
            <input className="ui-text-input" type="date" value={filters.fromDate} onChange={(event) => setFilters((current) => ({ ...current, fromDate: event.target.value }))} />
          </Field>
          <Field label="To Date">
            <input className="ui-text-input" type="date" value={filters.toDate} onChange={(event) => setFilters((current) => ({ ...current, toDate: event.target.value }))} />
          </Field>
        </div>
        <div className="admin-ai-actions admin-ai-actions--flush admin-usage-log-filter-actions">
          <span className="admin-ai-inline-note">
            {isLoading ? "Loading log entries..." : `Showing ${logs.length} of ${pagination.total} log entries`}
          </span>
          <Button
            variant="primary"
            onClick={() => {
              setAppliedFilters((current) => ({ ...current, fromDate: filters.fromDate, toDate: filters.toDate }));
              setPagination((current) => ({ ...current, page: 1 }));
            }}
            disabled={isLoading}
          >
            {isLoading ? "Loading..." : "Apply Date Filter"}
          </Button>
        </div>
      </section>

      <section className="admin-ai-monitoring-layout">
        <section className="admin-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Log Stream</h2>
              <p>Parsed-first view for operational debugging.</p>
            </div>
          </div>

          <div className="admin-ai-log-list">
            {!logs.length && !isLoading ? <div className="admin-empty-state">No usage logs match the current filters.</div> : null}
            {logs.map((log) => (
              <button
                key={log.id}
                type="button"
                className={`admin-ai-log-row${selectedLogId === log.id ? " is-active" : ""}`}
                onClick={() => setSelectedLogId(log.id)}
              >
                <div>
                  <strong>{log.agentRole || log.agentId}</strong>
                  <p>{log.triggeredByName || log.triggeredByEmail || log.triggeredBy} • {formatDateTime(log.createdAt)}</p>
                </div>
                <div className="admin-ai-log-row__metrics">
                  <span>{log.tokensUsed} tokens</span>
                  <span>${Number(log.costUsd || 0).toFixed(4)}</span>
                  <span>{formatVnd(resolveUsageAmountVnd(log))}</span>
                </div>
              </button>
            ))}
          </div>
          <div className="admin-table-panel__footer">
            <p>{isLoading ? "Syncing usage logs..." : `Showing ${logs.length} of ${pagination.total} usage logs`}</p>
            <div className="admin-pagination">
              <button
                type="button"
                className={pagination.page <= 1 ? "is-disabled" : ""}
                disabled={pagination.page <= 1 || isLoading}
                onClick={() => setPagination((current) => ({ ...current, page: Math.max(1, current.page - 1) }))}
              >
                &#8249;
              </button>
              <button type="button" className="is-active">
                {pagination.page}
              </button>
              <button
                type="button"
                className={pagination.page >= pagination.totalPages ? "is-disabled" : ""}
                disabled={pagination.page >= pagination.totalPages || isLoading}
                onClick={() =>
                  setPagination((current) => ({
                    ...current,
                    page: Math.min(pagination.totalPages || 1, current.page + 1)
                  }))
                }
              >
                &#8250;
              </button>
            </div>
          </div>
        </section>

        <section className="admin-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Parsed Payload</h2>
              <p>{selectedLog ? `${selectedLog.agentId} • ${selectedLog.id}` : "Select a log entry."}</p>
            </div>
          </div>

          {selectedLog ? (
            <div className="admin-ai-detail-panel">
              <div className="admin-ai-meta-stack">
                <span>Feature: {selectedLog.parsedPayload?.feature}</span>
                <span>Step: {selectedLog.parsedPayload?.step}</span>
                <span>Provider: {selectedLog.parsedPayload?.provider}</span>
                <span>Effective model: {selectedLog.parsedPayload?.effectiveModel}</span>
                <span>Fallback used: {selectedLog.parsedPayload?.fallbackUsed ? "Yes" : "No"}</span>
                <span>Total tokens: {selectedLog.parsedPayload?.totalTokens}</span>
              </div>

              <pre className="admin-ai-json-block">{JSON.stringify(selectedLog.parsedPayload, null, 2)}</pre>
            </div>
          ) : (
            <div className="admin-empty-state">Pick a log entry to inspect parsed payload details.</div>
          )}
        </section>
      </section>
    </AdminScaffold>
  );
}
