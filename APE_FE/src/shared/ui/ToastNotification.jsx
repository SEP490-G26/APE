import { ToastErrorIcon } from "../../components/icons/ToastErrorIcon";
import { ToastSuccessIcon } from "../../components/icons/ToastSuccessIcon";

export function ToastNotification({ toast }) {
  if (!toast?.message) {
    return null;
  }

  const type = toast.type === "success" ? "success" : "error";

  return (
    <div className={`toast-notification toast-notification--${type}`} role="status" aria-live="polite">
      <span className="toast-notification__icon">
        {type === "success" ? <ToastSuccessIcon /> : <ToastErrorIcon />}
      </span>
      <span className="toast-notification__message">{toast.message}</span>
    </div>
  );
}
