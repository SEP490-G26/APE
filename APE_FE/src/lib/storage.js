const AUTH_STORAGE_KEY = "ape.auth.session";
const THEME_STORAGE_KEY = "ape.theme";

export function saveAuthSession(session) {
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
}

export function getAuthSession() {
  const rawValue = localStorage.getItem(AUTH_STORAGE_KEY);
  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue);
  } catch {
    localStorage.removeItem(AUTH_STORAGE_KEY);
    return null;
  }
}

export function clearAuthSession() {
  localStorage.removeItem(AUTH_STORAGE_KEY);
}

export function getAccessToken() {
  return getAuthSession()?.accessToken || "";
}

export function getRefreshToken() {
  return getAuthSession()?.refreshToken || "";
}

export function updateAuthSessionUser(updates) {
  const session = getAuthSession();

  if (!session?.user) {
    return null;
  }

  const nextSession = {
    ...session,
    user: {
      ...session.user,
      ...updates
    }
  };

  saveAuthSession(nextSession);
  return nextSession;
}

export function getStoredTheme() {
  return localStorage.getItem(THEME_STORAGE_KEY) || "light";
}

export function saveStoredTheme(theme) {
  localStorage.setItem(THEME_STORAGE_KEY, theme);
}
