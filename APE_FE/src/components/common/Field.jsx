export function Field({ label, hint, children, className = "" }) {
  const classes = ["ui-field", className].filter(Boolean).join(" ");

  return (
    <label className={classes}>
      {label ? <span className="ui-field__label">{label}</span> : null}
      {children}
      {hint ? <small className="ui-field__hint">{hint}</small> : null}
    </label>
  );
}
