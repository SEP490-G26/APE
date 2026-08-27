import { Surface } from "../common";

export function LoginCard({
  googleButtonRef,
  loading,
  errorMessage,
  successMessage,
  isGoogleReady
}) {
  return (
    <aside className="login-panel">
      <Surface className="login-card">
        <div className="login-card__glow" aria-hidden="true" />
        <h2>Welcome Back</h2>
        <p>Log in to your institutional account to continue</p>

        <div className="google-button-shell">
          <div className={`google-button${!isGoogleReady ? " is-disabled" : ""}`}>
            <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
              <path
                fill="#4285F4"
                d="M21.6 12.23c0-.68-.06-1.33-.17-1.95H12v3.69h5.39a4.6 4.6 0 0 1-2 3.02v2.5h3.23c1.9-1.75 2.98-4.33 2.98-7.26Z"
              />
              <path
                fill="#34A853"
                d="M12 22c2.7 0 4.96-.9 6.61-2.44l-3.23-2.5c-.9.6-2.05.96-3.38.96-2.6 0-4.8-1.76-5.58-4.12H3.08v2.58A9.99 9.99 0 0 0 12 22Z"
              />
              <path
                fill="#FBBC05"
                d="M6.42 13.9A5.98 5.98 0 0 1 6.1 12c0-.66.11-1.3.32-1.9V7.52H3.08A9.99 9.99 0 0 0 2 12c0 1.61.39 3.13 1.08 4.48l3.34-2.58Z"
              />
              <path
                fill="#EA4335"
                d="M12 5.98c1.47 0 2.8.5 3.84 1.49l2.88-2.88C16.95 2.96 14.69 2 12 2A9.99 9.99 0 0 0 3.08 7.52L6.42 10.1c.78-2.36 2.98-4.12 5.58-4.12Z"
              />
            </svg>
            <span>Login with Google</span>
          </div>
          <div
            ref={googleButtonRef}
            className={`google-signin-slot${!isGoogleReady ? " is-disabled" : ""}`}
          />
        </div>

        {loading ? <p className="status-copy">Signing you in...</p> : null}
        {errorMessage ? <p className="status-copy status-copy--error">{errorMessage}</p> : null}
        {successMessage ? (
          <p className="status-copy status-copy--success">{successMessage}</p>
        ) : null}

        <p className="legal-copy">
          By signing in, you agree to the Institutional Guidelines and Terms of Service.
        </p>

        <p className="support-copy">
          Technical issues? <a href="/">Contact IT Support</a>
        </p>
      </Surface>
    </aside>
  );
}
