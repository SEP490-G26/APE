import { http } from "./http";

function unwrap(payload, fallbackMessage) {
  if (!payload?.success) {
    throw new Error(payload?.error || fallbackMessage);
  }

  return payload.data;
}

function normalizePackage(item) {
  return {
    id: item?.id ?? item?.Id ?? "",
    code: item?.code ?? item?.Code ?? "",
    name: item?.name ?? item?.Name ?? "",
    description: item?.description ?? item?.Description ?? "",
    amountVnd: Number(item?.amountVnd ?? item?.AmountVnd ?? 0),
    sortOrder: Number(item?.sortOrder ?? item?.SortOrder ?? 0),
    isActive: Boolean(item?.isActive ?? item?.IsActive),
    isFeatured: Boolean(item?.isFeatured ?? item?.IsFeatured)
  };
}

export async function getAdminTopupPackages() {
  const payload = await http("/api/admin/wallet-packages");
  const data = unwrap(payload, "Unable to load wallet packages.");
  return Array.isArray(data) ? data.map(normalizePackage) : [];
}

export async function createAdminTopupPackage(draft) {
  const payload = await http("/api/admin/wallet-packages", {
    method: "POST",
    body: JSON.stringify(draft)
  });
  return normalizePackage(unwrap(payload, "Unable to create wallet package."));
}

export async function updateAdminTopupPackage(id, draft) {
  const payload = await http(`/api/admin/wallet-packages/${id}`, {
    method: "PUT",
    body: JSON.stringify(draft)
  });
  return normalizePackage(unwrap(payload, "Unable to update wallet package."));
}

export async function updateAdminTopupPackageStatus(id, isActive) {
  const payload = await http(`/api/admin/wallet-packages/${id}/status`, {
    method: "PATCH",
    body: JSON.stringify({ isActive })
  });
  return normalizePackage(unwrap(payload, "Unable to update wallet package status."));
}
