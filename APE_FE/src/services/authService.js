import { getApiBaseUrl } from "../lib/env";
import {
  clearAuthSession,
  getAccessToken,
  getAuthSession,
  getRefreshToken,
  saveAuthSession
} from "../lib/storage";

let refreshPromise = null;

async function readJsonSafely(response) {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

function normalizeUser(user) {
  if (!user) {
    return null;
  }

  return {
    id: user.id ?? user.Id ?? "",
    email: user.email ?? user.Email ?? "",
    fullName: user.fullName ?? user.FullName ?? "",
    avatarUrl: user.avatarUrl ?? user.AvatarUrl ?? "",
    role: user.role ?? user.Role ?? "Student",
    expPoints: user.expPoints ?? user.ExpPoints ?? 0,
    aiWalletBalanceVnd: Number(user.aiWalletBalanceVnd ?? user.AiWalletBalanceVnd ?? 0),
    currentStreak: user.currentStreak ?? user.CurrentStreak ?? 0,
    highestStreak: user.highestStreak ?? user.HighestStreak ?? 0,
    badges: user.badges ?? user.Badges ?? []
  };
}

function normalizeSessionPayload(data, fallbackUser = null) {
  return {
    accessToken: data?.accessToken ?? data?.AccessToken ?? "",
    refreshToken: data?.refreshToken ?? data?.RefreshToken ?? "",
    expiresAt: data?.expiresAt ?? data?.ExpiresAt ?? null,
    user: normalizeUser(data?.user ?? data?.User ?? fallbackUser)
  };
}

async function fetchProfileWithAccessToken(accessToken) {
  const response = await fetch(`${getApiBaseUrl()}/api/auth/me`, {
    method: "GET",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`
    }
  });

  const payload = await readJsonSafely(response);

  if (!response.ok || !payload?.success || !payload?.data) {
    throw new Error(payload?.error || payload?.message || "Unable to load user profile.");
  }

  return normalizeUser(payload.data);
}

export async function loginWithGoogleIdToken(idToken) {
  const response = await fetch(`${getApiBaseUrl()}/api/auth/google`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ idToken })
  });
  const payload = await readJsonSafely(response);

  if (!response.ok) {
    throw new Error(payload?.error || payload?.message || "Dang nhap Google that bai.");
  }

  if (!payload?.success || !payload.data) {
    throw new Error(payload?.error || "Dang nhap Google that bai.");
  }

  const nextSession = normalizeSessionPayload(payload.data);
  nextSession.user = await fetchProfileWithAccessToken(nextSession.accessToken);
  return nextSession;
}

export async function refreshAuthSession() {
  if (refreshPromise) {
    return refreshPromise;
  }

  const refreshToken = getRefreshToken();
  const currentSession = getAuthSession();

  if (!refreshToken || !currentSession?.user) {
    clearAuthSession();
    throw new Error("Session expired.");
  }

  refreshPromise = (async () => {
    const response = await fetch(`${getApiBaseUrl()}/api/auth/refresh`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(getAccessToken() ? { Authorization: `Bearer ${getAccessToken()}` } : {})
      },
      body: JSON.stringify({ refreshToken })
    });

    const payload = await readJsonSafely(response);

    if (!response.ok || !payload?.success || !payload?.data) {
      clearAuthSession();
      throw new Error(payload?.error || payload?.message || "Session expired.");
    }

    const nextSession = normalizeSessionPayload(payload.data, currentSession.user);
    nextSession.user = await fetchProfileWithAccessToken(nextSession.accessToken);

    saveAuthSession(nextSession);
    return nextSession;
  })();

  try {
    return await refreshPromise;
  } finally {
    refreshPromise = null;
  }
}
