export function TextInput({ className = "", readOnly = false, ...props }) {
  const classes = [
    "ui-text-input",
    readOnly ? "ui-text-input--readonly" : "",
    className
  ]
    .filter(Boolean)
    .join(" ");

  return <input className={classes} readOnly={readOnly} {...props} />;
}
