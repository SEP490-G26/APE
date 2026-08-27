import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getAuthSession } from "../../lib/storage";
import { getStudentAiBillingTransactions } from "../../services/walletService";

function formatVnd(amount) {
  return Number(amount || 0).toLocaleString("vi-VN");
}

function formatFeatureLabel(featureKey) {
  const key = String(featureKey || "").trim().toLowerCase();

  if (!key) {
    return "Unknown";
  }

  const featureMap = {
    byos: "Upload document",
    byos_ingestion: "Upload document",
    document_ingestion: "Upload document",
    question_generation: "Generate questions",
    generate_questions: "Generate questions",
    question_review: "Review questions",
    reviewer: "Review questions",
    mentor: "AI mentor",
    code_mentor: "AI mentor",
    gatekeeper: "Gatekeeper",
    extraction: "Extract content",
    embedding: "Embedding",
    tagging: "Tagging"
  };

  return featureMap[key] || featureKey;
}

function getChargeStatusLabel(status) {
  const normalized = String(status || "").trim().toLowerCase();

  if (normalized === "charged") return "Charged";
  if (normalized === "refunded") return "Refunded";
  if (normalized === "absorbed") return "System absorbed";
  if (normalized === "waived") return "Waived";
  return status || "Unknown";
}

export function StudentSubscriptionPage() {
  const [balance, setBalance] = useState(0);
  const [usage, setUsage] = useState([]);
  const [transactions, setTransactions] = useState([]);
  const [transactionsError, setTransactionsError] = useState("");

  useEffect(() => {
    const currentWallet = Number(getAuthSession()?.user?.aiWalletBalanceVnd ?? 0);
    setBalance(currentWallet);

    const sevenDaysAgo = new Date();
    sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7);

    Promise.all([
      getStudentAiBillingTransactions({ page: 1, limit: 8 }),
      getStudentAiBillingTransactions({
        page: 1,
        limit: 100,
        fromDate: sevenDaysAgo.toISOString()
      })
    ])
      .then(([latestData, recentUsageData]) => {
        const latestItems = latestData.items || [];
        const recentItems = recentUsageData.items || [];
        const usageMap = new Map();

        recentItems.forEach((item) => {
          const rawFeatureKey = item.featureKey ?? item.FeatureKey ?? "";
          const feature = formatFeatureLabel(rawFeatureKey);
          const currentAmount = usageMap.get(feature) || 0;
          usageMap.set(feature, currentAmount + 1);
        });

        setTransactions(latestItems);
        setUsage(
          [...usageMap.entries()]
            .map(([feature, amount]) => ({ feature, amount }))
            .sort((left, right) => right.amount - left.amount)
        );
        setTransactionsError("");
      })
      .catch((error) => {
        setTransactions([]);
        setUsage([]);
        setTransactionsError(error.message || "Unable to load AI wallet history.");
      });
  }, []);

  const totalRuns = useMemo(
    () => usage.reduce((sum, item) => sum + Number(item.amount || 0), 0),
    [usage]
  );

  const topFeature = useMemo(() => {
    return [...usage].sort((left, right) => Number(right.amount || 0) - Number(left.amount || 0))[0] || null;
  }, [usage]);

  const totalDeducted = useMemo(
    () => transactions.reduce((sum, item) => sum + Number(item.actualDeductedVnd ?? item.ActualDeductedVnd ?? 0), 0),
    [transactions]
  );

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentSubscription}
      title="AI Wallet"
      subtitle="Track your VND balance, recent AI usage, and the next actions you can take."
    >
      <section className="wallet-shell">
        <article className="wallet-hero">
          <div className="wallet-hero__copy">
            <span className="student-panel__eyebrow">Available balance</span>
            <strong className="wallet-hero__value">{formatVnd(balance)} VND</strong>
            <p>
              Your wallet is charged when you use generation, review, mentor, and BYOS workflows.
              Keep enough balance ready so your study sessions are not interrupted.
            </p>
            <div className="wallet-hero__actions">
              <Button variant="primary" onClick={() => navigateTo(ROUTES.purchaseCredits)}>Top up</Button>
              <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentPurchaseHistory)}>View History</Button>
            </div>
          </div>
          <div className="wallet-summary-grid">
            <div className="wallet-summary-card">
              <span>Total AI actions</span>
              <strong>{totalRuns}</strong>
              <small>Tracked across recent wallet usage</small>
            </div>
            <div className="wallet-summary-card">
              <span>Most used flow</span>
              <strong>{topFeature?.feature || "No usage yet"}</strong>
              <small>{topFeature ? `${topFeature.amount} runs in the last 7 days` : "Start a session to see activity"}</small>
            </div>
            <div className="wallet-summary-card">
              <span>Funding status</span>
              <strong>{balance > 0 ? "Ready" : "Low balance"}</strong>
              <small>{balance > 0 ? "You can continue using AI tools" : "Top up before starting the next AI task"}</small>
            </div>
          </div>
        </article>

        <section className="wallet-layout">
          <article className="student-panel wallet-panel">
            <div className="student-panel__header">
              <div>
                <h2>Usage breakdown</h2>
                <p>Last 7 days</p>
              </div>
            </div>
            <div className="wallet-usage-list">
              {!usage.length ? (
                <article className="wallet-history-empty">
                  <strong>No activity in the last 7 days.</strong>
                  <p>Once student AI features are used, the breakdown will appear here.</p>
                </article>
              ) : null}
              {usage.map((item) => (
                <div key={item.feature} className="wallet-usage-row">
                  <div className="wallet-usage-row__copy">
                    <strong>{item.feature}</strong>
                    <span>{item.amount} runs</span>
                  </div>
                  <div className="wallet-usage-row__track">
                    <span style={{ width: `${Math.min(item.amount * 8, 100)}%` }} />
                  </div>
                </div>
              ))}
            </div>
          </article>

          <article className="student-panel wallet-panel">
            <div className="student-panel__header">
              <div>
                <h2>Quick actions</h2>
                <p>Common wallet tasks</p>
              </div>
            </div>
            <div className="wallet-action-list">
              <button type="button" className="wallet-action-card" onClick={() => navigateTo(ROUTES.purchaseCredits)}>
                <strong>Add balance</strong>
                <span>Start a PayOS top-up and fund your wallet in VND.</span>
              </button>
              <button type="button" className="wallet-action-card" onClick={() => navigateTo(ROUTES.studentPurchaseHistory)}>
                <strong>Review payment history</strong>
                <span>Check pending, completed, failed, or expired transactions.</span>
              </button>
              <button type="button" className="wallet-action-card" onClick={() => navigateTo(ROUTES.studentExams)}>
                <strong>Open exam setup</strong>
                <span>Go straight to practice configuration after checking your balance.</span>
              </button>
            </div>
          </article>
        </section>

        <article className="student-panel wallet-insight-panel">
          <div className="student-panel__header">
            <div>
              <h2>Wallet activity notes</h2>
              <p>Helpful context for planning the next study run</p>
            </div>
          </div>
          <div className="wallet-note-grid">
            <div className="wallet-note-card">
              <span>Billing rule</span>
              <strong>Charged per AI workflow</strong>
              <p>Generation, review, mentor, and BYOS tasks deduct directly from your VND wallet balance.</p>
            </div>
            <div className="wallet-note-card">
              <span>Best time to top up</span>
              <strong>Before long sessions</strong>
              <p>Add funds before exams, document processing, or multi-step review flows to avoid interruptions.</p>
            </div>
            <div className="wallet-note-card">
              <span>Next checkpoint</span>
              <strong>{topFeature?.feature || "No recent activity"}</strong>
              <p>{topFeature ? "This is your heaviest wallet usage area right now." : "Once you start using AI tools, the busiest area will show here."}</p>
            </div>
          </div>
        </article>

        <article className="student-panel wallet-insight-panel">
          <div className="student-panel__header">
            <div>
              <h2>AI usage charges</h2>
              <p>Each student AI action that deducts money is saved here.</p>
            </div>
            <div>
              <strong>{formatVnd(totalDeducted)} VND</strong>
              <p>Deducted in the latest records</p>
            </div>
          </div>

          {transactionsError ? <div className="student-status-banner">{transactionsError}</div> : null}

          <div className="student-table">
            <div className="student-table__head">
              <span>Time</span>
              <span>Feature</span>
              <span>Deducted</span>
              <span>Status</span>
              <span>Balance</span>
            </div>
            <div className="student-table__body">
              {!transactions.length && !transactionsError ? (
                <article className="wallet-history-empty">
                  <strong>No AI charges yet.</strong>
                  <p>When a student AI workflow deducts money, the record will appear here.</p>
                </article>
              ) : null}

              {transactions.map((item) => {
                const deducted = Number(item.actualDeductedVnd ?? item.ActualDeductedVnd ?? 0);
                const charged = Number(item.chargedVnd ?? item.ChargedVnd ?? 0);
                const afterBalance = Number(item.balanceAfterVnd ?? item.BalanceAfterVnd ?? 0);
                const rawStatus = item.status ?? item.Status ?? "";
                const statusClass = rawStatus === "refunded"
                  ? "is-green"
                  : rawStatus === "charged"
                    ? "is-orange"
                    : "is-red";

                return (
                  <article key={item.id ?? item.Id} className="student-table__row">
                    <span>{new Date(item.createdAt ?? item.CreatedAt).toLocaleString("vi-VN")}</span>
                    <span>
                      <strong>{formatFeatureLabel(item.featureKey ?? item.FeatureKey)}</strong>
                    </span>
                    <span>
                      <strong>{formatVnd(deducted || charged)} VND</strong>
                    </span>
                    <span className={`student-pill ${statusClass}`}>{getChargeStatusLabel(rawStatus)}</span>
                    <span>{formatVnd(afterBalance)} VND</span>
                  </article>
                );
              })}
            </div>
          </div>
        </article>
      </section>
    </StudentScaffold>
  );
}

