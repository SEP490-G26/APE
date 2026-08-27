export function IconButton({ children, className = "", ...props }) {
  const classes = ["ui-icon-button", className].filter(Boolean).join(" ");

  return (
    <button type="button" className={classes} {...props}>
      {children}
    </button>
  );
}
