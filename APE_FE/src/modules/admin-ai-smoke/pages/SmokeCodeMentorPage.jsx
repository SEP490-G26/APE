import { ROUTES } from "../../../lib/routes";
import { AdminSmokeScreenPage } from "./AdminSmokeScreenPage";

export function SmokeCodeMentorPage() {
  return (
    <AdminSmokeScreenPage
      activeRoute={ROUTES.adminAiSmokeCodeMentor}
      heroIcon="help"
      heroTitle="AI Smoke Tests"
      heroSubtitle="Step 3 - Code Mentor"
      title="Step 3 - Smoke Code Mentor"
      subtitle="Finish the smoke flow by running mentor feedback after content and generation checks are healthy."
      workflowStep="Step 3"
      kind="codeMentor"
      fields={[
        { key: "submission_id", label: "Submission Id", initialValue: "SUB-1002" },
        { key: "runtime_override", label: "Runtime Override", initialValue: "gemini-2.5-pro" },
        { key: "simulate_error", label: "Simulate Fallback Error", type: "checkbox", initialValue: false, checkboxLabel: "Simulate fallback-chain failure" }
      ]}
      parsedSections={[
        { title: "Submission", keys: ["submission_id", "feedback"] },
        { title: "Runtime Usage", keys: ["configured_model", "effective_model", "total_tokens", "cost_usd", "credits_deducted"] },
        { title: "Credit State", keys: ["remaining_free_credit", "remaining_paid_credit", "created_at", "fallback_used"] }
      ]}
    />
  );
}
