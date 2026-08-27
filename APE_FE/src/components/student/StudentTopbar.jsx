import { useEffect, useRef, useState } from "react";
import { Avatar, Button } from "../common";
import { BrandMark } from "../icons/BrandMark";
import { ROUTES, navigateTo } from "../../lib/routes";
import { clearAuthSession, getAuthSession } from "../../lib/storage";

const primaryNavItems = [
  { label: "Home", section: "dashboard", href: ROUTES.dashboard },
  { label: "Setup", section: "journey", href: ROUTES.studentJourney },
  { label: "Practice", section: "practice", href: ROUTES.studentPractice },
  { label: "Wallet", section: "wallet", href: ROUTES.studentSubscription },
  { label: "Analytics", section: "analytic", href: ROUTES.studentAnalytics }
];

function buildProfile() {
  const user = getAuthSession()?.user;

  return {
    fullName: user?.fullName || "APE Student",
    avatarUrl: user?.avatarUrl || ""
  };
}

export function StudentTopbar({ activeSection = "dashboard", actionLabel = "Open Exams", actionHref = ROUTES.studentExams }) {
  const profile = buildProfile();
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const menuRef = useRef(null);
  const navRef = useRef(null);

  useEffect(() => {
    function handleClickOutside(event) {
      if (!menuRef.current?.contains(event.target)) {
        setIsMenuOpen(false);
      }

      if (!navRef.current?.contains(event.target)) {
        setIsMobileNavOpen(false);
      }
    }

    function handleEscape(event) {
      if (event.key === "Escape") {
        setIsMenuOpen(false);
        setIsMobileNavOpen(false);
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleEscape);

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEscape);
    };
  }, []);

  useEffect(() => {
    setIsMenuOpen(false);
    setIsMobileNavOpen(false);
  }, [activeSection, actionHref]);

  function handleLogout() {
    clearAuthSession();
    window.location.assign(ROUTES.login);
  }

  return (
    <header className="student-exam-topbar">
      <div className="student-exam-topbar__inner">
        <button
          type="button"
          className="student-exam-topbar__brand"
          onClick={() => navigateTo(ROUTES.dashboard)}
        >
          <BrandMark />
          <span className="student-exam-topbar__title">APE</span>
        </button>

        <div className="student-exam-topbar__nav-shell" ref={navRef}>
          <button
            type="button"
            className={`student-exam-nav-toggle${isMobileNavOpen ? " is-open" : ""}`}
            aria-expanded={isMobileNavOpen}
            aria-controls="student-primary-nav"
            aria-label="Open navigation"
            onClick={() => setIsMobileNavOpen((current) => !current)}
          >
            <span />
            <span />
            <span />
          </button>

          <nav
            id="student-primary-nav"
            className={`student-exam-topbar__nav${isMobileNavOpen ? " is-open" : ""}`}
            aria-label="Student"
          >
            {primaryNavItems.map((item) => (
              <button
                key={item.section}
                type="button"
                className={`student-exam-nav-link${activeSection === item.section ? " is-active" : ""}`}
                onClick={() => {
                  setIsMobileNavOpen(false);
                  navigateTo(item.href);
                }}
              >
                {item.label}
              </button>
            ))}
          </nav>
        </div>

        <div className="student-exam-topbar__actions">
          <div className="profile-avatar-menu" ref={menuRef}>
            <button
              type="button"
              className="student-exam-profile profile-avatar-trigger"
              aria-haspopup="menu"
              aria-expanded={isMenuOpen}
              onClick={() => setIsMenuOpen((current) => !current)}
            >
              <Avatar name={profile.fullName} src={profile.avatarUrl} size="sm" className="profile-mini-avatar" />
            </button>

            {isMenuOpen ? (
              <div className="profile-avatar-dropdown" role="menu">
                <div className="profile-avatar-dropdown__meta">
                  <strong>{profile.fullName}</strong>
                </div>
                <Button
                  variant="ghost"
                  className="profile-avatar-dropdown__logout"
                  onClick={() => {
                    setIsMenuOpen(false);
                    navigateTo(ROUTES.profile);
                  }}
                >
                  Profile
                </Button>
                <Button
                  variant="ghost"
                  className="profile-avatar-dropdown__logout"
                  onClick={handleLogout}
                >
                  Logout
                </Button>
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </header>
  );
}
