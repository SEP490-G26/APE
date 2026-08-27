import { http } from "./http";

function wait(ms = 180) {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

const PROVIDER_ORDER = ["OpenAI", "DeepSeek", "Gemini", "Cohere"];

let providerCredentials = [
  {
    provider: "OpenAI",
    enabled: true,
    has_api_key: true,
    api_key_masked: "sk-...9x3A",
    base_url: "https://api.openai.com/v1",
    api_version: "",
    timeout_seconds: 60,
    headers: '{"X-Workspace":"ape-prod"}',
    updatedBy: "admin@ape.local",
    lastUpdated: "2026-07-28T09:12:00+07:00"
  },
  {
    provider: "DeepSeek",
    enabled: true,
    has_api_key: true,
    api_key_masked: "sk-...ad52",
    base_url: "https://api.deepseek.com",
    api_version: "",
    timeout_seconds: 45,
    headers: "{}",
    updatedBy: "admin@ape.local",
    lastUpdated: "2026-07-27T18:35:00+07:00"
  },
  {
    provider: "Cohere",
    enabled: false,
    has_api_key: false,
    api_key_masked: "",
    base_url: "https://api.cohere.com/v1",
    api_version: "",
    timeout_seconds: 45,
    headers: "{}",
    updatedBy: "system",
    lastUpdated: "2026-07-26T11:20:00+07:00"
  }
];

let aiAgents = [
  {
    id: "agent-fe-generator",
    agent_role: "fe_generator",
    provider: "OpenAI",
    model_name: "gpt-5.4-mini",
    max_tokens: 3200,
    temperature: 0.3,
    credit_cost: 6,
    is_enabled: true,
    fallback_provider: "DeepSeek",
    fallback_model_name: "DeepSeek-V4-Flash-0731",
    has_fallback_configured: true,
    updated_by: "admin@ape.local",
    updated_at: "2026-07-28T09:14:00+07:00",
    notes: "Primary runtime for FE generation."
  },
  {
    id: "agent-pe-generator",
    agent_role: "pe_generator",
    provider: "OpenAI",
    model_name: "gpt-5.4",
    max_tokens: 6400,
    temperature: 0.45,
    credit_cost: 12,
    is_enabled: true,
    fallback_provider: "DeepSeek",
    fallback_model_name: "DeepSeek-V4-Pro",
    has_fallback_configured: true,
    updated_by: "admin@ape.local",
    updated_at: "2026-07-27T17:35:00+07:00",
    notes: "High-context coding generation path."
  },
  {
    id: "agent-code-mentor",
    agent_role: "code_mentor",
    provider: "OpenAI",
    model_name: "gpt-5.4",
    max_tokens: 5400,
    temperature: 0.25,
    credit_cost: 8,
    is_enabled: true,
    fallback_provider: "DeepSeek",
    fallback_model_name: "DeepSeek-V4-Pro",
    has_fallback_configured: true,
    updated_by: "system",
    updated_at: "2026-07-26T11:05:00+07:00",
    notes: "Primary mentor runtime with DeepSeek fallback."
  }
];

let runtimeHealth = [];

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function unwrapData(payload) {
  return payload?.data ?? payload;
}

function stringifyHeaders(headers) {
  return JSON.stringify(headers || {}, null, 2);
}

function parseHeaders(headers) {
  if (!headers?.trim()) {
    return {};
  }

  const parsed = JSON.parse(headers);
  if (!parsed || Array.isArray(parsed) || typeof parsed !== "object") {
    throw new Error("Custom Headers must be a JSON object.");
  }

  return Object.fromEntries(
    Object.entries(parsed).map(([key, value]) => [key, value == null ? "" : String(value)])
  );
}

function normalizeProviderRecord(providerName, value, metadata = {}) {
  return {
    provider: providerName,
    enabled: Boolean(value?.enabled),
    has_api_key: Boolean(value?.hasApiKey ?? value?.has_api_key),
    api_key_masked: value?.apiKeyMasked ?? value?.api_key_masked ?? "",
    base_url: value?.baseUrl ?? value?.base_url ?? "",
    api_version: value?.apiVersion ?? value?.api_version ?? "",
    timeout_seconds: value?.timeoutSeconds ?? value?.timeout_seconds ?? 0,
    headers: stringifyHeaders(value?.headers),
    updatedBy: metadata.updatedBy ?? value?.updatedBy ?? value?.updated_by ?? "",
    lastUpdated: metadata.lastUpdated ?? value?.lastUpdated ?? value?.last_updated ?? null
  };
}

function normalizeProviderCredentials(payload) {
  const data = unwrapData(payload);
  if (Array.isArray(data)) {
    return data
      .map((item) => normalizeProviderRecord(item.provider, item))
      .sort((left, right) => {
        const leftIndex = PROVIDER_ORDER.indexOf(left.provider);
        const rightIndex = PROVIDER_ORDER.indexOf(right.provider);
        const normalizedLeft = leftIndex === -1 ? Number.MAX_SAFE_INTEGER : leftIndex;
        const normalizedRight = rightIndex === -1 ? Number.MAX_SAFE_INTEGER : rightIndex;
        return normalizedLeft - normalizedRight || left.provider.localeCompare(right.provider);
      });
  }

  if (!data || typeof data !== "object") {
    return clone(providerCredentials);
  }

  const metadata = {
    updatedBy: data.updatedBy ?? data.updated_by ?? "",
    lastUpdated: data.lastUpdated ?? data.last_updated ?? null
  };

  return PROVIDER_ORDER
    .filter((providerName) => data[providerName] || data[providerName.toLowerCase()])
    .map((providerName) => normalizeProviderRecord(
      providerName,
      data[providerName] ?? data[providerName.toLowerCase()],
      metadata
    ));
}

function buildProviderUpdatePayload(providerName, draft) {
  return {
    provider: providerName,
    enabled: Boolean(draft.enabled),
    apiKey: draft.api_key?.trim() || null,
    baseUrl: draft.base_url?.trim() || "",
    apiVersion: draft.api_version?.trim() || "",
    timeoutSeconds: Number(draft.timeout_seconds) || 0,
    headers: parseHeaders(draft.headers)
  };
}

function normalizeAiAgent(value) {
  return {
    id: value?.id ?? "",
    agent_role: value?.agentRole ?? value?.agent_role ?? "",
    provider: value?.provider ?? "",
    model_name: value?.modelName ?? value?.model_name ?? "",
    max_tokens: value?.maxTokens ?? value?.max_tokens ?? 0,
    temperature: value?.temperature ?? 0,
    credit_cost: value?.creditCost ?? value?.credit_cost ?? 0,
    is_enabled: Boolean(value?.isEnabled ?? value?.is_enabled),
    fallback_provider: value?.fallbackProvider ?? value?.fallback_provider ?? "",
    fallback_model_name: value?.fallbackModelName ?? value?.fallback_model_name ?? "",
    has_fallback_configured: Boolean(value?.hasFallbackConfigured ?? value?.has_fallback_configured),
    updated_by: value?.updatedBy ?? value?.updated_by ?? "",
    updated_at: value?.updatedAt ?? value?.updated_at ?? null,
    notes: value?.notes ?? ""
  };
}

const PROVIDER_MODEL_FALLBACK = {
  OpenAI: ["gpt-5.4", "gpt-5.4-mini", "gpt-4o", "gpt-4.1", "gpt-4.1-mini"],
  DeepSeek: ["DeepSeek-V4-Pro", "DeepSeek-V4-Flash-0731"],
  Gemini: ["gemini-3.5-flash", "gemini-3.1-flash-lite", "gemini-3-flash-preview", "gemini-2.5-pro", "gemini-2.5-flash"],
  Cohere: ["command-r7b-12-2024", "embed-multilingual-v3.0"]
};

export async function getProviderModels(providerName) {
  if (!providerName?.trim()) {
    return [];
  }

  try {
    const payload = await http(`/api/admin/ai/providers/${encodeURIComponent(providerName)}/models`);
    const data = unwrapData(payload);
    return Array.isArray(data)
      ? data.map((item) => item.id ?? item.label).filter(Boolean)
      : [];
  } catch {
    await wait(180);
    return PROVIDER_MODEL_FALLBACK[providerName] || [];
  }
}

export async function getBillingConfig() {
  const payload = await http("/api/admin/ai/billing-config");
  const data = unwrapData(payload);
  return {
    usdToVndRate: Number(data?.usdToVndRate ?? data?.UsdToVndRate ?? 26000),
    minBalanceVnd: Number(data?.minBalanceVnd ?? data?.MinBalanceVnd ?? 1000),
    chargeMultiplier: Number(data?.chargeMultiplier ?? data?.ChargeMultiplier ?? 1.5),
    settingName: data?.settingName ?? data?.SettingName ?? "AI_VND_BILLING",
    version: data?.version ?? data?.Version ?? "default",
    updatedBy: data?.updatedBy ?? data?.UpdatedBy ?? "",
    lastUpdated: data?.lastUpdated ?? data?.LastUpdated ?? null
  };
}

export async function updateBillingConfig(draft) {
  const payload = await http("/api/admin/ai/billing-config", {
    method: "PUT",
    body: JSON.stringify({
      usdToVndRate: Number(draft.usdToVndRate || 0),
      minBalanceVnd: Number(draft.minBalanceVnd || 0),
      chargeMultiplier: Number(draft.chargeMultiplier || 0)
    })
  });

  return unwrapData(payload);
}

function syncRuntimeHealth() {
  runtimeHealth = aiAgents.map((agent) => {
    const primaryProvider = providerCredentials.find((item) => item.provider === agent.provider);
    const fallbackProvider = providerCredentials.find((item) => item.provider === agent.fallback_provider);
    const primaryReady = Boolean(agent.is_enabled && primaryProvider?.enabled && primaryProvider?.has_api_key);
    const fallbackConfigured = Boolean(agent.fallback_provider && agent.fallback_model_name);
    const fallbackReady = Boolean(fallbackConfigured && fallbackProvider?.enabled && fallbackProvider?.has_api_key);

    let runtimeStatus = "disabled";
    if (agent.is_enabled && primaryReady && fallbackReady) {
      runtimeStatus = "ready_with_fallback";
    } else if (agent.is_enabled && primaryReady && fallbackConfigured && !fallbackReady) {
      runtimeStatus = "ready_primary_only_fallback_not_ready";
    } else if (agent.is_enabled && primaryReady) {
      runtimeStatus = "ready_primary_only";
    } else if (agent.is_enabled) {
      runtimeStatus = "primary_not_ready";
    }

    const warnings = [];
    if (agent.is_enabled && !primaryProvider?.has_api_key) {
      warnings.push("Primary provider is missing API key.");
    }
    if (agent.is_enabled && fallbackConfigured && !fallbackReady) {
      warnings.push("Fallback runtime is configured but not ready.");
    }
    if (agent.is_enabled && !fallbackConfigured) {
      warnings.push("No fallback runtime configured for this agent.");
    }

    return {
      agent_role: agent.agent_role,
      agent_enabled: agent.is_enabled,
      provider: agent.provider,
      model_name: agent.model_name,
      primary_provider_enabled: Boolean(primaryProvider?.enabled),
      primary_provider_has_api_key: Boolean(primaryProvider?.has_api_key),
      primary_runtime_ready: primaryReady,
      fallback_provider: agent.fallback_provider,
      fallback_model_name: agent.fallback_model_name,
      fallback_configured: fallbackConfigured,
      fallback_provider_enabled: Boolean(fallbackProvider?.enabled),
      fallback_provider_has_api_key: Boolean(fallbackProvider?.has_api_key),
      fallback_runtime_ready: fallbackReady,
      runtime_status: runtimeStatus,
      has_warnings: warnings.length > 0,
      warnings,
      updated_at: new Date().toISOString()
    };
  });
}

syncRuntimeHealth();

export async function getProviderCredentials() {
  try {
    const payload = await http("/api/admin/ai/provider-credentials");
    const normalized = normalizeProviderCredentials(payload);
    providerCredentials = normalized;
    return normalized;
  } catch {
    await wait();
    return clone(providerCredentials);
  }
}

export async function updateProviderCredentials(providerName, draft) {
  try {
    const payload = await http("/api/admin/ai/provider-credentials", {
      method: "PUT",
      body: JSON.stringify(buildProviderUpdatePayload(providerName, draft))
    });
    const savedRecord = normalizeProviderRecord(providerName, unwrapData(payload));
    providerCredentials = providerCredentials.map((item) => (item.provider === providerName ? savedRecord : item));
    syncRuntimeHealth();
    return savedRecord;
  } catch {
    await wait(280);
    providerCredentials = providerCredentials.map((item) => (
      item.provider === providerName
        ? {
          ...item,
          enabled: Boolean(draft.enabled),
          base_url: draft.base_url,
          api_version: draft.api_version || "",
          timeout_seconds: Number(draft.timeout_seconds) || 0,
          headers: draft.headers,
          has_api_key: item.has_api_key || Boolean(draft.api_key),
          api_key_masked: draft.api_key ? `${draft.api_key.slice(0, 3)}...${draft.api_key.slice(-2)}` : item.api_key_masked,
          lastUpdated: new Date().toISOString()
        }
        : item
    ));
    syncRuntimeHealth();
    return clone(providerCredentials.find((item) => item.provider === providerName));
  }
}

export async function createProviderCredentials(draft) {
  const providerName = draft.provider?.trim();
  if (!providerName) {
    throw new Error("Provider name is required.");
  }

  try {
    const payload = await http("/api/admin/ai/provider-credentials", {
      method: "POST",
      body: JSON.stringify(buildProviderUpdatePayload(providerName, draft))
    });
    const savedRecord = normalizeProviderRecord(providerName, unwrapData(payload));
    providerCredentials = normalizeProviderCredentials([...providerCredentials, savedRecord]);
    syncRuntimeHealth();
    return savedRecord;
  } catch {
    await wait(280);

    if (providerCredentials.some((item) => item.provider.toLowerCase() === providerName.toLowerCase())) {
      throw new Error("Provider already exists.");
    }

    const savedRecord = {
      provider: providerName,
      enabled: Boolean(draft.enabled),
      has_api_key: Boolean(draft.api_key),
      api_key_masked: draft.api_key ? `${draft.api_key.slice(0, 3)}...${draft.api_key.slice(-2)}` : "",
      base_url: draft.base_url || "",
      api_version: draft.api_version || "",
      timeout_seconds: Number(draft.timeout_seconds) || 0,
      headers: draft.headers || "{}",
      updatedBy: "local-admin",
      lastUpdated: new Date().toISOString()
    };

    providerCredentials = normalizeProviderCredentials([...providerCredentials, savedRecord]);
    syncRuntimeHealth();
    return clone(savedRecord);
  }
}

export async function deleteProviderCredentials(providerName) {
  try {
    const payload = await http(`/api/admin/ai/provider-credentials/${encodeURIComponent(providerName)}`, {
      method: "DELETE"
    });
    providerCredentials = providerCredentials.filter((item) => item.provider !== providerName);
    syncRuntimeHealth();
    return unwrapData(payload);
  } catch {
    await wait(220);

    const existingProvider = providerCredentials.find((item) => item.provider === providerName);
    if (!existingProvider) {
      throw new Error("Provider not found.");
    }

    providerCredentials = providerCredentials.filter((item) => item.provider !== providerName);
    syncRuntimeHealth();
    return { deleted: true, provider: providerName };
  }
}

export async function reloadProviders() {
  try {
    const payload = await http("/api/admin/ai/providers/reload", {
      method: "POST"
    });
    return unwrapData(payload);
  } catch {
    await wait(220);
    const reloaded_at = new Date().toISOString();
    providerCredentials = providerCredentials.map((item) => ({
      ...item,
      lastUpdated: reloaded_at
    }));
    syncRuntimeHealth();
    return { reloaded: true, reloaded_at };
  }
}

export async function getAiAgents() {
  try {
    const payload = await http("/api/admin/ai/agents");
    const data = Array.isArray(payload) ? payload : unwrapData(payload)?.items || unwrapData(payload) || aiAgents;
    const normalized = Array.isArray(data) ? data.map(normalizeAiAgent) : clone(aiAgents);
    aiAgents = normalized;
    syncRuntimeHealth();
    return normalized;
  } catch {
    await wait();
    return clone(aiAgents);
  }
}

export async function updateAiAgent(agentRole, draft) {
  try {
    const payload = await http(`/api/admin/ai/agents/${agentRole}`, {
      method: "PUT",
      body: JSON.stringify(draft)
    });
    const normalized = normalizeAiAgent(unwrapData(payload));
    aiAgents = aiAgents.map((item) => (item.agent_role === agentRole ? normalized : item));
    syncRuntimeHealth();
    return normalized;
  } catch {
    await wait(280);
    aiAgents = aiAgents.map((item) => (
      item.agent_role === agentRole
        ? {
          ...item,
          ...draft,
          has_fallback_configured: Boolean(draft.fallback_provider && draft.fallback_model_name),
          updated_by: "local-admin",
          updated_at: new Date().toISOString()
        }
        : item
    ));
    syncRuntimeHealth();
    return clone(aiAgents.find((item) => item.agent_role === agentRole));
  }
}

export async function createAiAgent(draft) {
  const agentRole = draft?.agent_role?.trim();
  if (!agentRole) {
    throw new Error("Agent role is required.");
  }

  try {
    const payload = await http(`/api/admin/ai/agents/${encodeURIComponent(agentRole)}`, {
      method: "PUT",
      body: JSON.stringify(draft)
    });
    const normalized = normalizeAiAgent(unwrapData(payload));
    aiAgents = [normalized, ...aiAgents.filter((item) => item.agent_role !== normalized.agent_role)];
    syncRuntimeHealth();
    return normalized;
  } catch {
    await wait(280);
    if (!draft?.agent_role) {
      throw new Error("Agent role is required.");
    }

    if (aiAgents.some((item) => item.agent_role === draft.agent_role)) {
      throw new Error("Agent role already exists.");
    }

    const createdAgent = {
      id: `agent-${draft.agent_role.replace(/_/g, "-")}`,
      agent_role: draft.agent_role,
      provider: draft.provider,
      model_name: draft.model_name,
      max_tokens: draft.max_tokens,
      temperature: draft.temperature,
      credit_cost: 0,
      is_enabled: draft.is_enabled,
      fallback_provider: draft.fallback_provider || "",
      fallback_model_name: draft.fallback_model_name || "",
      has_fallback_configured: Boolean(draft.fallback_provider && draft.fallback_model_name),
      updated_by: "local-admin",
      updated_at: new Date().toISOString(),
      notes: draft.notes || ""
    };

    aiAgents = [createdAgent, ...aiAgents];
    syncRuntimeHealth();
    return clone(createdAgent);
  }
}

export async function deleteAiAgent(agentRole) {
  throw new Error("The backend does not support deleting AI agents yet. Disable the agent instead.");
}

export async function getRuntimeHealth() {
  try {
    const payload = await http("/api/admin/ai/runtime-health");
    return Array.isArray(payload) ? payload : unwrapData(payload)?.items || unwrapData(payload) || runtimeHealth;
  } catch {
    syncRuntimeHealth();
    await wait();
    return clone(runtimeHealth);
  }
}
