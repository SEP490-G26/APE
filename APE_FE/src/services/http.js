import { getApiBaseUrl } from "../lib/env";
import { clearAuthSession, getAccessToken } from "../lib/storage";
import { refreshAuthSession } from "./authService";

async function requestJson(path, options = {}) {
  const accessToken = getAccessToken();
  const isFormData = options.body instanceof FormData;
  const headers = {
    ...(isFormData ? {} : { "Content-Type": "application/json" }),
    ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    ...(options.headers || {})
  };

  const response = await fetch(`${getApiBaseUrl()}${path}`, {
    headers,
    ...options
  });

  let payload = null;
  try {
    payload = await response.json();
  } catch {
    payload = null;
  }

  return { response, payload };
}

export async function http(path, options = {}) {
  let { response, payload } = await requestJson(path, options);

  if (response.status === 401 && getAccessToken()) {
    try {
      await refreshAuthSession();
      ({ response, payload } = await requestJson(path, options));
    } catch {
      clearAuthSession();
      window.dispatchEvent(new StorageEvent("storage", { key: "ape.auth.session" }));
      throw new Error("Session expired. Please log in again.");
    }
  }

  if (!response.ok) {
    if (response.status === 401) {
      clearAuthSession();
      window.dispatchEvent(new StorageEvent("storage", { key: "ape.auth.session" }));
    }

    throw new Error(payload?.error || payload?.message || "Yeu cau that bai.");
  }

  return payload;
}
