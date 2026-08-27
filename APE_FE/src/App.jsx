import { useEffect, useState } from "react";
import { LoginPage } from "./pages/auth";
import { refreshAuthSession } from "./services/authService";
import {
  clearAuthSession,
  getAuthSession,
  getStoredTheme,
  saveStoredTheme
} from "./lib/storage";
import { ROUTES, navigateTo, normalizePath } from "./lib/routes";
import { ADMIN_ROUTES, renderAdminRoute } from "./app/adminRoutes";
import {
  STUDENT_AUTH_ROUTES,
  STUDENT_MEMBER_ROUTES,
  renderStudentRoute
} from "./app/studentRoutes";

function isProtectedStudentPath(pathname) {
  return pathname === ROUTES.dashboard
    || pathname === ROUTES.legacyDashboard
    || pathname === ROUTES.legacyStudentJourney
    || pathname === ROUTES.legacyStudentSetupRoot
    || STUDENT_AUTH_ROUTES.includes(pathname)
    || isStudentManagedPath(pathname);
}

function isStudentManagedPath(pathname) {
  return pathname.startsWith("/documents/byos/") || pathname.startsWith("/generate/");
}

function isAdminManagedPath(pathname) {
  return pathname.startsWith("/admin/");
}

function getAuthenticatedHomeRoute(session = getAuthSession()) {
  const role = session?.user?.role?.toLowerCase();

  return role === "admin" ? ROUTES.adminDashboard : ROUTES.dashboard;
}

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(() => Boolean(getAuthSession()));
  const [pathname, setPathname] = useState(() => normalizePath(window.location.pathname));
  const [theme, setTheme] = useState(() => getStoredTheme());
  const [isBootstrappingAuth, setIsBootstrappingAuth] = useState(() => Boolean(getAuthSession()));

  useEffect(() => {
    function syncAuthState() {
      setIsAuthenticated(Boolean(getAuthSession()));
    }

    window.addEventListener("storage", syncAuthState);
    return () => window.removeEventListener("storage", syncAuthState);
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function bootstrapAuth() {
      if (!getAuthSession()) {
        setIsBootstrappingAuth(false);
        return;
      }

      try {
        await refreshAuthSession();
        if (!cancelled) {
          setIsAuthenticated(Boolean(getAuthSession()));
        }
      } catch {
        if (!cancelled) {
          clearAuthSession();
          setIsAuthenticated(false);
        }
      } finally {
        if (!cancelled) {
          setIsBootstrappingAuth(false);
        }
      }
    }

    bootstrapAuth();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    function handleRouteChange() {
      setPathname(normalizePath(window.location.pathname));
    }

    window.addEventListener("popstate", handleRouteChange);
    return () => window.removeEventListener("popstate", handleRouteChange);
  }, []);

  useEffect(() => {
    const authenticatedHomeRoute = getAuthenticatedHomeRoute();

    if (pathname === ROUTES.legacyDashboard) {
      navigateTo(ROUTES.dashboard, { replace: true });
      return;
    }

    if (pathname === ROUTES.legacyStudentJourney) {
      navigateTo(ROUTES.studentJourney, { replace: true });
      return;
    }

    if (pathname === ROUTES.legacyStudentSetupRoot) {
      navigateTo(ROUTES.studentJourney, { replace: true });
      return;
    }

    if (isAuthenticated && pathname === ROUTES.login) {
      navigateTo(authenticatedHomeRoute, { replace: true });
      return;
    }

    if (!isAuthenticated && isProtectedStudentPath(pathname)) {
      navigateTo(ROUTES.login, { replace: true });
      return;
    }

    if (!isAuthenticated && (ADMIN_ROUTES.includes(pathname) || isAdminManagedPath(pathname))) {
      navigateTo(ROUTES.login, { replace: true });
      return;
    }

    if (isAuthenticated && pathname === ROUTES.dashboard && authenticatedHomeRoute !== ROUTES.dashboard) {
      navigateTo(authenticatedHomeRoute, { replace: true });
      return;
    }

    if (isAuthenticated && pathname === ROUTES.profile && authenticatedHomeRoute === ROUTES.adminDashboard) {
      navigateTo(ROUTES.adminProfile, { replace: true });
      return;
    }

    if (isAuthenticated && (STUDENT_MEMBER_ROUTES.includes(pathname) || isStudentManagedPath(pathname)) && authenticatedHomeRoute !== ROUTES.dashboard) {
      navigateTo(authenticatedHomeRoute, { replace: true });
      return;
    }

    if (isAuthenticated && (ADMIN_ROUTES.includes(pathname) || isAdminManagedPath(pathname)) && authenticatedHomeRoute !== ROUTES.adminDashboard) {
      navigateTo(authenticatedHomeRoute, { replace: true });
    }
  }, [isAuthenticated, pathname]);

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    saveStoredTheme(theme);
  }, [theme]);

  const handleLogout = () => {
    clearAuthSession();
    setIsAuthenticated(false);
    navigateTo(ROUTES.login);
  };

  if (isBootstrappingAuth) {
    return null;
  }

  const sharedRouteProps = {
    isAuthenticated,
    onLogout: handleLogout
  };

  const studentRoute = renderStudentRoute(pathname, {
    ...sharedRouteProps,
    theme,
    onToggleTheme: () => setTheme((currentTheme) => (currentTheme === "light" ? "dark" : "light"))
  });

  if (studentRoute) {
    return studentRoute;
  }

  const adminRoute = renderAdminRoute(pathname, {
    ...sharedRouteProps,
    theme,
    onToggleTheme: () => setTheme((currentTheme) => (currentTheme === "light" ? "dark" : "light"))
  });

  if (adminRoute) {
    return adminRoute;
  }

  return (
    <LoginPage
      onLoginSuccess={(session) => {
        setIsAuthenticated(true);
        navigateTo(getAuthenticatedHomeRoute(session));
      }}
    />
  );
}

export default App;

