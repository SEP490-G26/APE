export function Avatar({ name, src, className = "", size = "md" }) {
  const classes = ["ui-avatar", `ui-avatar--${size}`, className].filter(Boolean).join(" ");
  const initials = (name || "?")
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();

  return (
    <div className={classes} aria-label={name}>
      {src ? <img src={src} alt={name} /> : <span>{initials}</span>}
    </div>
  );
}
