import { useEffect, useMemo, useRef, useState } from "react";
import { usePayOS } from "@payos/payos-checkout";
import { Button } from "../../components/common";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getAuthSession, updateAuthSessionUser } from "../../lib/storage";
import { refreshAuthSession } from "../../services/authService";
import {
  cancelWalletTopup,
  createWalletTopup,
  getWalletTopupConstraints,
  getWalletTopupDetail,
  getWalletTopupPackages
} from "../../services/walletService";

const TOPUP_TIMEOUT_SECONDS = 5 * 60;
const PAYOS_ELEMENT_ID = "student-payos-embedded-checkout";

function formatVnd(amount) {
  return Number(amount || 0).toLocaleString("vi-VN");
}

function formatCountdown(totalSeconds) {
  const safeSeconds = Math.max(0, totalSeconds);
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

function toSecondsUntil(expiresAt) {
  if (!expiresAt) {
    return TOPUP_TIMEOUT_SECONDS;
  }

  const diffSeconds = Math.ceil((new Date(expiresAt).getTime() - Date.now()) / 1000);
  return Math.max(0, diffSeconds);
}

function getTransactionTone(status) {
  if (status === "success") return "is-green";
  if (status === "error" || status === "cancelled") return "is-red";
  if (status === "pending") return "is-orange";
  return "is-blue";
}

function getTransactionLabel(status, hasActivePayment) {
  if (status === "success") return "Completed";
  if (status === "error") return "Failed";
  if (status === "cancelled") return "Cancelled";
  if (status === "pending" || hasActivePayment) return "Awaiting payment";
  return "Not started";
}

function getResumePaymentId() {
  const params = new URLSearchParams(window.location.search);
  return params.get("paymentId") || "";
}

export function PurchaseCreditsPage() {
  const [constraints, setConstraints] = useState({ minAmountVnd: 10000, maxAmountVnd: 10000000, provider: "PayOS" });
  const [packages, setPackages] = useState([]);
  const [selectedPackageId, setSelectedPackageId] = useState("");
  const [status, setStatus] = useState("idle");
  const [message, setMessage] = useState("");
  const [currentBalance, setCurrentBalance] = useState(() => Number(getAuthSession()?.user?.aiWalletBalanceVnd ?? 0));
  const [remainingSeconds, setRemainingSeconds] = useState(0);
  const [activePayment, setActivePayment] = useState(null);
  const checkoutOpenedRef = useRef(false);

  const selectedPackage = useMemo(
    () => packages.find((item) => item.id === selectedPackageId) || null,
    [packages, selectedPackageId]
  );

  const isAmountValid = useMemo(
    () => selectedPackage && selectedPackage.amountVnd >= constraints.minAmountVnd && selectedPackage.amountVnd <= constraints.maxAmountVnd,
    [constraints.maxAmountVnd, constraints.minAmountVnd, selectedPackage]
  );

  const transactionLabel = useMemo(
    () => getTransactionLabel(status, Boolean(activePayment?.paymentId)),
    [activePayment?.paymentId, status]
  );

  useEffect(() => {
    let isMounted = true;

    Promise.all([
      getWalletTopupConstraints(),
      getWalletTopupPackages(),
      refreshAuthSession().catch(() => null)
    ])
      .then(([constraintData, packageData, session]) => {
        if (!isMounted) {
          return;
        }

        setConstraints(constraintData);
        setPackages(packageData);
        if (packageData.length) {
          setSelectedPackageId(packageData.find((item) => item.isFeatured)?.id || packageData[0].id);
        }

        const nextBalance = Number(session?.user?.aiWalletBalanceVnd ?? 0);
        setCurrentBalance(nextBalance);
        updateAuthSessionUser({ aiWalletBalanceVnd: nextBalance });
      })
      .catch((error) => {
        if (isMounted) {
          setMessage(error.message || "Unable to load top-up settings.");
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    let isMounted = true;
    const paymentId = getResumePaymentId();

    if (!paymentId) {
      return undefined;
    }

    getWalletTopupDetail(paymentId)
      .then((detail) => {
        if (!isMounted) {
          return;
        }

        if ((detail.status === "Pending" || detail.status === 0) && detail.checkoutUrl && toSecondsUntil(detail.expiresAt) > 0) {
          setSelectedPackageId(detail.packageId || "");
          setActivePayment({
            paymentId: detail.id ?? paymentId,
            orderCode: detail.orderCode,
            packageId: detail.packageId,
            packageName: detail.packageName,
            amountVnd: detail.amountVnd,
            checkoutUrl: detail.checkoutUrl,
            qrCode: detail.qrCode,
            status: detail.status,
            createdAt: detail.createdAt,
            expiresAt: detail.expiresAt
          });
          setRemainingSeconds(toSecondsUntil(detail.expiresAt));
          setStatus("pending");
          setMessage("Your pending PayOS transaction has been reopened. Complete the payment within the remaining time.");
          return;
        }

        if (detail.status === "Completed" || detail.status === "Confirmed") {
          setStatus("success");
          setMessage("This transaction has already been completed.");
          return;
        }

        if (detail.status === "Cancelled") {
          setStatus("cancelled");
          setMessage(detail.failureReason || "This transaction was cancelled.");
          return;
        }

        if (detail.status === "Failed" || detail.status === "Expired") {
          setStatus("error");
          setMessage(detail.failureReason || "This transaction is no longer available for payment.");
        }
      })
      .catch((error) => {
        if (isMounted) {
          setStatus("error");
          setMessage(error.message || "Unable to reopen the pending transaction.");
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    if (!activePayment?.paymentId) {
      setRemainingSeconds(0);
      return undefined;
    }

    const intervalId = window.setInterval(() => {
      setRemainingSeconds((current) => (current > 0 ? current - 1 : 0));
    }, 1000);

    return () => window.clearInterval(intervalId);
  }, [activePayment?.paymentId]);

  const payOS = usePayOS({
    RETURN_URL: `${window.location.origin}${ROUTES.studentPurchaseHistory}`,
    ELEMENT_ID: PAYOS_ELEMENT_ID,
    CHECKOUT_URL: activePayment?.checkoutUrl || "",
    embedded: true,
    onSuccess: async () => {
      checkoutOpenedRef.current = false;
      setStatus("success");
      setMessage("Payment completed successfully. Your wallet balance is being updated.");
      await refreshWalletBalance();
      await syncActivePayment("Payment completed successfully.");
    },
    onCancel: async () => {
      checkoutOpenedRef.current = false;
      setStatus("cancelled");
      setMessage("You cancelled the PayOS transaction.");
      await syncActivePayment("You cancelled the PayOS transaction.");
    },
    onExit: async () => {
      checkoutOpenedRef.current = false;
      setStatus("idle");
      setMessage("You closed the payment window.");
      await syncActivePayment("You closed the payment window.");
    }
  });

  useEffect(() => {
    if (!activePayment?.checkoutUrl) {
      checkoutOpenedRef.current = false;
      return undefined;
    }

    const host = document.getElementById(PAYOS_ELEMENT_ID);
    if (!host) {
      return undefined;
    }

    host.innerHTML = "";
    payOS.open();
    checkoutOpenedRef.current = true;

    return () => {
      if (!checkoutOpenedRef.current) {
        return;
      }

      try {
        payOS.exit();
      } catch {
        const container = document.getElementById(PAYOS_ELEMENT_ID);
        if (container) {
          container.innerHTML = "";
        }
      }
      checkoutOpenedRef.current = false;
    };
  }, [activePayment?.checkoutUrl]);

  useEffect(() => {
    if (!activePayment?.paymentId || remainingSeconds > 0) {
      return undefined;
    }

    syncActivePayment("The transaction expired after 5 minutes without PayOS confirmation.");
    return undefined;
  }, [remainingSeconds, activePayment?.paymentId]);

  async function refreshWalletBalance() {
    try {
      const session = await refreshAuthSession();
      const nextBalance = Number(session?.user?.aiWalletBalanceVnd ?? 0);
      setCurrentBalance(nextBalance);
      updateAuthSessionUser({ aiWalletBalanceVnd: nextBalance });
    } catch {
      // leave current balance as-is; purchase history can sync later
    }
  }

  async function syncActivePayment(fallbackMessage) {
    if (!activePayment?.paymentId) {
      return;
    }

    try {
      const detail = await getWalletTopupDetail(activePayment.paymentId);
      setActivePayment((current) => current ? { ...current, status: detail.status } : current);

      if (detail.status === "Completed" || detail.status === "Confirmed") {
        setStatus("success");
        setMessage("Payment completed and the balance has been added to your wallet.");
        setActivePayment(null);
        await refreshWalletBalance();
        return;
      }

      if (detail.status === "Cancelled") {
        setStatus("cancelled");
        setMessage(detail.failureReason || "You cancelled the PayOS transaction.");
        setActivePayment(null);
        return;
      }

      if (detail.status === "Failed" || detail.status === "Expired") {
        setStatus("error");
        setMessage(detail.failureReason || fallbackMessage || "The payment transaction was not completed.");
        setActivePayment(null);
        return;
      }

      setMessage(fallbackMessage || "Waiting for PayOS confirmation.");
    } catch {
      setMessage(fallbackMessage || "Waiting for PayOS confirmation.");
    }
  }

  async function handleConfirm() {
    if (!selectedPackage) {
      setStatus("error");
      setMessage("Choose a wallet package before continuing.");
      return;
    }

    if (!isAmountValid) {
      setStatus("error");
      setMessage(`The top-up amount must be between ${formatVnd(constraints.minAmountVnd)} and ${formatVnd(constraints.maxAmountVnd)} VND.`);
      return;
    }

    try {
      setStatus("pending");
      setMessage("Creating your payment transaction...");
      const result = await createWalletTopup({
        packageId: selectedPackage.id,
        amountVnd: selectedPackage.amountVnd
      });
      setActivePayment(result);
      setRemainingSeconds(toSecondsUntil(result.expiresAt));
      setMessage("The PayOS payment frame is open. Complete the payment within 5 minutes.");
    } catch (error) {
      setStatus("error");
      setActivePayment(null);
      setRemainingSeconds(0);
      setMessage(error.message || "Unable to create the payment transaction.");
    }
  }

  async function handleCancelCheckout() {
    const paymentId = activePayment?.paymentId;

    try {
      payOS.exit();
    } catch {
      const container = document.getElementById(PAYOS_ELEMENT_ID);
      if (container) {
        container.innerHTML = "";
      }
    }

    checkoutOpenedRef.current = false;
    setStatus("pending");
    setMessage("Cancelling the PayOS transaction...");

    if (!paymentId) {
      setStatus("cancelled");
      setMessage("You closed the payment frame.");
      setActivePayment(null);
      setRemainingSeconds(0);
      return;
    }

    try {
      await cancelWalletTopup(paymentId);
      setStatus("cancelled");
      setMessage("You cancelled the PayOS transaction.");
    } catch (error) {
      setStatus("error");
      setMessage(error.message || "Unable to cancel the PayOS transaction.");
      return;
    }

    setActivePayment(null);
    setRemainingSeconds(0);
  }

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentSubscription}
      title="Top Up Wallet"
      subtitle="Add VND to your AI wallet and complete the payment through PayOS."
      actions={<Button variant="ghost" onClick={() => navigateTo(ROUTES.studentPurchaseHistory)}>Payment History</Button>}
    >
      <section className="wallet-topup-shell">
        <article className="wallet-topup-hero">
          <div className="wallet-topup-hero__copy">
            <span className="student-panel__eyebrow">Secure payment</span>
            <h2>Top up once, then keep studying without balance interruptions.</h2>
            <p>
              Choose a package, create the transaction, and complete the embedded PayOS checkout
              in one place.
            </p>
          </div>
          <div className="wallet-topup-hero__stats">
            <div className="wallet-status-card">
              <span>Current balance</span>
              <strong>{formatVnd(currentBalance)} VND</strong>
            </div>
            <div className="wallet-status-card">
              <span>Selected package</span>
              <strong>{selectedPackage?.name || "Choose a package"}</strong>
            </div>
            <div className="wallet-status-card">
              <span>Transaction state</span>
              <strong>{transactionLabel}</strong>
            </div>
          </div>
        </article>

        <article className="student-panel wallet-topup-panel">
          <div className="student-panel__header">
            <div><h2>Choose package</h2><p>Select one package to fund your wallet balance.</p></div>
            <span className={`student-pill ${getTransactionTone(status)}`}>{transactionLabel}</span>
          </div>
          <div className="student-package-grid">
            {packages.map((item) => (
              <button key={item.id} type="button" className={`student-package-card${selectedPackageId === item.id ? " is-selected" : ""}`} onClick={() => setSelectedPackageId(item.id)}>
                <strong>{formatVnd(item.amountVnd)} VND</strong>
              </button>
            ))}
          </div>
          <div className="wallet-topup-summary">
            <div>
              <span>Payment provider</span>
              <strong>{constraints.provider}</strong>
            </div>
            <div>
              <span>Package amount</span>
              <strong>{selectedPackage ? `${formatVnd(selectedPackage.amountVnd)} VND` : "Choose a package"}</strong>
            </div>
          </div>
          <div className="wallet-inline-note">
            Your wallet is updated after PayOS confirms the transaction. Keep this page open until the checkout finishes.
          </div>
          {message ? <div className="student-status-banner">{message}</div> : null}
          <Button variant="primary" onClick={handleConfirm} disabled={!selectedPackage}>Continue to PayOS</Button>
        </article>

        <article className="student-panel wallet-topup-panel">
          <div className="student-panel__header">
            <div><h2>Complete payment</h2><p>Review the status below and finish the embedded checkout.</p></div>
          </div>
          <div className="wallet-topup-steps">
            <div className={`wallet-topup-step ${selectedPackage ? "is-active" : ""}`}>
              <strong>1. Choose package</strong>
              <span>Select one preset wallet package.</span>
            </div>
            <div className={`wallet-topup-step ${activePayment?.paymentId ? "is-active" : ""}`}>
              <strong>2. Create transaction</strong>
              <span>Open the PayOS frame and lock the payment session.</span>
            </div>
            <div className={`wallet-topup-step ${status === "success" ? "is-active" : ""}`}>
              <strong>3. Confirm payment</strong>
              <span>Your wallet balance updates after confirmation.</span>
            </div>
          </div>
          <div className="student-topup-timer" aria-live="polite">
            <span className="student-topup-timer__label">Payment window</span>
            <strong className="student-topup-timer__value">{formatCountdown(remainingSeconds)}</strong>
            <span className="student-topup-timer__hint">
              {activePayment?.paymentId ? "This payment session stays active for up to 5 minutes." : "Create a transaction to start the payment window."}
            </span>
            {activePayment?.paymentId ? (
              <div className="student-topup-timer__actions">
                <Button type="button" variant="ghost" onClick={handleCancelCheckout}>Cancel transaction</Button>
              </div>
            ) : null}
          </div>
          <div id={PAYOS_ELEMENT_ID} className={`student-payos-embed${activePayment?.paymentId ? " is-active" : ""}`} />
          {!activePayment?.paymentId ? (
            <div className="wallet-inline-note">
              Create a transaction first, then the embedded PayOS checkout will appear here.
            </div>
          ) : null}
        </article>
      </section>
    </StudentScaffold>
  );
}
