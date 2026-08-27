export function Topbar({
  brand,
  navItems = [],
  actions,
  className = ""
}) {
  const classes = ["ui-topbar", className].filter(Boolean).join(" ");

  return (
    <header className={classes}>
      <div className="ui-topbar__brand">{brand}</div>

      <nav className="ui-topbar__nav" aria-label="Primary">
        {navItems.map((item) => (
          <a href={item.href || "/"} key={item.label}>
            {item.label}
          </a>
        ))}
      </nav>

      <div className="ui-topbar__actions">{actions}</div>
    </header>
  );
}
