import { useEffect, useMemo, useState } from "react";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES } from "../../lib/routes";
import { getWalletTopupHistory } from "../../services/walletService";
import { refreshAuthSession } from "../../services/authService";
import { updateAuthSessionUser } from "../../lib/storage";

const STATUS_LABELS = {
  Pending: "Pending",
  Completed: "Completed",
  Failed: "Failed",
  Expired: "Expired",
  Cancelled: "Cancelled",
  Confirmed: "Completed",
  0: "Pending",
  1: "Completed",
  2: "Failed",
  3: "Cancelled",
  4: "Expired"
};

function normalizeStatus(status) {
  if (status === null || status === undefined) {
    return "Pending";
  }

  const raw = String(status).trim();
  if (raw === "0") return "Pending";
  if (raw === "1") return "Completed";
  if (raw === "2") return "Failed";
  if (raw === "3") return "Cancelled";
  if (raw === "4") return "Expired";
  if (raw === "Confirmed") return "Completed";
  return raw;
}

function getStatusLabel(status) {
  const normalized = normalizeStatus(status);
  return STATUS_LABELS[normalized] || normalized;
}

function getStatusClass(status) {
  const normalized = normalizeStatus(status);
  if (normalized === "Completed") {
    return "is-green";
  }

  if (normalized === "Pending") {
    return "is-orange";
  }

  if (normalized === "Cancelled" || normalized === "Expired") {
    return "is-orange";
  }

  return "is-red";
}

