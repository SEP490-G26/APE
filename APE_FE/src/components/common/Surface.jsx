export function Surface({ as: Component = "div", children, className = "", ...props }) {
  const classes = ["ui-surface", className].filter(Boolean).join(" ");

  return (
    <Component className={classes} {...props}>
      {children}
    </Component>
  );
}
