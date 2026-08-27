import { useEffect, useMemo, useState } from "react";
import { Button, Field } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { ROUTES } from "../../../lib/routes";
import { useInFlightGuard } from "../../../shared/guards";
import { ConfirmModal, InFlightNotice, ToastNotification } from "../../../shared/ui";
import {
  createAiAgent,
  getAiAgents,
  getProviderCredentials,
  getProviderModels,
  updateAiAgent
} from "../../../services/adminAiSettingsService";

function formatDateTime(value) {
  return value ? new Date(value).toLocaleString("vi-VN") : "Unknown";
}

export function AdminAiAgentsPage() {
  const [agents, setAgents] = useState([]);
  const [providers, setProviders] = useState([]);
  const [providerModels, setProviderModels] = useState({});
  const [selectedRole, setSelectedRole] = useState("");
  const [draft, setDraft] = useState(null);
  const [isCreating, setIsCreating] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingModels, setIsLoadingModels] = useState(false);
  const [isLoadingFallbackModels, setIsLoadingFallbackModels] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isDisableConfirmOpen, setIsDisableConfirmOpen] = useState(false);
  const [toast, setToast] = useState(null);

  useInFlightGuard(isSaving, "Saving AI settings...");

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

    async function bootstrap() {
      setIsLoading(true);
      try {
        const [agentResponse, providerResponse] = await Promise.all([
          getAiAgents(),
          getProviderCredentials()
        ]);

        if (ignore) {
          return;
        }

        setAgents(agentResponse);
        setProviders(providerResponse);
        setSelectedRole(agentResponse[0]?.agent_role || "");
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    bootstrap();
    return () => {
      ignore = true;
    };
  }, []);

  const selectedAgent = useMemo(
    () => agents.find((item) => item.agent_role === selectedRole) || null,
    [agents, selectedRole]
  );

  const providerOptions = useMemo(() => {
    return Array.from(new Set([
      ...providers.map((item) => item.provider).filter(Boolean),
      ...agents.map((item) => item.provider).filter(Boolean),
      ...agents.map((item) => item.fallback_provider).filter(Boolean)
    ]));
  }, [agents, providers]);

  const modelOptions = useMemo(() => {
    const provider = draft?.provider;
    if (!provider) {
      return [];
    }

    return Array.from(new Set([
      ...(providerModels[provider] || []),
      ...agents.filter((item) => item.provider === provider).map((item) => item.model_name).filter(Boolean)
    ]));
  }, [agents, draft?.provider, providerModels]);

  const fallbackModelOptions = useMemo(() => {
    const provider = draft?.fallback_provider;
    if (!provider) {
      return [];
    }

    return Array.from(new Set([
      ...(providerModels[provider] || []),
      ...agents.filter((item) => item.provider === provider).map((item) => item.model_name).filter(Boolean),
      ...agents.filter((item) => item.fallback_provider === provider).map((item) => item.fallback_model_name).filter(Boolean)
    ]));
  }, [agents, draft?.fallback_provider, providerModels]);

  useEffect(() => {
    const provider = draft?.provider?.trim();
    if (!provider || providerModels[provider]) {
      return;
    }

    let ignore = false;

    async function loadModels() {
      setIsLoadingModels(true);
      try {
        const models = await getProviderModels(provider);
        if (!ignore) {
          setProviderModels((current) => ({ ...current, [provider]: models }));
        }
      } finally {
        if (!ignore) {
          setIsLoadingModels(false);
        }
      }
    }

    loadModels();
    return () => {
      ignore = true;
    };
  }, [draft?.provider, providerModels]);

  useEffect(() => {
    const provider = draft?.fallback_provider?.trim();
    if (!provider || providerModels[provider]) {
      return;
    }

    let ignore = false;

    async function loadFallbackModels() {
      setIsLoadingFallbackModels(true);
      try {
        const models = await getProviderModels(provider);
        if (!ignore) {
          setProviderModels((current) => ({ ...current, [provider]: models }));
        }
      } finally {
        if (!ignore) {
          setIsLoadingFallbackModels(false);
        }
      }
    }

    loadFallbackModels();
    return () => {
      ignore = true;
    };
  }, [draft?.fallback_provider, providerModels]);

  useEffect(() => {
    if (!draft?.provider) {
      return;
    }

    if (draft.model_name && modelOptions.includes(draft.model_name)) {
      return;
    }

    setDraft((current) => {
      if (!current?.provider) {
        return current;
      }

      if (current.model_name && modelOptions.includes(current.model_name)) {
        return current;
      }

      return {
        ...current,
        model_name: modelOptions[0] || current.model_name || ""
      };
    });
  }, [draft?.provider, draft?.model_name, modelOptions]);

  useEffect(() => {
    if (!draft?.fallback_provider) {
      return;
    }

    if (draft.fallback_model_name && fallbackModelOptions.includes(draft.fallback_model_name)) {
      return;
    }

    setDraft((current) => {
      if (!current?.fallback_provider) {
        return current;
      }

      if (current.fallback_model_name && fallbackModelOptions.includes(current.fallback_model_name)) {
        return current;
      }

      return {
        ...current,
        fallback_model_name: fallbackModelOptions[0] || current.fallback_model_name || ""
      };
    });
  }, [draft?.fallback_provider, draft?.fallback_model_name, fallbackModelOptions]);

  useEffect(() => {
    if (!selectedAgent) {
      if (!isCreating) {
        setDraft(null);
      }
      return;
    }

    setDraft({
      agent_role: selectedAgent.agent_role,
      provider: selectedAgent.provider,
      model_name: selectedAgent.model_name,
      max_tokens: selectedAgent.max_tokens,
      temperature: selectedAgent.temperature,
      is_enabled: selectedAgent.is_enabled,
      fallback_provider: selectedAgent.fallback_provider || "",
      fallback_model_name: selectedAgent.fallback_model_name || "",
      notes: selectedAgent.notes || ""
    });
    setIsCreating(false);
  }, [isCreating, selectedAgent]);

  function handleCreateNew() {
    setIsCreating(true);
    setSelectedRole("");
    setDraft({
      agent_role: "",
      provider: providerOptions[0] || "",
      model_name: "",
      max_tokens: 3200,
      temperature: 0.3,
      is_enabled: true,
      fallback_provider: "",
      fallback_model_name: "",
      notes: ""
    });
    setToast(null);
  }

  async function handleSave() {
    if (!draft) {
      return;
    }

    if (!draft.agent_role?.trim()) {
      showToast("error", "Agent role is required.");
      return;
    }

    if (!draft.provider?.trim()) {
      showToast("error", "Provider is required.");
      return;
    }

    if (!draft.model_name?.trim()) {
      showToast("error", "Model is required.");
      return;
    }

    if (draft.fallback_provider?.trim() && !draft.fallback_model_name?.trim()) {
      showToast("error", "Fallback model is required.");
      return;
    }

    setToast(null);
    setIsSaving(true);
    try {
      const payload = {
        ...draft,
        agent_role: draft.agent_role.trim()
      };

      const savedAgent = isCreating
        ? await createAiAgent(payload)
        : await updateAiAgent(selectedAgent.agent_role, payload);

      setAgents((current) => {
        if (isCreating) {
          return [savedAgent, ...current];
        }

        return current.map((item) => (item.agent_role === savedAgent.agent_role ? savedAgent : item));
      });
      setSelectedRole(savedAgent.agent_role);
      setIsCreating(false);
      showToast("success", `${savedAgent.agent_role} saved.`);
    } catch (error) {
      showToast("error", error.message || "Could not save agent.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDisable() {
    if (!selectedAgent || isCreating || !draft) {
      return;
    }

    setToast(null);
    setIsSaving(true);
    try {
      const updatedAgent = await updateAiAgent(selectedAgent.agent_role, {
        ...draft,
        agent_role: selectedAgent.agent_role,
        is_enabled: false
      });
      setAgents((current) => current.map((item) => (item.agent_role === updatedAgent.agent_role ? updatedAgent : item)));
      setDraft((current) => (current ? { ...current, is_enabled: false } : current));
      showToast("success", `${selectedAgent.agent_role} disabled.`);
      setIsDisableConfirmOpen(false);
    } catch (error) {
      showToast("error", error.message || "Could not disable agent.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminAiAgents}
      heroIcon="user"
      heroTitle="AI Settings"
      heroSubtitle="Agent runtime assignment"
      title="AI Agents Runtime Assignment"
      subtitle="Assign provider, model, fallback, and runtime notes for each operational AI role."
    >
      <ToastNotification toast={toast} />

      {isSaving ? (
        <InFlightNotice
          title="Saving AI settings"
          description="The selected agent is locked while the mutation finishes."
        />
      ) : null}

      <section className="admin-ai-settings-grid">
        <section className="admin-table-panel admin-ai-list-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Agents</h2>
              <p>Primary runtime, fallback, and enable state.</p>
            </div>
            <Button variant="secondary" onClick={handleCreateNew} disabled={isSaving}>
              Add Agent
            </Button>
          </div>

          <div className="admin-ai-card-list">
            {isLoading ? <div className="admin-empty-state">Loading agents...</div> : null}
            {!isLoading && !agents.length ? <div className="admin-empty-state">No agents found.</div> : null}
            {agents.map((agent) => (
              <button
                key={agent.id}
                type="button"
                className={`admin-ai-select-card${selectedRole === agent.agent_role ? " is-active" : ""}`}
                onClick={() => setSelectedRole(agent.agent_role)}
              >
                <div className="admin-ai-select-card__header">
                  <strong>{agent.agent_role}</strong>
                  <span className={`admin-ai-badge ${agent.is_enabled ? "is-good" : "is-neutral"}`}>
                    {agent.is_enabled ? "Enabled" : "Disabled"}
                  </span>
                </div>
                <p>{agent.provider} / {agent.model_name}</p>
                <div className="admin-ai-meta-row">
                  <span>{agent.has_fallback_configured ? "Fallback On" : "No Fallback"}</span>
                  <span>{agent.is_enabled ? "DB Routed" : "Disabled"}</span>
                </div>
              </button>
            ))}
          </div>
        </section>

        <section className="admin-table-panel admin-ai-form-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>{isCreating ? "Add Agent" : selectedAgent?.agent_role || "Agent detail"}</h2>
              <p>
                {isCreating
                  ? "Create a new AI agent runtime assignment."
                  : `Last updated ${formatDateTime(selectedAgent?.updated_at)} by ${selectedAgent?.updated_by || "Unknown"}.`}
              </p>
            </div>
          </div>

          {draft ? (
            <>
              <div className="admin-ai-form-grid admin-ai-form-grid--two">
                <Field label="Agent Role">
                  <input
                    className="ui-text-input"
                    value={draft.agent_role}
                    onChange={(event) => setDraft((current) => ({ ...current, agent_role: event.target.value }))}
                    disabled={!isCreating}
                  />
                </Field>

                <Field label="Provider">
                  <select
                    className="admin-ai-select"
                    value={draft.provider}
                    onChange={(event) => setDraft((current) => ({ ...current, provider: event.target.value, model_name: "" }))}
                  >
                    <option value="">Select provider</option>
                    {providerOptions.map((provider) => (
                      <option key={provider} value={provider}>
                        {provider}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field label="Model">
                  <select
                    className="admin-ai-select"
                    value={draft.model_name}
                    onChange={(event) => setDraft((current) => ({ ...current, model_name: event.target.value }))}
                    disabled={!draft.provider || isLoadingModels}
                  >
                    <option value="">{isLoadingModels ? "Loading models..." : "Select model"}</option>
                    {modelOptions.map((model) => (
                      <option key={model} value={model}>
                        {model}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field label="Max Tokens">
                  <input
                    className="ui-text-input"
                    type="number"
                    min="0"
                    value={draft.max_tokens}
                    onChange={(event) => setDraft((current) => ({ ...current, max_tokens: Number(event.target.value) }))}
                  />
                </Field>

                <Field label="Temperature">
                  <input
                    className="ui-text-input"
                    type="number"
                    min="0"
                    max="1"
                    step="0.05"
                    value={draft.temperature}
                    onChange={(event) => setDraft((current) => ({ ...current, temperature: Number(event.target.value) }))}
                  />
                </Field>

                <Field label="Enabled">
                  <label className="admin-ai-toggle">
                    <input
                      type="checkbox"
                      checked={draft.is_enabled}
                      onChange={(event) => setDraft((current) => ({ ...current, is_enabled: event.target.checked }))}
                    />
                    <span>{draft.is_enabled ? "Agent active" : "Agent disabled"}</span>
                  </label>
                </Field>

                <Field label="Fallback Provider">
                  <select
                    className="admin-ai-select"
                    value={draft.fallback_provider}
                    onChange={(event) => setDraft((current) => ({ ...current, fallback_provider: event.target.value, fallback_model_name: "" }))}
                  >
                    <option value="">No fallback</option>
                    {providerOptions.map((provider) => (
                      <option key={provider} value={provider}>
                        {provider}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field label="Fallback Model">
                  <select
                    className="admin-ai-select"
                    value={draft.fallback_model_name}
                    onChange={(event) => setDraft((current) => ({ ...current, fallback_model_name: event.target.value }))}
                    disabled={!draft.fallback_provider || isLoadingFallbackModels}
                  >
                    <option value="">{isLoadingFallbackModels ? "Loading models..." : "Select fallback model"}</option>
                    {fallbackModelOptions.map((model) => (
                      <option key={model} value={model}>
                        {model}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field label="Notes" className="admin-ai-field--full">
                  <textarea
                    className="admin-ai-textarea"
                    value={draft.notes}
                    onChange={(event) => setDraft((current) => ({ ...current, notes: event.target.value }))}
                  />
                </Field>
              </div>

              <div className="admin-ai-actions">
                <span className="admin-ai-inline-note">
                  Configure runtime and fallback for this agent. Active execution should follow DB-first routing from BE.
                </span>
                <div className="admin-ai-actions__group">
                  <Button
                    variant="secondary"
                    onClick={() => setIsDisableConfirmOpen(true)}
                    disabled={isSaving || isCreating || !draft.is_enabled}
                  >
                    Disable
                  </Button>
                  <Button variant="primary" onClick={handleSave} disabled={isSaving}>
                    {isSaving ? "Saving..." : "Save"}
                  </Button>
                </div>
              </div>
            </>
          ) : (
            <div className="admin-empty-state">Pick an agent to inspect its runtime assignment.</div>
          )}
        </section>
      </section>

      <ConfirmModal
        open={isDisableConfirmOpen}
        title="Disable agent"
        message={selectedAgent ? `Disable agent ${selectedAgent.agent_role}?` : ""}
        confirmLabel="Disable"
        isConfirming={isSaving}
        onCancel={() => !isSaving && setIsDisableConfirmOpen(false)}
        onConfirm={handleDisable}
      />
    </AdminScaffold>
  );
}
