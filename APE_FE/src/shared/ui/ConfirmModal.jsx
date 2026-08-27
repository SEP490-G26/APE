import { Button } from "../../components/common";

export function ConfirmModal({
  open,
  title = "Confirm action",
  message,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  confirmVariant = "primary",
  isConfirming = false,
  onConfirm,
  onCancel
}) {
  if (!open) {
    return null;
  }

  return (
    <div className="confirm-modal-backdrop" role="presentation" onClick={() => !isConfirming && onCancel?.()}>
      <div
        className="confirm-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="confirm-modal-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="confirm-modal__header">
          <h2 id="confirm-modal-title">{title}</h2>
          <button
            type="button"
            className="confirm-modal__close"
            aria-label="Close confirmation dialog"
            onClick={() => !isConfirming && onCancel?.()}
          >
            x
          </button>
        </div>

        <div className="confirm-modal__body">
          <p>{message}</p>
        </div>

        <div className="confirm-modal__footer">
          <Button type="button" variant="secondary" onClick={onCancel} disabled={isConfirming}>
            {cancelLabel}
          </Button>
          <Button type="button" variant={confirmVariant} onClick={onConfirm} disabled={isConfirming}>
            {isConfirming ? "Processing..." : confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
