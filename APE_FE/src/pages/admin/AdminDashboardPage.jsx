import { useEffect, useMemo, useState } from "react";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getAdminDashboardAnalytics } from "../../services/adminDashboardService";

const PERIOD_OPTIONS = [
  { value: "day", label: "Day" },
  { value: "week", label: "Week" },
  { value: "month", label: "Month" }
];

function formatVnd(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} VND`;
}

function formatVndCell(value) {
  return Number(value || 0).toLocaleString("vi-VN");
}

function formatNumber(value, options) {
  return Number(value || 0).toLocaleString("vi-VN", options);
}

function formatRangeLabel(fromDate, toDate, period) {
  if (!fromDate || !toDate) {
    return "";
  }

  const from = new Date(fromDate);
  const to = new Date(toDate);

  if (period === "month") {
    return `Year ${from.getFullYear()}`;
  }

  return `${from.toLocaleDateString("en-GB")} - ${to.toLocaleDateString("en-GB")}`;
}

function getMaxValue(items, selector) {
  return Math.max(...items.map((item) => Number(selector(item) || 0)), 1);
}

function isMonthlyLabel(label) {
  return /^\d{2}\/\d{4}$/.test(String(label || ""));
}

function formatChartValue(key, value) {
  return key.toLowerCase().includes("vnd")
    ? formatVnd(value)
    : formatNumber(value, { maximumFractionDigits: 2 });
}

function getBarHeight(value, maxValue) {
  const numericValue = Number(value || 0);
  if (numericValue <= 0 || maxValue <= 0) {
    return "0%";
  }

  const rawHeight = (numericValue / maxValue) * 100;
  return `${Math.max(4, Math.round(rawHeight))}%`;
}

function renderChartLabel(label) {
  if (!isMonthlyLabel(label)) {
    return <strong>{label}</strong>;
  }

  const [month, year] = String(label).split("/");
  return (
    <strong className="admin-report-chart__tick admin-report-chart__tick--month">
      <span>{Number(month)}th</span>
    </strong>
  );
}

function CompactBarChart({ items, firstKey, secondKey, firstLabel, secondLabel }) {
  const maxValue = getMaxValue(items, (item) => Math.max(item[firstKey], item[secondKey]));
  const useScrollableLayout = items.length > 8;
  const hasMonthlyLabels = items.some((item) => isMonthlyLabel(item?.label));
  const columnWidth = useScrollableLayout ? (hasMonthlyLabels ? 56 : 42) : null;
  const chartMinWidth = useScrollableLayout ? items.length * columnWidth : null;

  if (!items.length) {
    return <div className="admin-empty-state">No data available for this period.</div>;
  }

  return (
    <div className={`admin-report-chart${hasMonthlyLabels ? " is-monthly" : ""}`}>
      <div className="admin-report-chart__legend">
        <span><i className="is-primary" />{firstLabel}</span>
        <span><i className="is-secondary" />{secondLabel}</span>
      </div>
      <div className={`admin-report-chart__scroll${useScrollableLayout ? " is-scrollable" : ""}`}>
        <div
          className="admin-report-chart__bars"
          style={useScrollableLayout
            ? {
                minWidth: `${chartMinWidth}px`,
                gridTemplateColumns: `repeat(${items.length}, ${columnWidth}px)`
              }
            : {
                gridTemplateColumns: `repeat(${items.length}, minmax(0, 1fr))`
              }}
        >
          {items.map((item) => (
            <div key={item.id} className="admin-report-chart__group">
              <div className="admin-report-chart__column">
                <span
                  className="is-primary"
                  style={{ height: getBarHeight(item[firstKey], maxValue) }}
                  title={`${firstLabel}: ${formatChartValue(firstKey, item[firstKey])}`}
                />
                <span
                  className="is-secondary"
                  style={{ height: getBarHeight(item[secondKey], maxValue) }}
                  title={`${secondLabel}: ${formatChartValue(secondKey, item[secondKey])}`}
                />
              </div>
              {renderChartLabel(item.label)}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function StatCard({ label, value, note, tone = "default" }) {
  return (
    <article className={`admin-report-stat-card${tone !== "default" ? ` is-${tone}` : ""}`}>
      <span>{label}</span>
      <strong>{value}</strong>
      <p>{note}</p>
    </article>
  );
}

export function AdminDashboardPage({ isAuthenticated = false }) {
  const [period, setPeriod] = useState("month");
  const [analytics, setAnalytics] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadAnalytics() {
      if (!isAuthenticated) {
        return;
      }

      setIsLoading(true);
      setError("");

      try {
        const response = await getAdminDashboardAnalytics(period);
        if (!ignore) {
          setAnalytics(response);
        }
      } catch (loadError) {
        if (!ignore) {
          setAnalytics(null);
          setError(loadError?.message || "Unable to load dashboard analytics.");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadAnalytics();

    return () => {
      ignore = true;
    };
  }, [isAuthenticated, period]);

  const rangeLabel = useMemo(
    () => formatRangeLabel(analytics?.fromDate, analytics?.toDate, period),
    [analytics?.fromDate, analytics?.toDate, period]
  );

  const timelineRows = analytics?.timeline || [];
  const featureRows = analytics?.aiFeatures || [];
  const highlightRows = analytics?.highlights || [];

  const kpis = analytics ? [
    {
      label: "Student Top-up",
      value: formatVnd(analytics.aiFinance.totalTopupVnd),
      note: "Total AI wallet top-up in this period"
    },
    {
      label: "AI Revenue",
      value: formatVnd(analytics.aiFinance.totalAiRevenueVnd),
      note: `${formatNumber(analytics.aiFinance.activeAiUsers)} students used AI`
    },
    {
      label: "Actual AI Cost",
      value: formatVnd(analytics.aiFinance.totalAiActualCostVnd),
      note: `${formatNumber(analytics.aiFinance.aiCallCount)} AI calls`
    },
    {
      label: "Pricing Margin",
      value: formatVnd(analytics.aiFinance.pricingMarginVnd),
      note: "Charged minus actual AI cost",
      tone: analytics.aiFinance.pricingMarginVnd >= 0 ? "success" : "danger"
    },
    {
      label: "System Absorbed Loss",
      value: formatVnd(analytics.aiFinance.totalAiAbsorbedVnd),
      note: "Loss covered when AI runs below wallet balance",
      tone: analytics.aiFinance.totalAiAbsorbedVnd > 0 ? "warning" : "default"
    },
    {
      label: "Practice Sessions",
      value: formatNumber(analytics.practice.totalSessions),
      note: `${formatNumber(analytics.practice.completedSessions)} submitted sessions`
    }
  ] : [];

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminDashboard}
      heroIcon="grid"
      heroTitle="Analytics"
      heroSubtitle="Admin Dashboard"
      title="Admin Dashboard"
      subtitle="Operational reporting powered by live backend data for AI usage, practice activity, and student behavior."
    >
      <section className="admin-report-hero">
        <div>
          <p className="admin-report-hero__eyebrow">Operations Overview</p>
          <h2>AI, top-up, practice, and user highlights</h2>
          <p>{rangeLabel || "Select a period to review day, week, or month analytics."}</p>
        </div>
        <div className="admin-report-hero__actions">
          <div className="admin-report-period-switch" role="tablist" aria-label="Dashboard period">
            {PERIOD_OPTIONS.map((option) => (
              <button
                key={option.value}
                type="button"
                className={period === option.value ? "is-active" : ""}
                onClick={() => setPeriod(option.value)}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>
      </section>

      {error ? (
        <section className="admin-report-alert">
          <strong>Unable to load dashboard.</strong>
          <p>{error}</p>
        </section>
      ) : null}

      <section className="admin-report-stat-grid">
        {isLoading && !analytics ? (
          Array.from({ length: 6 }).map((_, index) => (
            <article key={index} className="admin-report-stat-card is-loading">
              <span>Loading...</span>
              <strong>...</strong>
              <p>Analytics are being prepared.</p>
            </article>
          ))
        ) : (
          kpis.map((item) => (
            <StatCard key={item.label} label={item.label} value={item.value} note={item.note} tone={item.tone} />
          ))
        )}
      </section>

      <section className="admin-report-grid">
        <section className="admin-dashboard-panel admin-report-panel">
          <div className="admin-dashboard-panel__header">
            <div>
              <h2>AI Revenue vs Top-up</h2>
              <p>Compare wallet top-up against actual student AI spending in the selected period.</p>
            </div>
            <div className="admin-dashboard-panel__actions">
              <span className="admin-pill admin-pill--soft">{rangeLabel || period}</span>
            </div>
          </div>
          <CompactBarChart
            items={timelineRows}
            firstKey="topupVnd"
            secondKey="aiRevenueVnd"
            firstLabel="Top-up"
            secondLabel="AI revenue"
          />
        </section>

        <section className="admin-dashboard-panel admin-report-panel">
          <div className="admin-dashboard-panel__header">
            <div>
              <h2>Practice Activity vs AI Loss</h2>
              <p>Track days with both higher practice volume and absorbed AI loss.</p>
            </div>
            <div className="admin-dashboard-panel__actions">
              <button type="button" className="dashboard-inline-link" onClick={() => navigateTo(ROUTES.userManagement)}>
                Open User Registry
              </button>
            </div>
          </div>
          <CompactBarChart
            items={timelineRows.map((item) => ({ ...item, aiLossUnit: item.aiAbsorbedVnd }))}
            firstKey="practiceSessions"
            secondKey="aiLossUnit"
            firstLabel="Practice sessions"
            secondLabel="AI absorbed loss"
          />
        </section>
      </section>

      <section className="admin-report-grid admin-report-grid--tables">
        <section className="admin-dashboard-panel admin-report-panel">
          <div className="admin-dashboard-panel__header">
            <div>
              <h2>AI Finance</h2>
              <p>Revenue, actual cost, pricing margin, and absorbed loss by feature.</p>
            </div>
          </div>
          <div className="admin-report-table-shell">
            <table className="admin-report-table">
              <thead>
                <tr>
                  <th>Feature</th>
                  <th>Transactions</th>
                  <th>Users</th>
                  <th>Revenue (VND)</th>
                  <th>Cost (VND)</th>
                  <th>Margin (VND)</th>
                  <th>Absorbed Loss (VND)</th>
                </tr>
              </thead>
              <tbody>
                {featureRows.length ? featureRows.map((item) => (
                  <tr key={item.id}>
                    <td>{item.featureKey}</td>
                    <td>{formatNumber(item.transactionCount)}</td>
                    <td>{formatNumber(item.userCount)}</td>
                    <td>{formatVndCell(item.aiRevenueVnd)}</td>
                    <td>{formatVndCell(item.aiActualCostVnd)}</td>
                    <td>{formatVndCell(item.pricingMarginVnd)}</td>
                    <td>{formatVndCell(item.aiAbsorbedVnd)}</td>
                  </tr>
                )) : (
                  <tr>
                    <td colSpan="7" className="admin-report-table__empty">No AI transactions in this period.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>

        <section className="admin-dashboard-panel admin-report-panel">
          <div className="admin-dashboard-panel__header">
            <div>
              <h2>Practice</h2>
              <p>Student practice activity in the selected period.</p>
            </div>
          </div>
          <div className="admin-report-practice-summary">
            <article>
              <span>Active Students</span>
              <strong>{formatNumber(analytics?.practice.activeStudents)}</strong>
            </article>
            <article>
              <span>Average Score</span>
              <strong>{formatNumber(analytics?.practice.averageScore, { maximumFractionDigits: 2 })}</strong>
            </article>
            <article>
              <span>Average Duration</span>
              <strong>{formatNumber(analytics?.practice.averageDurationMinutes, { maximumFractionDigits: 1 })} min</strong>
            </article>
          </div>
          <div className="admin-report-table-shell admin-report-table-shell--practice">
            <table className="admin-report-table">
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Top-up (VND)</th>
                  <th>AI Revenue (VND)</th>
                  <th>Practice Sessions</th>
                  <th>Active Students</th>
                </tr>
              </thead>
              <tbody>
                {timelineRows.length ? timelineRows.map((item) => (
                  <tr key={item.id}>
                    <td>{item.label}</td>
                    <td>{formatVndCell(item.topupVnd)}</td>
                    <td>{formatVndCell(item.aiRevenueVnd)}</td>
                    <td>{formatNumber(item.practiceSessions)}</td>
                    <td>{formatNumber(item.activeStudents)}</td>
                  </tr>
                )) : (
                  <tr>
                    <td colSpan="5" className="admin-report-table__empty">No practice activity in this period.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </section>

      <section className="admin-dashboard-panel admin-report-panel">
        <div className="admin-dashboard-panel__header">
          <div>
            <h2>User Highlights</h2>
            <p>Students with notable AI usage, top-up activity, or practice volume.</p>
          </div>
        </div>
        <div className="admin-report-table-shell">
          <table className="admin-report-table">
            <thead>
              <tr>
                <th>Student</th>
                <th>Top-up (VND)</th>
                <th>AI Spend (VND)</th>
                <th>Actual AI Cost (VND)</th>
                <th>Absorbed Loss (VND)</th>
                <th>AI Calls</th>
                <th>Practice</th>
              </tr>
            </thead>
            <tbody>
              {highlightRows.length ? highlightRows.map((item) => (
                <tr key={item.id}>
                  <td>
                    <div className="admin-report-user-cell">
                      <strong>{item.fullName || item.userId}</strong>
                      <span>{item.email || item.userId}</span>
                    </div>
                  </td>
                  <td>{formatVndCell(item.topupVnd)}</td>
                  <td>{formatVndCell(item.aiRevenueVnd)}</td>
                  <td>{formatVndCell(item.aiActualCostVnd)}</td>
                  <td>{formatVndCell(item.aiAbsorbedVnd)}</td>
                  <td>{formatNumber(item.aiCalls)}</td>
                  <td>{formatNumber(item.practiceSessions)}</td>
                </tr>
              )) : (
                <tr>
                  <td colSpan="7" className="admin-report-table__empty">No highlighted users in this period.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </AdminScaffold>
  );
}
