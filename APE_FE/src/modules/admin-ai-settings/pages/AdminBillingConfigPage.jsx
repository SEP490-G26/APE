import { useEffect, useState } from "react";
import { Button, Field } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { ROUTES } from "../../../lib/routes";
import { useInFlightGuard } from "../../../shared/guards";
import { InFlightNotice, ToastNotification } from "../../../shared/ui";
import { getBillingConfig, updateBillingConfig } from "../../../services/adminAiSettingsService";

function formatDateTime(value) {
  if (!value) {
    return "Unknown";
  }

  return new Date(value).toLocaleString("vi-VN");
}

export function AdminBillingConfigPage() {
  const [billingDraft, setBillingDraft] = useState({
    usdToVndRate: 26000,
    minBalanceVnd: 1000,
    chargeMultiplier: 1.5,
    version: "default",
    updatedBy: "",
    lastUpdated: null
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [toast, setToast] = useState(null);

  useInFlightGuard(isSaving, "Saving AI billing config...");

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  useEffect(() => {
    let ignore = false;

    async function loadBillingConfig() {
      setIsLoading(true);
      try {
        const response = await getBillingConfig();
        if (!ignore) {
          setBillingDraft(response);
        }
      } catch (error) {
        if (!ignore) {
          setToast({ type: "error", message: error.message || "Could not load billing config." });
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadBillingConfig();
    return () => {
      ignore = true;
    };
  }, []);

  async function handleSaveBilling() {
    setToast(null);
    setIsSaving(true);

    try {
      const saved = await updateBillingConfig(billingDraft);
      setBillingDraft({
        usdToVndRate: Number(saved?.usdToVndRate ?? saved?.UsdToVndRate ?? billingDraft.usdToVndRate),
        minBalanceVnd: Number(saved?.minBalanceVnd ?? saved?.MinBalanceVnd ?? billingDraft.minBalanceVnd),
        chargeMultiplier: Number(saved?.chargeMultiplier ?? saved?.ChargeMultiplier ?? billingDraft.chargeMultiplier),
        version: saved?.version ?? saved?.Version ?? billingDraft.version,
        updatedBy: saved?.updatedBy ?? saved?.UpdatedBy ?? "",
        lastUpdated: saved?.lastUpdated ?? saved?.LastUpdated ?? null
      });
      setToast({ type: "success", message: "Billing config saved." });
    } catch (error) {
      setToast({ type: "error", message: error.message || "Could not save billing config." });
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminAiBillingConfig}
      heroIcon="spark"
      heroTitle="AI Settings"
      heroSubtitle="Wallet billing rules"
      title="AI Wallet Billing"
      subtitle="Configure exchange rate, minimum wallet balance, and the billing multiplier used when charging AI usage."
    >
      <ToastNotification toast={toast} />

      {isSaving ? (
        <InFlightNotice
          title="Saving billing config"
          description="The billing settings are locked until the update finishes."
        />
      ) : null}

      <section className="admin-table-panel admin-ai-form-panel">
        <div className="admin-ai-panel__header">
          <div>
            <h2>Billing Configuration</h2>
            <p>
              {isLoading
                ? "Loading billing config..."
                : `Version ${billingDraft.version || "default"} - updated ${formatDateTime(billingDraft.lastUpdated)} by ${billingDraft.updatedBy || "Unknown"}.`}
            </p>
          </div>
        </div>

        <div className="admin-ai-form-grid">
          <Field label="USD to VND Rate">
            <input
              className="ui-text-input"
              type="number"
              min="1"
              step="1"
              value={billingDraft.usdToVndRate}
              onChange={(event) => setBillingDraft((current) => ({ ...current, usdToVndRate: Number(event.target.value) }))}
              disabled={isLoading || isSaving}
            />
          </Field>

          <Field label="Minimum Balance VND">
            <input
              className="ui-text-input"
              type="number"
              min="0"
              step="1000"
              value={billingDraft.minBalanceVnd}
              onChange={(event) => setBillingDraft((current) => ({ ...current, minBalanceVnd: Number(event.target.value) }))}
              disabled={isLoading || isSaving}
            />
          </Field>

          <Field label="Charge Multiplier" hint="Charged VND = ReportedCostUsd x ChargeMultiplier x USD to VND Rate.">
            <input
              className="ui-text-input"
              type="number"
              min="0.01"
              step="0.01"
              value={billingDraft.chargeMultiplier}
              onChange={(event) => setBillingDraft((current) => ({ ...current, chargeMultiplier: Number(event.target.value) }))}
              disabled={isLoading || isSaving}
            />
          </Field>
        </div>

        <div className="admin-ai-actions">
          <span className="admin-ai-inline-note">
            This setting is applied globally for all student AI wallet charges.
          </span>
          <div className="admin-ai-actions__group">
            <Button variant="primary" onClick={handleSaveBilling} disabled={isLoading || isSaving}>
              {isSaving ? "Saving..." : "Save Billing Config"}
            </Button>
          </div>
        </div>
      </section>
    </AdminScaffold>
  );
}
