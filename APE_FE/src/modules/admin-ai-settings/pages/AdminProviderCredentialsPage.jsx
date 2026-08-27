import { useEffect, useMemo, useState } from "react";
import { Button, Field } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { ROUTES } from "../../../lib/routes";
import { useInFlightGuard } from "../../../shared/guards";
import { ConfirmModal, InFlightNotice, ToastNotification } from "../../../shared/ui";
import {
  createProviderCredentials,
  deleteProviderCredentials,
  getProviderCredentials,
  reloadProviders,
  updateProviderCredentials
} from "../../../services/adminAiSettingsService";

function formatDateTime(value) {
  if (!value) {
    return "Unknown";
  }

  return new Date(value).toLocaleString("vi-VN");
}

export function AdminProviderCredentialsPage() {
  const [providers, setProviders] = useState([]);
  const [selectedProvider, setSelectedProvider] = useState("OpenAI");
  const [draft, setDraft] = useState({
    enabled: false,
    api_key: "",
    base_url: "",
    api_version: "",
    timeout_seconds: 45,
    headers: "{}"
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isReloading, setIsReloading] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
  const [toast, setToast] = useState(null);

  useInFlightGuard(isSaving || isReloading, "Saving AI settings...");

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  function showToast(type, message) {
    setToast({ type, message });
  }

  useEffect(() => {
    let ignore = false;

    async function loadProviders() {
      setIsLoading(true);
      try {
        const response = await getProviderCredentials();
        if (ignore) {
          return;
        }

        setProviders(response);
        setSelectedProvider((current) => response.find((item) => item.provider === current)?.provider || response[0]?.provider || "OpenAI");
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadProviders();
    return () => {
      ignore = true;
    };
  }, []);

  const selectedRecord = useMemo(
    () => providers.find((item) => item.provider === selectedProvider) || null,
    [providers, selectedProvider]
  );

  useEffect(() => {
    if (!selectedRecord) {
      return;
    }

    setDraft({
      enabled: Boolean(selectedRecord.enabled),
      api_key: "",
      base_url: selectedRecord.base_url || "",
      api_version: selectedRecord.api_version || "",
      timeout_seconds: selectedRecord.timeout_seconds || 45,
      headers: selectedRecord.headers || "{}"
    });
    setIsCreating(false);
  }, [selectedRecord]);

  function handleCreateNew() {
    setIsCreating(true);
    setSelectedProvider("");
    setDraft({
      provider: "",
      enabled: true,
      api_key: "",
      base_url: "",
      api_version: "",
      timeout_seconds: 45,
      headers: "{}"
    });
    setToast(null);
  }

  async function handleSave() {
    if (!draft || (!selectedRecord && !isCreating)) {
      return;
    }

    if (isCreating && !draft.provider?.trim()) {
      showToast("error", "Provider name is required.");
      return;
    }

    setToast(null);
    setIsSaving(true);
    try {
      const savedRecord = isCreating
        ? await createProviderCredentials(draft)
        : await updateProviderCredentials(selectedRecord.provider, draft);

      setProviders((current) => (
        isCreating
          ? [...current, savedRecord].sort((left, right) => left.provider.localeCompare(right.provider))
          : current.map((item) => (item.provider === savedRecord.provider ? savedRecord : item))
      ));
      setSelectedProvider(savedRecord.provider);
      setIsCreating(false);
      showToast("success", `${savedRecord.provider} settings saved.`);
    } catch (error) {
      showToast("error", error.message || "Could not save provider settings.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleReload() {
    setToast(null);
    setIsReloading(true);
    try {
      const result = await reloadProviders();
      const response = await getProviderCredentials();
      setProviders(response);
      showToast("success", result?.reloaded ? `Providers reloaded at ${formatDateTime(result.reloaded_at)}.` : "Providers reloaded.");
    } catch (error) {
      showToast("error", error.message || "Could not reload providers.");
    } finally {
      setIsReloading(false);
    }
  }

  async function handleDelete() {
    if (!selectedRecord || isCreating) {
      return;
    }

    setToast(null);
    setIsDeleting(true);
    try {
      await deleteProviderCredentials(selectedRecord.provider);
      const nextProviders = providers.filter((item) => item.provider !== selectedRecord.provider);
      setProviders(nextProviders);
      setSelectedProvider(nextProviders[0]?.provider || "");
      setIsDeleteConfirmOpen(false);
      showToast("success", `${selectedRecord.provider} deleted.`);
    } catch (error) {
      showToast("error", error.message || "Could not delete provider.");
    } finally {
      setIsDeleting(false);
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminAiProviders}
      heroIcon="spark"
      heroTitle="AI Settings"
      heroSubtitle="Provider credentials"
      title="Provider Credentials"
      subtitle="Manage API keys, base URLs, timeouts, and custom headers for each runtime provider."
    >
      <ToastNotification toast={toast} />

      {isSaving || isReloading ? (
        <InFlightNotice
          title={isSaving ? "Saving AI settings" : "Reloading providers"}
          description="Your current form is locked until the request finishes."
        />
      ) : null}

      <section className="admin-ai-settings-grid">
        <section className="admin-table-panel admin-ai-list-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Providers</h2>
              <p>Current runtime credential status.</p>
            </div>
            <div className="admin-ai-actions__group">
              <Button variant="secondary" onClick={handleReload} disabled={isSaving || isReloading || isDeleting}>
                {isReloading ? "Reloading..." : "Reload Providers"}
              </Button>
              <Button variant="secondary" onClick={handleCreateNew} disabled={isSaving || isReloading || isDeleting}>
                Add Provider
              </Button>
            </div>
          </div>

          <div className="admin-ai-card-list">
            {isLoading ? <div className="admin-empty-state">Loading providers...</div> : null}
            {!isLoading && !providers.length ? <div className="admin-empty-state">No providers available.</div> : null}
            {providers.map((provider) => (
              <button
                key={provider.provider}
                type="button"
                className={`admin-ai-select-card${selectedProvider === provider.provider ? " is-active" : ""}`}
                onClick={() => setSelectedProvider(provider.provider)}
              >
                <div className="admin-ai-select-card__header">
                  <strong>{provider.provider}</strong>
                  <span className={`admin-ai-badge ${provider.enabled ? "is-good" : "is-neutral"}`}>
                    {provider.enabled ? "Enabled" : "Disabled"}
                  </span>
                </div>
                <p className="admin-ai-select-card__endpoint">{provider.base_url}</p>
                <div className="admin-ai-meta-row">
                  <span className="admin-ai-meta-pill">{provider.has_api_key ? provider.api_key_masked : "No API key"}</span>
                  <span className="admin-ai-meta-pill">Timeout {provider.timeout_seconds}s</span>
                </div>
              </button>
            ))}
          </div>
        </section>

        <section className="admin-table-panel admin-ai-form-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>{isCreating ? "New Provider" : selectedRecord?.provider || "Provider detail"}</h2>
              <p>
                {isCreating
                  ? "Create a new provider runtime entry."
                  : `Last synced ${formatDateTime(selectedRecord?.lastUpdated)} by ${selectedRecord?.updatedBy || "Unknown"}.`}
              </p>
            </div>
          </div>

          <div className="admin-ai-form-grid">
            <Field label="Provider Name" hint="Use a stable provider key because agent routing records may reference it.">
              <input
                className="ui-text-input"
                value={draft.provider || selectedRecord?.provider || ""}
                onChange={(event) => setDraft((current) => ({ ...current, provider: event.target.value }))}
                disabled={!isCreating}
                placeholder="OpenAI"
              />
            </Field>

            <Field label="Enabled">
              <label className="admin-ai-toggle">
                <input
                  type="checkbox"
                  checked={draft.enabled}
                  onChange={(event) => setDraft((current) => ({ ...current, enabled: event.target.checked }))}
                />
                <span>{draft.enabled ? "Provider active" : "Provider disabled"}</span>
              </label>
            </Field>

            <Field label="New API Key" hint={selectedRecord?.has_api_key ? `Current key: ${selectedRecord.api_key_masked}` : "No key stored yet."}>
              <input
                className="ui-text-input"
                type="password"
                value={draft.api_key}
                onChange={(event) => setDraft((current) => ({ ...current, api_key: event.target.value }))}
                placeholder="Paste a new API key to rotate credentials"
              />
            </Field>

            <Field label="Base URL">
              <input
                className="ui-text-input"
                value={draft.base_url}
                onChange={(event) => setDraft((current) => ({ ...current, base_url: event.target.value }))}
              />
            </Field>

            <Field label="API Version">
              <input
                className="ui-text-input"
                value={draft.api_version}
                onChange={(event) => setDraft((current) => ({ ...current, api_version: event.target.value }))}
                placeholder="v1beta"
              />
            </Field>

            <Field label="Timeout Seconds">
              <input
                className="ui-text-input"
                type="number"
                min="1"
                value={draft.timeout_seconds}
                onChange={(event) => setDraft((current) => ({ ...current, timeout_seconds: Number(event.target.value) }))}
              />
            </Field>

            <Field
              label="Custom Headers"
              className="admin-ai-field--full"
              hint={'Store as a JSON object, for example {"X-Workspace":"ape-prod"}.'}
            >
              <textarea
                className="admin-ai-textarea"
                value={draft.headers}
                onChange={(event) => setDraft((current) => ({ ...current, headers: event.target.value }))}
              />
            </Field>
          </div>

          <div className="admin-ai-actions">
            <span />
            <div className="admin-ai-actions__group">
              <Button
                variant="secondary"
                onClick={() => setIsDeleteConfirmOpen(true)}
                disabled={isSaving || isReloading || isDeleting || !selectedRecord || isCreating}
              >
                {isDeleting ? "Deleting..." : "Delete"}
              </Button>
              <Button variant="primary" onClick={handleSave} disabled={isSaving || isReloading || isDeleting || (!selectedRecord && !isCreating)}>
                {isSaving ? "Saving..." : isCreating ? "Create" : "Save"}
              </Button>
            </div>
          </div>
        </section>
      </section>

      <ConfirmModal
        open={isDeleteConfirmOpen}
        title="Delete provider"
        message={selectedRecord ? `Delete provider ${selectedRecord.provider}?` : ""}
        confirmLabel="Delete"
        isConfirming={isDeleting}
        onCancel={() => !isDeleting && setIsDeleteConfirmOpen(false)}
        onConfirm={handleDelete}
      />
    </AdminScaffold>
  );
}
