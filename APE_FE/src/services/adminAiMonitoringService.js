import { http } from "./http";

function unwrap(payload, fallbackMessage) {
  if (!payload?.success) {
    throw new Error(payload?.error || fallbackMessage);
  }

  return payload.data;
}

function normalizeUsageLog(item) {
  const payloadData = item?.payloadData ?? item?.PayloadData ?? null;
  const chargeBreakdown = payloadData?.charge_breakdown ?? payloadData?.chargeBreakdown ?? null;
  const actualCostVnd =
    Number(
      item?.actualCostVnd ??
      item?.ActualCostVnd ??
      chargeBreakdown?.actual_cost_vnd ??
      payloadData?.actual_cost_vnd ??
      payloadData?.actualCostVnd ??
      0
    ) || 0;
  const chargedVnd =
    Number(
      item?.chargedVnd ??
      item?.ChargedVnd ??
      chargeBreakdown?.charged_vnd ??
      payloadData?.charged_vnd ??
      payloadData?.chargedVnd ??
      0
    ) || 0;
  const actualDeductedVnd =
    Number(
      item?.actualDeductedVnd ??
      item?.ActualDeductedVnd ??
      chargeBreakdown?.actual_deducted_vnd ??
      payloadData?.actual_deducted_vnd ??
      payloadData?.actualDeductedVnd ??
      0
    ) || 0;

  return {
    id: item?.id ?? item?.Id ?? "",
    triggeredBy: item?.triggeredBy ?? item?.TriggeredBy ?? "",
    triggeredByName: item?.triggeredByName ?? item?.TriggeredByName ?? "",
    triggeredByEmail: item?.triggeredByEmail ?? item?.TriggeredByEmail ?? "",
    agentId: item?.agentId ?? item?.AgentId ?? "",
    agentRole: item?.agentRole ?? item?.AgentRole ?? "",
    creditsDeducted: Number(item?.creditsDeducted ?? item?.CreditsDeducted ?? 0),
    tokensUsed: Number(item?.tokensUsed ?? item?.TokensUsed ?? 0),
    costUsd: Number(item?.costUsd ?? item?.CostUsd ?? 0),
    actualCostVnd,
    chargedVnd,
    actualDeductedVnd,
    createdAt: item?.createdAt ?? item?.CreatedAt ?? "",
    parsedPayload: item?.parsedPayload ?? item?.ParsedPayload ?? {},
    payloadData
  };
}

function normalizeUsageSummary(item, index = 0) {
  return {
    id: item?.id ?? item?.Id ?? `${item?.agentId ?? item?.AgentId ?? "agent"}-${index}`,
    agentId: item?.agentId ?? item?.AgentId ?? "",
    agentRole: item?.agentRole ?? item?.AgentRole ?? "",
    provider: item?.provider ?? item?.Provider ?? "",
    model: item?.model ?? item?.Model ?? "",
    normalizedModelKey: item?.normalizedModelKey ?? item?.NormalizedModelKey ?? "",
    feature: item?.feature ?? item?.Feature ?? "",
    step: item?.step ?? item?.Step ?? "",
    callCount: Number(item?.callCount ?? item?.CallCount ?? 0),
    distinctTriggeredUsers: Number(item?.distinctTriggeredUsers ?? item?.DistinctTriggeredUsers ?? 0),
    totalTokens: Number(item?.totalTokens ?? item?.TotalTokens ?? 0),
    totalCostUsd: Number(item?.totalCostUsd ?? item?.TotalCostUsd ?? 0),
    totalCreditsDeducted: Number(item?.totalCreditsDeducted ?? item?.TotalCreditsDeducted ?? 0),
    fallbackCallCount: Number(item?.fallbackCallCount ?? item?.FallbackCallCount ?? 0),
    avgTokensPerCall: Number(item?.avgTokensPerCall ?? item?.AvgTokensPerCall ?? 0),
    avgCostUsdPerCall: Number(item?.avgCostUsdPerCall ?? item?.AvgCostUsdPerCall ?? 0),
    fallbackRate: Number(item?.fallbackRate ?? item?.FallbackRate ?? 0),
    firstUsedAt: item?.firstUsedAt ?? item?.FirstUsedAt ?? "",
    lastUsedAt: item?.lastUsedAt ?? item?.LastUsedAt ?? ""
  };
}

function buildSearchParams(filters = {}) {
  const searchParams = new URLSearchParams();

  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      searchParams.set(key, String(value));
    }
  });

  return searchParams;
}

