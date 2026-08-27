import { LoginCard } from "../../components/auth/LoginCard";
import { HeroSection } from "../../components/landing/HeroSection";
import { AppFooter } from "../../components/layout/AppFooter";
import { featureCards, footerLinks } from "../../data/landingContent";
import { useGoogleAuth } from "../../hooks/useGoogleAuth";

export function LoginPage({ onLoginSuccess }) {
  const auth = useGoogleAuth({ onLoginSuccess });

  return (
    <main className="shell">
      <HeroSection cards={featureCards} />
      <LoginCard {...auth} />
      <AppFooter links={footerLinks} />
    </main>
  );
}
