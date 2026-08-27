export function InFlightNotice({ title = "Processing", description }) {
  return (
    <div className="ai-inflight-notice" role="status" aria-live="polite">
      <div className="ai-inflight-notice__spinner" aria-hidden="true" />
      <div className="ai-inflight-notice__copy">
        <strong>{title}</strong>
        {description ? <span>{description}</span> : null}
      </div>
    </div>
  );
}
