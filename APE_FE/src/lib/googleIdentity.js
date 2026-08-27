const GOOGLE_SCRIPT_SRC = "https://accounts.google.com/gsi/client?hl=en";

export function loadGoogleIdentityScript() {
  return new Promise((resolve, reject) => {
    if (window.google?.accounts?.id) {
      resolve(window.google);
      return;
    }

    const existingScript = document.querySelector(`script[src="${GOOGLE_SCRIPT_SRC}"]`);

    if (existingScript) {
      existingScript.addEventListener("load", () => resolve(window.google), { once: true });
      existingScript.addEventListener(
        "error",
        () => reject(new Error("Unable to load Google Identity Services.")),
        { once: true }
      );
      return;
    }

    const script = document.createElement("script");
    script.src = GOOGLE_SCRIPT_SRC;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve(window.google);
    script.onerror = () => reject(new Error("Unable to load Google Identity Services."));
    document.head.appendChild(script);
  });
}
