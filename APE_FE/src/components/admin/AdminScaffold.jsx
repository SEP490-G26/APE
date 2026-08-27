import { useEffect, useMemo, useRef, useState } from "react";
import { Avatar } from "../common";
import { AppFooter } from "../layout/AppFooter";
import { AdminGlyph } from "./AdminGlyph";
import { BrandMark } from "../icons/BrandMark";
import { getAdminNavSections } from "../../data/adminCourseContent";
import { footerLinks } from "../../data/landingContent";
import { ROUTES, navigateTo } from "../../lib/routes";
import { clearAuthSession, getAuthSession } from "../../lib/storage";

function buildProfile() {
  const user = getAuthSession()?.user;

  return {
    fullName: user?.fullName || "Admin Center",
    avatarUrl: user?.avatarUrl || ""
  };
}

export function AdminScaffold({ activeRoute, heroIcon = "grid", heroTitle, heroSubtitle, title, subtitle, children }) {
  const profile = buildProfile();
  const adminNavSections = useMemo(() => getAdminNavSections(activeRoute), [activeRoute]);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const menuRef = useRef(null);
  const sidebarRef = useRef(null);
  const SIDEBAR_SCROLL_KEY = "ape.admin.sidebar.scrollTop";

  useEffect(() => {
    function handleClickOutside(event) {
      if (!menuRef.current?.contains(event.target)) {
        setIsMenuOpen(false);
      }
    }

    function handleEscape(event) {
      if (event.key === "Escape") {
        setIsMenuOpen(false);
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
    const sidebar = sidebarRef.current;
    if (!sidebar) {
      return undefined;
    }

    const saved = sessionStorage.getItem(SIDEBAR_SCROLL_KEY);
    if (saved) {
      const nextScrollTop = Number(saved);
      if (Number.isFinite(nextScrollTop)) {
        sidebar.scrollTop = nextScrollTop;
      }
    }

    function handleScroll() {
      sessionStorage.setItem(SIDEBAR_SCROLL_KEY, String(sidebar.scrollTop || 0));
    }

    sidebar.addEventListener("scroll", handleScroll, { passive: true });
    return () => sidebar.removeEventListener("scroll", handleScroll);
  }, []);

  function handleLogout() {
    clearAuthSession();
    window.location.assign(ROUTES.login);
  }

  return (
    <div className="admin-shell">
      <header className="admin-topbar">
        <button type="button" className="admin-topbar__brand" onClick={() => navigateTo(ROUTES.adminDashboard)}>
          <div className="admin-topbar__brand-mark" aria-hidden="true">
            <BrandMark />
          </div>
          <div className="admin-topbar__brand-copy">
            <span className="brand-platform">APE Admin</span>
            <small>Operations Workspace</small>
          </div>
        </button>

        <div className="admin-topbar__actions">
          <div className="profile-avatar-menu" ref={menuRef}>
            <button
              type="button"
              className="admin-topbar__profile profile-avatar-trigger"
              aria-haspopup="menu"
              aria-expanded={isMenuOpen}
              onClick={() => setIsMenuOpen((current) => !current)}
            >
              <Avatar name={profile.fullName} src={profile.avatarUrl} size="sm" className="profile-mini-avatar" />
              <span>Admin Center</span>
            </button>

            {isMenuOpen ? (
              <div className="profile-avatar-dropdown" role="menu">
                <div className="profile-avatar-dropdown__meta">
                  <strong>{profile.fullName}</strong>
                </div>
                <button
                  type="button"
                  className="profile-avatar-dropdown__logout"
                  onClick={() => {
                    setIsMenuOpen(false);
                    navigateTo(ROUTES.adminProfile);
                  }}
                >
                  Profile
                </button>
                <button
                  type="button"
                  className="profile-avatar-dropdown__logout"
                  onClick={handleLogout}
                >
                  Logout
                </button>
              </div>
            ) : null}
          </div>
        </div>
      </header>

      <div className="admin-layout">
        <aside className="admin-sidebar" ref={sidebarRef}>
          <div className="admin-sidebar__hero">
            <div className="admin-sidebar__hero-icon">
              <AdminGlyph kind={heroIcon} />
            </div>
            <div>
              <strong>{heroTitle}</strong>
              <p>{heroSubtitle}</p>
            </div>
          </div>

          <nav className="admin-sidebar__nav" aria-label="Admin">
            {adminNavSections.map((section) => (
              <section key={section.label} className="admin-sidebar__section">
                <p className="admin-sidebar__section-label">{section.label}</p>
                <div className="admin-sidebar__section-items">
                  {section.items.map((item) => (
                    <button
                      key={item.label}
                      type="button"
                      className={`admin-nav-link${item.active ? " is-active" : ""}`}
                      onClick={() => {
                        if (sidebarRef.current) {
                          sessionStorage.setItem(SIDEBAR_SCROLL_KEY, String(sidebarRef.current.scrollTop || 0));
                        }
                        if (item.href && item.href !== "#") {
                          navigateTo(item.href);
                        }
                      }}
                    >
                      <span className="admin-nav-link__icon">
                        <AdminGlyph kind={item.icon} />
                      </span>
                      <span>{item.label}</span>
                    </button>
                  ))}
                </div>
              </section>
            ))}
          </nav>
        </aside>

        <main className="admin-main">
          <section className="admin-page-header">
            <div>
              <h1>{title}</h1>
              <p>{subtitle}</p>
            </div>
          </section>

          {children}

          <AppFooter
            links={footerLinks}
            className="app-footer--admin"
            brand="APE Admin"
            description="Fallback data is active where backend endpoints are not available yet."
          />
        </main>
      </div>
    </div>
  );
}