function normalizeFilterOption(item, fallbackKeys = []) {
  if (!item) {
    return null;
  }

  if (typeof item === "string") {
    const trimmed = item.trim();
    return trimmed ? { value: trimmed, label: trimmed } : null;
  }

  if (typeof item !== "object") {
    return null;
  }

  const candidates = [
    item.value,
    item.Value,
    ...fallbackKeys.map((key) => item?.[key]).filter(Boolean)
  ];
  const rawValue = candidates.find((value) => typeof value === "string" && value.trim());
  if (!rawValue) {
    return null;
  }

  const rawLabelCandidates = [
    item.label,
    item.Label,
    item.name,
    item.Name,
    item.displayName,
    item.DisplayName,
    item.email,
    item.Email,
    rawValue
  ];
  const rawLabel = rawLabelCandidates.find((value) => typeof value === "string" && value.trim()) ?? rawValue;

  return {
    value: rawValue.trim(),
    label: rawLabel.trim()
  };
}

function normalizeFilterOptionList(items, fallbackKeys = []) {
  if (!Array.isArray(items)) {
    return [];
  }

  return items
    .map((item) => normalizeFilterOption(item, fallbackKeys))
    .filter((item) => item?.value);
}

export async function getUsageLogs(filters = {}) {
  const searchParams = buildSearchParams(filters);
  const payload = await http(`/api/admin/ai/usage-logs${searchParams.size ? `?${searchParams.toString()}` : ""}`);
  const data = unwrap(payload, "Unable to load AI usage logs.");
  const items = Array.isArray(data?.items ?? data?.Items) ? (data?.items ?? data?.Items) : [];

  return {
    items: items.map(normalizeUsageLog),
    total: Number(data?.total ?? data?.Total ?? 0),
    page: Number(data?.page ?? data?.Page ?? 1),
    limit: Number(data?.limit ?? data?.Limit ?? 20),
    totalPages: Number(data?.totalPages ?? data?.TotalPages ?? 1)
  };
}

export async function getUsageLogFilterOptions(filters = {}) {
  const searchParams = buildSearchParams(filters);
  const payload = await http(`/api/admin/ai/usage-logs/filter-options${searchParams.size ? `?${searchParams.toString()}` : ""}`);
  const data = unwrap(payload, "Unable to load AI usage log filter options.");

  return {
    triggeredBy: normalizeFilterOptionList(data?.triggeredBy ?? data?.TriggeredBy, ["email"]),
    feature: normalizeFilterOptionList(data?.features ?? data?.Features, ["feature", "Feature", "key", "Key"]),
    provider: normalizeFilterOptionList(data?.providers ?? data?.Providers, ["provider", "Provider", "key", "Key"]),
    model: normalizeFilterOptionList(data?.models ?? data?.Models, [
      "model",
      "Model",
      "effectiveModel",
      "EffectiveModel",
      "normalizedModelKey",
      "NormalizedModelKey",
      "key",
      "Key"
    ])
  };
}

export async function getUsageSummary(filters = {}) {
  const searchParams = buildSearchParams(filters);
  const payload = await http(`/api/admin/ai/usage-summary${searchParams.size ? `?${searchParams.toString()}` : ""}`);
  const data = unwrap(payload, "Unable to load AI usage summary.");
  const items = Array.isArray(data) ? data : [];
  return items.map(normalizeUsageSummary);
}

function normalizeUserUsageSummary(item, index = 0) {
  return {
    id: `${item?.triggeredBy ?? item?.TriggeredBy ?? "user"}-${index}`,
    triggeredBy: item?.triggeredBy ?? item?.TriggeredBy ?? "",
    provider: item?.provider ?? item?.Provider ?? "",
    model: item?.model ?? item?.Model ?? "",
    normalizedModelKey: item?.normalizedModelKey ?? item?.NormalizedModelKey ?? "",
    feature: item?.feature ?? item?.Feature ?? "",
    step: item?.step ?? item?.Step ?? "",
    callCount: Number(item?.callCount ?? item?.CallCount ?? 0),
    totalTokens: Number(item?.totalTokens ?? item?.TotalTokens ?? 0),
    totalCostUsd: Number(item?.totalCostUsd ?? item?.TotalCostUsd ?? 0),
    totalCreditsDeducted: Number(item?.totalCreditsDeducted ?? item?.TotalCreditsDeducted ?? 0),
    fallbackCallCount: Number(item?.fallbackCallCount ?? item?.FallbackCallCount ?? 0),
    avgTokensPerCall: Number(item?.avgTokensPerCall ?? item?.AvgTokensPerCall ?? 0),
    avgCostUsdPerCall: Number(item?.avgCostUsdPerCall ?? item?.AvgCostUsdPerCall ?? 0),
    fallbackRate: Number(item?.fallbackRate ?? item?.FallbackRate ?? 0),
    firstUsedAt: item?.firstUsedAt ?? item?.FirstUsedAt ?? "",
    lastUsedAt: item?.lastUsedAt ?? item?.LastUsedAt ?? ""
  };
}

