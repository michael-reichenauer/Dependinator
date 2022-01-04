import { useEffect, useState } from "react";
import { durationString } from "./utils";

const activityTimeout = 60 * 1000;
const activityMargin = 1000;

let activityTime = 0;
let activityStartTime = 0;
let activityCheckTimer: NodeJS.Timeout | null = null;

let isDocumentActive = false;
const monitorEvents = [
  "mousemove",
  "mousedown",
  "touchstart",
  "touchmove",
  "keydown",
  "wheel",
];

export const activityEventName = "customActivityChange";

// Activates the user activity monitor, which can be read using useActivity()
export function useActivityMonitor() {
  useEffect(() => {
    // Called by monitored event handlers on activity = true events
    const onActive = () => {
      activityTime = Date.now();
      if (activityCheckTimer !== null) {
        // Already active
        return;
      }

      // Toggle active = true
      console.log(`Active`);
      isDocumentActive = true;
      activityStartTime = activityTime;

      // Schedule check if still active
      const timeout = activityTimeout + activityMargin;
      activityCheckTimer = setTimeout(checkIfActive, timeout);

      // Post activity=true event
      const activityEvent = new CustomEvent(activityEventName, {
        detail: true,
      });
      document.dispatchEvent(activityEvent);
    };

    // Called by check activity timeout or visibility hidden event handler
    const onInactive = () => {
      if (activityCheckTimer === null) {
        // Already inactive
        return;
      }

      // Toggle active = false
      console.log(
        `Inactive (total: ${durationString(Date.now() - activityStartTime)})`
      );
      isDocumentActive = false;
      activityStartTime = 0;

      // Clear timeout
      clearTimeout(activityCheckTimer);
      activityCheckTimer = null;

      // Post activity=false event
      const activityEvent = new CustomEvent(activityEventName, {
        detail: false,
      });
      document.dispatchEvent(activityEvent);
    };

    const checkIfActive = () => {
      if (activityCheckTimer === null) {
        // Already set to inactive
        return;
      }

      const now = Date.now();
      if (now - activityTime < activityTimeout) {
        // Still active, reschedule check
        const totalActiveTime = now - activityStartTime;
        console.log(`Still active (total: ${durationString(totalActiveTime)})`);
        const timeout = activityTimeout - (now - activityTime) + activityMargin;
        activityCheckTimer = setTimeout(checkIfActive, timeout);
        return;
      }

      // console.log(`No longer active (${now - activityTime}), recheck..  (total: ${Date.now() - activityStartTime})`)
      onInactive();
    };

    const onVisibilityChange = (_e: any) => {
      // console.log(`onVisibilityChange visible=${!document.hidden}`)
      if (!document.hidden) {
        onActive();
        return;
      }

      onInactive();
    };

    // Register monitored events handler
    document.onvisibilitychange = onVisibilityChange;
    monitorEvents.forEach((name) => document.addEventListener(name, onActive));

    setTimeout(onActive, 1);

    return () => {
      // Unregister monitored events handler
      document.onvisibilitychange = null;
      monitorEvents.forEach((name) =>
        document.removeEventListener(name, onActive)
      );
    };
  }, []);
}

// Returns the current activity
export function useActivity() {
  const [isActive, setIsActive] = useState(isDocumentActive);

  useEffect(() => {
    const onActivityEvent = (e: any) => {
      setIsActive(e.detail);
    };

    document.addEventListener(activityEventName, onActivityEvent);

    return () => {
      document.removeEventListener(activityEventName, onActivityEvent);
    };
  }, []);

  return [isActive];
}

export function useActivityChanged() {
  const [isChanged, setIsChanged] = useState(false);

  useEffect(() => {
    const onActivityEvent = (_e: any) => {
      setIsChanged(true);
    };

    document.addEventListener(activityEventName, onActivityEvent);

    return () => {
      document.removeEventListener(activityEventName, onActivityEvent);
    };
  }, []);

  if (isChanged) {
    setIsChanged(false);
  }

  return isChanged;
}
