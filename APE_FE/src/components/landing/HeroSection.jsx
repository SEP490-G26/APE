import { BrandMark } from "../icons/BrandMark";
import { FeatureCard } from "./FeatureCard";

export function HeroSection({ cards }) {
  return (
    <section className="hero-panel">
      <header className="brand-row">
        <BrandMark />
        <div className="brand-copy">
          <span className="brand-ape">APE</span>
          <span className="brand-platform">Platform</span>
        </div>
      </header>

      <div className="hero-copy">
        <p className="eyebrow">FPT University assessment workspace</p>
        <h1>Examination and programming practice for FPT University</h1>
        <p className="hero-description">
          One place for protected exams, browser-based coding, and instant evaluation
          built for serious technical practice.
        </p>
      </div>

      <div className="feature-grid">
        {cards.map((card) => (
          <FeatureCard
            key={card.title}
            icon={card.icon}
            title={card.title}
            description={card.description}
          />
        ))}
      </div>
    </section>
  );
}