function formatCountdown(expiresAt) {
  if (!expiresAt) {
    return "";
  }

  const diffMs = new Date(expiresAt).getTime() - Date.now();
  if (diffMs <= 0) {
    return "Expired";
  }

  const totalSeconds = Math.floor(diffMs / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")} remaining`;
}

function isPendingAndPayable(item) {
  return normalizeStatus(item?.status) === "Pending" &&
    Boolean(item?.checkoutUrl) &&
    Boolean(item?.expiresAt) &&
    new Date(item.expiresAt).getTime() > Date.now();
}

export function PurchaseHistoryPage() {
  const [filters, setFilters] = useState({ type: "All", status: "All" });
  const [rows, setRows] = useState([]);
  const [error, setError] = useState("");
  const [nowTick, setNowTick] = useState(Date.now());

  const hasPendingRows = useMemo(() => rows.some((item) => normalizeStatus(item.status) === "Pending"), [rows]);

  useEffect(() => {
    let isMounted = true;

    async function loadHistory() {
      try {
        const [data, session] = await Promise.all([
          getWalletTopupHistory(filters.status),
          refreshAuthSession().catch(() => null)
        ]);

        if (!isMounted) {
          return;
        }

        setRows(data);
        setError("");

        const nextBalance = Number(session?.user?.aiWalletBalanceVnd ?? NaN);
        if (Number.isFinite(nextBalance)) {
          updateAuthSessionUser({ aiWalletBalanceVnd: nextBalance });
        }
      } catch (nextError) {
        if (isMounted) {
          setRows([]);
          setError(nextError.message || "Unable to load payment history.");
        }
      }
    }

    loadHistory();

    return () => {
      isMounted = false;
    };
  }, [filters]);

  useEffect(() => {
    const hasCompleted = rows.some((item) => normalizeStatus(item.status) === "Completed");

    if (!hasCompleted) {
      return undefined;
    }

    let ignore = false;

    async function syncBalanceOnce() {
      try {
        const session = await refreshAuthSession();
        if (ignore) {
          return;
        }

        const nextBalance = Number(session?.user?.aiWalletBalanceVnd ?? NaN);
        if (Number.isFinite(nextBalance)) {
          updateAuthSessionUser({ aiWalletBalanceVnd: nextBalance });
        }
      } catch {
        // Keep current session state. Payment history polling should not log the user out.
      }
    }

    syncBalanceOnce();

    return () => {
      ignore = true;
    };
  }, [rows]);

  useEffect(() => {
    if (!hasPendingRows) {
      return undefined;
    }

    const intervalId = window.setInterval(() => {
      setNowTick(Date.now());
    }, 1000);

    return () => window.clearInterval(intervalId);
  }, [hasPendingRows]);

  useEffect(() => {
    if (!hasPendingRows) {
      return undefined;
    }

    const intervalId = window.setInterval(async () => {
      try {
        const data = await getWalletTopupHistory(filters.status);

        setRows(data);
      } catch {
        // keep current UI state; next poll can recover
      }
    }, 5000);

    return () => window.clearInterval(intervalId);
  }, [filters.status, hasPendingRows]);

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentPurchaseHistory}
      title="Payment History"
      subtitle="Track PayOS transactions and the status of wallet balance updates."
    >
      <section className="wallet-history-shell">
        <article className="wallet-history-filter">
          <div>
            <span className="student-panel__eyebrow">Transactions</span>
            <h2>Review your payment activity</h2>
            <p>Use the status filter to focus on payments that still need attention.</p>
          </div>
          <div className="student-filter-strip">
            <label className="student-filter-field">
              <span>Type</span>
              <select value={filters.type} onChange={(event) => setFilters((current) => ({ ...current, type: event.target.value }))} disabled>
                <option value="All">Top-up</option>
              </select>
            </label>
            <label className="student-filter-field">
              <span>Status</span>
              <select value={filters.status} onChange={(event) => setFilters((current) => ({ ...current, status: event.target.value }))}>
                <option value="All">All</option>
                <option value="Completed">Completed</option>
                <option value="Pending">Pending</option>
                <option value="Failed">Failed</option>
                <option value="Expired">Expired</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </label>
          </div>
        </article>

        <section className="student-panel student-table-panel wallet-history-table">
          {error ? <div className="student-status-banner">{error}</div> : null}
          <div className="student-table">
            <div className="student-table__head">
              <span>OrderCode</span>
              <span>Provider</span>
              <span>Amount</span>
              <span>Status</span>
              <span>Date</span>
              <span>Action</span>
            </div>
            <div className="student-table__body">
              {!rows.length && !error ? (
                <article className="wallet-history-empty">
                  <strong>No transactions found.</strong>
                  <p>Completed and pending top-ups will appear here after you start using the wallet.</p>
                </article>
              ) : null}
              {rows.map((item) => (
                <article key={item.id} className="student-table__row">
                  <span>
                    {item.orderCode}
                    {item.packageName ? (
                      <>
                        <br />
                        <small>{item.packageName}</small>
                      </>
                    ) : null}
                    {normalizeStatus(item.status) === "Pending" ? (
                      <>
                        <br />
                        <small key={nowTick}>{formatCountdown(item.expiresAt)}</small>
                      </>
                    ) : item.failureReason ? (
                      <>
                        <br />
                        <small>{item.failureReason}</small>
                      </>
                    ) : null}
                  </span>
                  <span>{item.provider}</span>
                  <strong>{Number(item.amountVnd || 0).toLocaleString("vi-VN")} VND</strong>
                  <span className={`wallet-history-status ${getStatusClass(item.status)}`}>
                    {getStatusLabel(item.status)}
                  </span>
                  <span>{new Date(item.paidAt || item.createdAt).toLocaleString("vi-VN")}</span>
                  <span>
                    {isPendingAndPayable(item) ? (
                      <button
                        type="button"
                        className="student-link-button"
                        onClick={() => window.location.assign(`${ROUTES.purchaseCredits}?paymentId=${encodeURIComponent(item.id)}`)}
                      >
                        Pay now
                      </button>
                    ) : (
                      <span className="student-table__muted">-</span>
                    )}
                  </span>
                </article>
              ))}
            </div>
          </div>
        </section>
      </section>
    </StudentScaffold>
  );
}

