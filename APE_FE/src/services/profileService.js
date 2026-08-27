import { http } from "./http";

function normalizeProfile(profile) {
  if (!profile) {
    return null;
  }

  return {
    id: profile?.id ?? profile?.Id ?? "",
    email: profile?.email ?? profile?.Email ?? "",
    fullName: profile?.fullName ?? profile?.FullName ?? "",
    avatarUrl: profile?.avatarUrl ?? profile?.AvatarUrl ?? "",
    role: profile?.role ?? profile?.Role ?? "Student",
    expPoints: Number(profile?.expPoints ?? profile?.ExpPoints ?? 0),
    aiWalletBalanceVnd: Number(profile?.aiWalletBalanceVnd ?? profile?.AiWalletBalanceVnd ?? 0),
    currentStreak: Number(profile?.currentStreak ?? profile?.CurrentStreak ?? 0),
    highestStreak: Number(profile?.highestStreak ?? profile?.HighestStreak ?? 0),
    badges: Array.isArray(profile?.badges ?? profile?.Badges) ? (profile?.badges ?? profile?.Badges) : []
  };
}

export async function getProfile() {
  const payload = await http("/api/student/profile", {
    method: "GET"
  });

  return normalizeProfile(payload);
}

export async function updateProfile(payload) {
  return await http("/api/student/profile", {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}
