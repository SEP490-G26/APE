import { CreditIcon } from "../icons/CreditIcon";
import { SunIcon } from "../icons/SunIcon";
import { Avatar, Button, Field, Surface, TextInput } from "../common";
import { ROUTES, navigateTo } from "../../lib/routes";

function formatVnd(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} VND`;
}

export function ProfileCard({
  profile,
  draftFullName,
  isSaving = false,
  theme = "light",
  onDraftFullNameChange,
  onDiscard,
  onSave,
  onToggleTheme
}) {
  const isLightMode = theme === "light";

  return (
    <Surface as="section" className="profile-card">
      <div className="profile-card__section">
        <h2>Personal Information</h2>

        <div className="profile-personal-grid">
          <div className="profile-avatar-panel">
            <div className="profile-avatar-card">
              <div className="profile-avatar-card__label">@{profile.fullName.split(" ").join("").toLowerCase()}</div>
              <Avatar
                name={profile.fullName}
                src={profile.avatarUrl}
                size="xl"
                className="profile-avatar-card__image"
              />
            </div>
          </div>

          <div className="profile-form">
            <Field
              label="Email Address (Google SSO)"
              hint="SSO linked accounts cannot change their email."
            >
              <div className="profile-input profile-input--readonly">
                <span className="profile-input__icon">@</span>
                <span>{profile.email}</span>
              </div>
            </Field>

            <Field label="Full Name">
              <TextInput value={draftFullName} onChange={onDraftFullNameChange} />
            </Field>
          </div>
        </div>
      </div>

      <div className="profile-card__divider" />

      <div className="profile-card__section">
        <h2>Platform Details</h2>

        <div className="profile-detail-grid">
          <article className="detail-tile">
            <div className="detail-tile__icon detail-tile__icon--credit">
              <CreditIcon />
            </div>
            <div>
              <p>AI Wallet</p>
              <strong>{formatVnd(profile.aiWalletBalanceVnd)}</strong>
            </div>
            <Button type="button" variant="ghost" className="detail-link" onClick={() => navigateTo(ROUTES.studentSubscription)}>
              View Billing
            </Button>
          </article>

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
      </div>

      <div className="profile-card__actions">
        <Button type="button" variant="secondary" onClick={onDiscard}>
          Discard
        </Button>
        <Button type="button" variant="primary" onClick={onSave} disabled={isSaving}>
          {isSaving ? "Saving..." : "Save Changes"}
        </Button>
      </div>
    </Surface>
  );
}
