import { http } from "./http";

function unwrap(payload, fallbackMessage) {
  if (!payload?.success) {
    throw new Error(payload?.error || fallbackMessage);
  }

  return payload.data;
}

function normalizeTimelineItem(item, index = 0) {
  return {
    id: `${item?.periodStart ?? item?.PeriodStart ?? "period"}-${index}`,
    label: item?.label ?? item?.Label ?? "",
    periodStart: item?.periodStart ?? item?.PeriodStart ?? "",
    topupVnd: Number(item?.topupVnd ?? item?.TopupVnd ?? 0),
    aiRevenueVnd: Number(item?.aiRevenueVnd ?? item?.AiRevenueVnd ?? 0),
    aiActualCostVnd: Number(item?.aiActualCostVnd ?? item?.AiActualCostVnd ?? 0),
    aiAbsorbedVnd: Number(item?.aiAbsorbedVnd ?? item?.AiAbsorbedVnd ?? 0),
    practiceSessions: Number(item?.practiceSessions ?? item?.PracticeSessions ?? 0),
    activeStudents: Number(item?.activeStudents ?? item?.ActiveStudents ?? 0),
    averageScore: Number(item?.averageScore ?? item?.AverageScore ?? 0)
  };
}

function normalizeFeature(item, index = 0) {
  return {
    id: `${item?.featureKey ?? item?.FeatureKey ?? "feature"}-${index}`,
    featureKey: item?.featureKey ?? item?.FeatureKey ?? "Unknown",
    transactionCount: Number(item?.transactionCount ?? item?.TransactionCount ?? 0),
    userCount: Number(item?.userCount ?? item?.UserCount ?? 0),
    aiRevenueVnd: Number(item?.aiRevenueVnd ?? item?.AiRevenueVnd ?? 0),
    aiActualCostVnd: Number(item?.aiActualCostVnd ?? item?.AiActualCostVnd ?? 0),
    aiAbsorbedVnd: Number(item?.aiAbsorbedVnd ?? item?.AiAbsorbedVnd ?? 0),
    pricingMarginVnd: Number(item?.pricingMarginVnd ?? item?.PricingMarginVnd ?? 0)
  };
}

function normalizeHighlight(item, index = 0) {
  return {
    id: `${item?.userId ?? item?.UserId ?? "user"}-${index}`,
    userId: item?.userId ?? item?.UserId ?? "",
    fullName: item?.fullName ?? item?.FullName ?? "",
    email: item?.email ?? item?.Email ?? "",
    topupVnd: Number(item?.topupVnd ?? item?.TopupVnd ?? 0),
    aiRevenueVnd: Number(item?.aiRevenueVnd ?? item?.AiRevenueVnd ?? 0),
    aiActualCostVnd: Number(item?.aiActualCostVnd ?? item?.AiActualCostVnd ?? 0),
    aiAbsorbedVnd: Number(item?.aiAbsorbedVnd ?? item?.AiAbsorbedVnd ?? 0),
    aiCalls: Number(item?.aiCalls ?? item?.AiCalls ?? 0),
    practiceSessions: Number(item?.practiceSessions ?? item?.PracticeSessions ?? 0),
    averageScore: Number(item?.averageScore ?? item?.AverageScore ?? 0),
    currentWalletBalanceVnd: Number(item?.currentWalletBalanceVnd ?? item?.CurrentWalletBalanceVnd ?? 0)
  };
}

export async function getAdminDashboardAnalytics(period = "month") {
  const payload = await http(`/api/admin/dashboard/analytics?period=${encodeURIComponent(period)}`);
  const data = unwrap(payload, "Unable to load admin dashboard analytics.");

  return {
    period: data?.period ?? data?.Period ?? "month",
    fromDate: data?.fromDate ?? data?.FromDate ?? "",
    toDate: data?.toDate ?? data?.ToDate ?? "",
    aiFinance: {
      totalTopupVnd: Number(data?.aiFinance?.totalTopupVnd ?? data?.AiFinance?.TotalTopupVnd ?? 0),
      totalAiRevenueVnd: Number(data?.aiFinance?.totalAiRevenueVnd ?? data?.AiFinance?.TotalAiRevenueVnd ?? 0),
      totalAiActualCostVnd: Number(data?.aiFinance?.totalAiActualCostVnd ?? data?.AiFinance?.TotalAiActualCostVnd ?? 0),
      totalAiChargedVnd: Number(data?.aiFinance?.totalAiChargedVnd ?? data?.AiFinance?.TotalAiChargedVnd ?? 0),
      totalAiAbsorbedVnd: Number(data?.aiFinance?.totalAiAbsorbedVnd ?? data?.AiFinance?.TotalAiAbsorbedVnd ?? 0),
      pricingMarginVnd: Number(data?.aiFinance?.pricingMarginVnd ?? data?.AiFinance?.PricingMarginVnd ?? 0),
      realizedNetVnd: Number(data?.aiFinance?.realizedNetVnd ?? data?.AiFinance?.RealizedNetVnd ?? 0),
      activeAiUsers: Number(data?.aiFinance?.activeAiUsers ?? data?.AiFinance?.ActiveAiUsers ?? 0),
      aiTransactionCount: Number(data?.aiFinance?.aiTransactionCount ?? data?.AiFinance?.AiTransactionCount ?? 0),
      aiCallCount: Number(data?.aiFinance?.aiCallCount ?? data?.AiFinance?.AiCallCount ?? 0)
    },
    practice: {
      totalSessions: Number(data?.practice?.totalSessions ?? data?.Practice?.TotalSessions ?? 0),
      completedSessions: Number(data?.practice?.completedSessions ?? data?.Practice?.CompletedSessions ?? 0),
      activeStudents: Number(data?.practice?.activeStudents ?? data?.Practice?.ActiveStudents ?? 0),
      averageScore: Number(data?.practice?.averageScore ?? data?.Practice?.AverageScore ?? 0),
      averageDurationMinutes: Number(data?.practice?.averageDurationMinutes ?? data?.Practice?.AverageDurationMinutes ?? 0)
    },
    timeline: Array.isArray(data?.timeline ?? data?.Timeline) ? (data?.timeline ?? data?.Timeline).map(normalizeTimelineItem) : [],
    aiFeatures: Array.isArray(data?.aiFeatures ?? data?.AiFeatures) ? (data?.aiFeatures ?? data?.AiFeatures).map(normalizeFeature) : [],
    highlights: Array.isArray(data?.highlights ?? data?.Highlights) ? (data?.highlights ?? data?.Highlights).map(normalizeHighlight) : []
  };
}
