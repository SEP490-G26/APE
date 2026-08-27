import { useEffect } from "react";

export function useBeforeUnloadGuard(isActive, message) {
  useEffect(() => {
    if (!isActive) {
      return undefined;
    }

    function handleBeforeUnload(event) {
      event.preventDefault();
      event.returnValue = message;
      return message;
    }

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [isActive, message]);
}
