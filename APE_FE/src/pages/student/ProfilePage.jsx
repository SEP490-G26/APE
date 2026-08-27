import { useEffect, useState } from "react";
import { defaultProfile } from "../../data/profileContent";
import { getAuthSession, updateAuthSessionUser } from "../../lib/storage";
import { ROUTES, navigateTo } from "../../lib/routes";
import { ProfileCard } from "../../components/profile/ProfileCard";
import { ProfileHero } from "../../components/profile/ProfileHero";
import { StudentWorkspaceShell } from "../../components/student/StudentWorkspaceShell";
import { updateProfile } from "../../services/profileService";
import { ToastNotification } from "../../shared/ui";

function buildProfileViewModel() {
  const session = getAuthSession();
  const user = session?.user;

  return {
    email: user?.email || defaultProfile.email,
    fullName: user?.fullName || defaultProfile.fullName,
    avatarUrl: user?.avatarUrl || defaultProfile.avatarUrl,
    aiWalletBalanceVnd: Number(user?.aiWalletBalanceVnd ?? 0),
    theme: defaultProfile.theme
  };
}

function getProfileHomeRoute() {
  const role = getAuthSession()?.user?.role?.toLowerCase();
  return role === "admin" ? ROUTES.adminDashboard : ROUTES.dashboard;
}

export function ProfilePage({ theme, onToggleTheme }) {
  const [profile, setProfile] = useState(() => buildProfileViewModel());
  const [draftFullName, setDraftFullName] = useState(() => buildProfileViewModel().fullName);
  const [toast, setToast] = useState(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    const nextProfile = buildProfileViewModel();
    setProfile(nextProfile);
    setDraftFullName(nextProfile.fullName);
  }, []);

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  const handleNameChange = (event) => {
    setDraftFullName(event.target.value);
  };

  const handleDiscard = () => {
    setDraftFullName(profile.fullName);
    setToast(null);
    navigateTo(getProfileHomeRoute());
  };

  const handleSave = async () => {
    const normalizedFullName = draftFullName.trim();

    if (!normalizedFullName) {
      setDraftFullName(profile.fullName);
      setToast({
        type: "error",
        message: "Full name cannot be empty."
      });
      return;
    }

    setIsSaving(true);

    try {
      const updatedProfile = await updateProfile({ fullName: normalizedFullName });
      const nextProfile = {
        ...profile,
        ...updatedProfile,
        fullName: updatedProfile?.fullName || normalizedFullName,
        aiWalletBalanceVnd: Number(updatedProfile?.aiWalletBalanceVnd ?? profile.aiWalletBalanceVnd ?? 0)
      };

      setProfile(nextProfile);
      setDraftFullName(nextProfile.fullName);
      updateAuthSessionUser({
        fullName: nextProfile.fullName,
        avatarUrl: nextProfile.avatarUrl,
        email: nextProfile.email,
        aiWalletBalanceVnd: nextProfile.aiWalletBalanceVnd
      });
      setToast({
        type: "success",
        message: "Profile updated successfully."
      });
    } catch (error) {
      setToast({
        type: "error",
        message: error.message || "Unable to save profile right now."
      });
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <StudentWorkspaceShell
      activeRoute={ROUTES.profile}
      title="Account Settings"
      subtitle="Manage your profile information and preferences."
      actions={null}
      showPageHeader={false}
    >
      <div className="profile-shell">
        <main className="profile-main">
          <ProfileHero />
          <ProfileCard
            profile={profile}
            draftFullName={draftFullName}
            isSaving={isSaving}
            theme={theme}
            onDraftFullNameChange={handleNameChange}
            onDiscard={handleDiscard}
            onSave={handleSave}
            onToggleTheme={onToggleTheme}
          />
        </main>
        <ToastNotification toast={toast} />
      </div>
    </StudentWorkspaceShell>
  );
}
