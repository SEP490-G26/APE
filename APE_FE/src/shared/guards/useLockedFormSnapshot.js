import { useEffect, useRef, useState } from "react";

export function useLockedFormSnapshot(value, isLocked) {
  const wasLockedRef = useRef(false);
  const [snapshot, setSnapshot] = useState(value);

  useEffect(() => {
    if (isLocked && !wasLockedRef.current) {
      setSnapshot(value);
    }

    wasLockedRef.current = isLocked;
  }, [isLocked, value]);

  return isLocked ? snapshot : value;
}
