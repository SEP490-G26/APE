import { http } from "./http";

function unwrap(payload, fallbackMessage) {
  if (!payload?.success) {
    throw new Error(payload?.error || fallbackMessage);
  }

  return payload.data;
}

export async function getWalletTopupConstraints() {
  const payload = await http("/api/student/wallet/topups/constraints");
  return unwrap(payload, "Unable to load top-up settings.");
}

export async function getWalletTopupPackages() {
  const payload = await http("/api/student/wallet/topups/packages");
  const data = unwrap(payload, "Unable to load wallet packages.");
  return Array.isArray(data)
    ? data.map((item) => ({
      id: item?.id ?? item?.Id ?? "",
      code: item?.code ?? item?.Code ?? "",
      name: item?.name ?? item?.Name ?? "",
      description: item?.description ?? item?.Description ?? "",
      amountVnd: Number(item?.amountVnd ?? item?.AmountVnd ?? 0),
      sortOrder: Number(item?.sortOrder ?? item?.SortOrder ?? 0),
      isActive: Boolean(item?.isActive ?? item?.IsActive),
      isFeatured: Boolean(item?.isFeatured ?? item?.IsFeatured)
    }))
    : [];
}

export async function createWalletTopup({ packageId, amountVnd }) {
  const payload = await http("/api/student/wallet/topups/create", {
    method: "POST",
    body: JSON.stringify({ packageId, amountVnd })
  });

  return unwrap(payload, "Unable to create the payment transaction.");
}

export async function getWalletTopupHistory(status = "All") {
  const query = status && status !== "All" ? `?status=${encodeURIComponent(status)}` : "";
  const payload = await http(`/api/student/wallet/topups/history${query}`);
  return unwrap(payload, "Unable to load payment history.");
}

export async function getWalletTopupDetail(paymentId) {
  const payload = await http(`/api/student/wallet/topups/${paymentId}`);
  return unwrap(payload, "Unable to load payment details.");
}

export async function cancelWalletTopup(paymentId) {
  const payload = await http(`/api/student/wallet/topups/${paymentId}/cancel`, {
    method: "POST"
  });

  return unwrap(payload, "Unable to cancel the payment transaction.");
}

function normalizePaginated(payload) {
  const data = unwrap(payload, "Unable to load AI wallet history.");
  return {
    items: Array.isArray(data?.items ?? data?.Items) ? (data?.items ?? data?.Items) : [],
    total: Number(data?.total ?? data?.Total ?? 0),
    page: Number(data?.page ?? data?.Page ?? 1),
    limit: Number(data?.limit ?? data?.Limit ?? 20),
    totalPages: Number(data?.totalPages ?? data?.TotalPages ?? 1)
  };
}

export async function getStudentAiBillingTransactions(filters = {}) {
  const query = new URLSearchParams();
  query.set("page", String(filters.page || 1));
  query.set("limit", String(filters.limit || 10));

  if (filters.featureKey && filters.featureKey !== "All") {
    query.set("featureKey", filters.featureKey);
  }

  if (filters.status && filters.status !== "All") {
    query.set("status", filters.status);
  }

  if (filters.sourceEntityType && filters.sourceEntityType !== "All") {
    query.set("sourceEntityType", filters.sourceEntityType);
  }

  if (filters.fromDate) {
    query.set("fromDate", filters.fromDate);
  }

  if (filters.toDate) {
    query.set("toDate", filters.toDate);
  }

  const payload = await http(`/api/student/wallet/ai-transactions?${query.toString()}`);
  return normalizePaginated(payload);
}
