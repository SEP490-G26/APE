import { http } from "./http";

const ADMIN_USERS_BASE = "/api/admin/users";

function unwrapPayload(payload) {
  return payload?.data || payload;
}

export async function getAdminUsers(params = {}) {
  const query = new URLSearchParams();

  if (params.search?.trim()) {
    query.set("search", params.search.trim());
  }

  if (params.role && params.role !== "all") {
    query.set("role", params.role);
  }

  if (params.status && params.status !== "all") {
    query.set("status", params.status);
  }

  query.set("page", String(params.page || 1));
  query.set("limit", String(params.limit || 10));

  const payload = await http(`${ADMIN_USERS_BASE}?${query.toString()}`);
  return unwrapPayload(payload);
}

export async function getAdminUserById(userId) {
  const payload = await http(`${ADMIN_USERS_BASE}/${userId}`);
  return unwrapPayload(payload);
}

export async function updateAdminUserStatus(userId, status) {
  const payload = await http(`${ADMIN_USERS_BASE}/${userId}/status`, {
    method: "PATCH",
    body: JSON.stringify({ status })
  });

  return unwrapPayload(payload);
}
