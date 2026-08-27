export function AdminGlyph({ kind }) {
  if (kind === "cap") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="m4 10 8-4 8 4-8 4-8-4Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
        <path d="M7.5 11.75v3.2c0 .9 1.85 2.05 4.5 2.05s4.5-1.15 4.5-2.05v-3.2" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </svg>
    );
  }

  if (kind === "grid") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <rect x="4" y="4" width="6" height="6" rx="1.2" stroke="currentColor" strokeWidth="1.8" />
        <rect x="14" y="4" width="6" height="6" rx="1.2" stroke="currentColor" strokeWidth="1.8" />
        <rect x="4" y="14" width="6" height="6" rx="1.2" stroke="currentColor" strokeWidth="1.8" />
        <rect x="14" y="14" width="6" height="6" rx="1.2" stroke="currentColor" strokeWidth="1.8" />
      </svg>
    );
  }

  if (kind === "doc") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <rect x="6" y="4" width="12" height="16" rx="2" stroke="currentColor" strokeWidth="1.8" />
        <path d="M9 9h6M9 13h6M9 17h4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </svg>
    );
  }

  if (kind === "user") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <circle cx="12" cy="9" r="3" stroke="currentColor" strokeWidth="1.8" />
        <path d="M6.5 18c1.2-2.1 3-3.25 5.5-3.25S16.3 15.9 17.5 18" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </svg>
    );
  }

  if (kind === "spark") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="m12 4 1.65 3.85L17.5 9.5l-3.85 1.65L12 15l-1.65-3.85L6.5 9.5l3.85-1.65L12 4Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      </svg>
    );
  }

  if (kind === "search") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <circle cx="11" cy="11" r="5.25" stroke="currentColor" strokeWidth="1.8" />
        <path d="m16 16 3 3" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </svg>
    );
  }

  if (kind === "filter") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="M5 7h14M8 12h8M10 17h4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </svg>
    );
  }

  if (kind === "edit") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="m8 16 7.85-7.85 2 2L10 18H8v-2Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      </svg>
    );
  }

  if (kind === "copy") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <rect x="9" y="7" width="8" height="10" rx="1.8" stroke="currentColor" strokeWidth="1.8" />
        <path d="M7 15H6a1 1 0 0 1-1-1V6.8A1.8 1.8 0 0 1 6.8 5H13" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </svg>
    );
  }

  if (kind === "trash") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="M7 8h10M9 8V6.75A1.75 1.75 0 0 1 10.75 5h2.5A1.75 1.75 0 0 1 15 6.75V8M8 8l.6 9.2A1.9 1.9 0 0 0 10.5 19h3a1.9 1.9 0 0 0 1.9-1.8L16 8" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    );
  }

  if (kind === "plus") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="M12 7v10M7 12h10" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </svg>
    );
  }

  if (kind === "help") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <circle cx="12" cy="12" r="8" stroke="currentColor" strokeWidth="1.8" />
        <path d="M9.9 9.6a2.4 2.4 0 1 1 3.3 2.2c-.8.38-1.2.8-1.2 1.7" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        <circle cx="12" cy="16.8" r=".8" fill="currentColor" />
      </svg>
    );
  }

  if (kind === "wallet") {
    return (
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="M6 7.5A2.5 2.5 0 0 1 8.5 5h8a1.5 1.5 0 0 1 0 3h-8A1.5 1.5 0 0 0 7 9.5v7A2.5 2.5 0 0 0 9.5 19h8A2.5 2.5 0 0 0 20 16.5v-6A2.5 2.5 0 0 0 17.5 8H8.5A2.5 2.5 0 0 1 6 5.5v2Z" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
        <circle cx="16.5" cy="13.5" r="1" fill="currentColor" />
      </svg>
    );
  }

  return null;
}
