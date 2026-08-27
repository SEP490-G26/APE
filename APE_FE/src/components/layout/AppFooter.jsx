export function AppFooter({
  links,
  className = "",
  brand = "APE Platform",
  description = "© 2026 FPT University • AI-Powered Examination & Programming Practice Platform",
  navLabel = "Footer links"
}) {
  return (
    <footer className={["app-footer", className].filter(Boolean).join(" ")}>
      <div>
        <strong>{brand}</strong>
        <p>{description}</p>
      </div>

      <nav aria-label={navLabel}>
        {links.map((link) => (
          <a key={link.label} href={link.href}>
            {link.label}
          </a>
        ))}
      </nav>
    </footer>
  );
}
