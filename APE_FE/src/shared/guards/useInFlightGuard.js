import { useEffect } from "react";
import { setNavigationBlocker } from "../../lib/routes";
import { useBeforeUnloadGuard } from "./useBeforeUnloadGuard";

const DEFAULT_MESSAGE = "An AI request is still running. Leaving this page may interrupt the current operation.";

export function useInFlightGuard(isActive, message = DEFAULT_MESSAGE) {
  useBeforeUnloadGuard(isActive, message);

  useEffect(() => {
    if (!isActive) {
      return undefined;
    }

    const releaseBlocker = setNavigationBlocker(() => window.confirm(message));
    return releaseBlocker;
  }, [isActive, message]);
}
