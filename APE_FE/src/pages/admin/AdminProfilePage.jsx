import { useEffect, useState } from "react";
import { Avatar, Button } from "../../components/common";
import { SunIcon } from "../../components/icons/SunIcon";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { defaultProfile } from "../../data/profileContent";
import { getAuthSession, updateAuthSessionUser } from "../../lib/storage";
import { updateProfile } from "../../services/profileService";
import { ToastNotification } from "../../shared/ui";

function buildAdminProfileViewModel() {
  const session = getAuthSession();
  const user = session?.user;

  return {
    email: user?.email || defaultProfile.email,
    fullName: user?.fullName || "Admin Center",
    avatarUrl: user?.avatarUrl || defaultProfile.avatarUrl,
    role: user?.role || "Admin"
  };
}

export function AdminProfilePage({ theme = "light", onToggleTheme }) {
  const [profile, setProfile] = useState(() => buildAdminProfileViewModel());
  const [draftFullName, setDraftFullName] = useState(() => buildAdminProfileViewModel().fullName);
  const [toast, setToast] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const isLightMode = theme === "light";

  useEffect(() => {
    const nextProfile = buildAdminProfileViewModel();
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

  function handleDiscard() {
    setDraftFullName(profile.fullName);
    setToast(null);
    navigateTo(ROUTES.adminDashboard);
  }

  async function handleSave() {
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
        fullName: updatedProfile?.fullName || normalizedFullName
      };

      setProfile(nextProfile);
      setDraftFullName(nextProfile.fullName);
      updateAuthSessionUser({
        fullName: nextProfile.fullName,
        avatarUrl: nextProfile.avatarUrl,
        email: nextProfile.email
      });
      setToast({
        type: "success",
        message: "Admin profile updated successfully."
      });
    } catch (error) {
      setToast({
        type: "error",
        message: error.message || "Unable to save admin profile right now."
      });
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminProfile}
      heroIcon="users"
      heroTitle="Admin Settings"
      heroSubtitle="Profile"
      title="Admin Profile"
      subtitle="Manage your administrator identity and account basics."
    >
      <section className="admin-profile-shell">
        <article className="admin-table-panel admin-profile-grid">
          <div className="admin-profile-summary">
            <div className="admin-profile-avatar">
              <Avatar
                name={profile.fullName}
                src={profile.avatarUrl}
                size="xl"
                className="admin-profile-avatar__image"
              />
            </div>
          <div className="admin-profile-summary__copy">
            <h2>{profile.fullName}</h2>
            <span>{profile.role}</span>
          </div>
        </div>

          <div className="admin-profile-form">
            <label className="admin-document-field">
              <span>Email Address</span>
              <div className="admin-profile-readonly">{profile.email}</div>
            </label>

            <label className="admin-document-field admin-document-field--full">
              <span>Full Name</span>
              <input
                type="text"
                value={draftFullName}
                onChange={(event) => setDraftFullName(event.target.value)}
              />
            </label>
          </div>

          <div className="profile-detail-grid">
            <article className="detail-tile">
              <div className="detail-tile__icon detail-tile__icon--theme">
                <SunIcon />
              </div>
              <div>
                <p>Interface Theme</p>
                <strong>{isLightMode ? "Light Mode" : "Dark Mode"}</strong>
              </div>
              <button
                type="button"
                className={`theme-switch${isLightMode ? "" : " is-active"}`}
                aria-label="Toggle theme"
                aria-pressed={!isLightMode}
                onClick={onToggleTheme}
              >
                <span />
              </button>
            </article>
          </div>

          <div className="admin-profile-actions">
            <Button type="button" variant="secondary" onClick={handleDiscard}>
              Discard
            </Button>
            <Button type="button" variant="primary" onClick={handleSave} disabled={isSaving}>
              {isSaving ? "Saving..." : "Save Changes"}
            </Button>
          </div>
        </article>
      </section>

      <ToastNotification toast={toast} />
    </AdminScaffold>
  );
}