export async function getUsageSummaryUsers(filters = {}) {
  const searchParams = buildSearchParams(filters);
  const payload = await http(`/api/admin/ai/usage-summary/users${searchParams.size ? `?${searchParams.toString()}` : ""}`);
  const data = unwrap(payload, "Unable to load AI user usage summary.");
  const items = Array.isArray(data) ? data : [];
  return items.map(normalizeUserUsageSummary);
}

function normalizeBillingSummary(item, index = 0) {
  return {
    id: `${item?.featureKey ?? item?.FeatureKey ?? "feature"}-${item?.status ?? item?.Status ?? "status"}-${index}`,
    featureKey: item?.featureKey ?? item?.FeatureKey ?? "",
    status: item?.status ?? item?.Status ?? "",
    policyVersion: item?.policyVersion ?? item?.PolicyVersion ?? "",
    transactionCount: Number(item?.transactionCount ?? item?.TransactionCount ?? 0),
    distinctUserCount: Number(item?.distinctUserCount ?? item?.DistinctUserCount ?? 0),
    totalReportedCostUsd: Number(item?.totalReportedCostUsd ?? item?.TotalReportedCostUsd ?? 0),
    totalActualCostVnd: Number(item?.totalActualCostVnd ?? item?.TotalActualCostVnd ?? 0),
    totalChargedVnd: Number(item?.totalChargedVnd ?? item?.TotalChargedVnd ?? 0),
    totalActualDeductedVnd: Number(item?.totalActualDeductedVnd ?? item?.TotalActualDeductedVnd ?? 0),
    totalAbsorbedVnd: Number(item?.totalAbsorbedVnd ?? item?.TotalAbsorbedVnd ?? 0),
    firstCreatedAt: item?.firstCreatedAt ?? item?.FirstCreatedAt ?? "",
    lastCreatedAt: item?.lastCreatedAt ?? item?.LastCreatedAt ?? ""
  };
}

export async function getBillingSummary(filters = {}) {
  const searchParams = buildSearchParams(filters);
  const payload = await http(`/api/admin/ai/billing-summary${searchParams.size ? `?${searchParams.toString()}` : ""}`);
  const data = unwrap(payload, "Unable to load AI billing summary.");
  const items = Array.isArray(data) ? data : [];
  return items.map(normalizeBillingSummary);
}

function normalizeBillingDailySummary(item, index = 0) {
  return {
    id: `${item?.date ?? item?.Date ?? "date"}-${index}`,
    date: item?.date ?? item?.Date ?? "",
    transactionCount: Number(item?.transactionCount ?? item?.TransactionCount ?? 0),
    distinctUserCount: Number(item?.distinctUserCount ?? item?.DistinctUserCount ?? 0),
    totalActualCostVnd: Number(item?.totalActualCostVnd ?? item?.TotalActualCostVnd ?? 0),
    totalChargedVnd: Number(item?.totalChargedVnd ?? item?.TotalChargedVnd ?? 0),
    totalActualDeductedVnd: Number(item?.totalActualDeductedVnd ?? item?.TotalActualDeductedVnd ?? 0),
    totalAbsorbedVnd: Number(item?.totalAbsorbedVnd ?? item?.TotalAbsorbedVnd ?? 0)
  };
}

export async function getBillingDailySummary(filters = {}) {
  const searchParams = buildSearchParams(filters);
  const payload = await http(`/api/admin/ai/billing-summary/daily${searchParams.size ? `?${searchParams.toString()}` : ""}`);
  const data = unwrap(payload, "Unable to load AI billing daily summary.");
  const items = Array.isArray(data) ? data : [];
  return items.map(normalizeBillingDailySummary);
}

export async function getAdminMentorFeedbackLogs(filters = {}) {
  const searchParams = buildSearchParams(filters);
  const payload = await http(`/api/admin/ai/mentor-feedbacks${searchParams.size ? `?${searchParams.toString()}` : ""}`);
  const data = unwrap(payload, "Unable to load mentor feedback logs.");
  return Array.isArray(data) ? data : [];
}
