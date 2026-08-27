const DEFAULT_API_BASE_URL = "http://localhost:5292";

export function getApiBaseUrl() {
  return (import.meta.env.VITE_API_BASE_URL || DEFAULT_API_BASE_URL).replace(/\/+$/, "");
}

export function getGoogleClientId() {
  return import.meta.env.VITE_GOOGLE_CLIENT_ID || "";
}
