export function FeatureIcon({ kind }) {
  return (
    <span className="material-symbols-outlined feature-icon__symbol" aria-hidden="true">
      {kind}
    </span>
  );
}
