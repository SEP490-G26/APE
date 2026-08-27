import { useEffect, useRef, useState } from "react";
import { loginWithGoogleIdToken } from "../services/authService";
import { getGoogleClientId } from "../lib/env";
import { loadGoogleIdentityScript } from "../lib/googleIdentity";
import { saveAuthSession } from "../lib/storage";

export function useGoogleAuth({ onLoginSuccess } = {}) {
  const buttonRef = useRef(null);
  const initializedRef = useRef(false);
  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [successMessage, setSuccessMessage] = useState("");
  const [isGoogleReady, setIsGoogleReady] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function setupGoogle() {
      const clientId = getGoogleClientId();

      if (!clientId) {
        setErrorMessage("Missing VITE_GOOGLE_CLIENT_ID. Please add your Google Client ID to the .env file.");
        return;
      }

      try {
        const google = await loadGoogleIdentityScript();
        if (cancelled || !buttonRef.current || initializedRef.current) {
          return;
        }

        initializedRef.current = true;
        buttonRef.current.innerHTML = "";

        google.accounts.id.initialize({
          client_id: clientId,
          callback: async (response) => {
            if (!response.credential) {
              setErrorMessage("Google did not return a valid idToken.");
              return;
            }

            setLoading(true);
            setErrorMessage("");
            setSuccessMessage("");

            try {
              const session = await loginWithGoogleIdToken(response.credential);
              saveAuthSession(session);
              setSuccessMessage(`Login successful: ${session.user.fullName || session.user.email}`);
              onLoginSuccess?.(session);
            } catch (error) {
              setErrorMessage(error.message);
            } finally {
              setLoading(false);
            }
          }
        });

        google.accounts.id.renderButton(buttonRef.current, {
          type: "standard",
          theme: "outline",
          size: "large",
          text: "signin_with",
          locale: "en",
          shape: "rectangular",
          logo_alignment: "left",
          width: 320
        });

        setIsGoogleReady(true);
      } catch (error) {
        setErrorMessage(error.message);
      }
    }

    setupGoogle();

    return () => {
      cancelled = true;
      initializedRef.current = false;
    };
  }, []);

  return {
    googleButtonRef: buttonRef,
    loading,
    errorMessage,
    successMessage,
    isGoogleReady
  };
}
