import { FeatureIcon } from "../icons/FeatureIcon";

export function FeatureCard({ icon, title, description }) {
  return (
    <article className="feature-card">
      <div className="feature-icon">
        <FeatureIcon kind={icon} />
      </div>
      <h2>{title}</h2>
      <p>{description}</p>
    </article>
  );
}
