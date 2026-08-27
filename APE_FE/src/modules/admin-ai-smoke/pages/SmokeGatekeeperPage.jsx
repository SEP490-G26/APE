import { ROUTES } from "../../../lib/routes";
import { AdminSmokeScreenPage } from "./AdminSmokeScreenPage";

export function SmokeGatekeeperPage() {
  return (
    <AdminSmokeScreenPage
      activeRoute={ROUTES.adminAiSmokeGatekeeper}
      heroIcon="filter"
      heroTitle="AI Smoke Tests"
      heroSubtitle="Step 1 - Gatekeeper"
      title="Step 1 - Smoke Gatekeeper"
      subtitle="Start the smoke flow by checking whitelist classification, runtime override, and fallback behavior for source support checks."
      workflowStep="Step 1"
      kind="gatekeeper"
      fields={[
        {
          key: "source_file",
          label: "Source File",
          type: "file",
          hint: "Chon file local de FE tu nap ten file va noi dung vao smoke test.",
          fullWidth: true,
          accept: ".txt,.md,.json,.csv,.xml,.html,.htm,.c,.h,.cpp,.java,.js,.ts,.jsx,.tsx,.cs,.py,.sql"
        },
        { key: "content", label: "Content", type: "textarea", initialValue: "Binary search works on sorted arrays and halves the search space.", fullWidth: true },
        { key: "file_name", label: "File Name", initialValue: "binary-search-notes.pdf" },
        { key: "subject_hint", label: "Subject Hint", initialValue: "Data Structures & Algorithms" },
        { key: "language", label: "Language", initialValue: "en" },
        { key: "runtime_override", label: "Runtime Override", initialValue: "gpt-4.1-mini" },
        { key: "simulate_error", label: "Simulate Fallback Error", type: "checkbox", initialValue: false, checkboxLabel: "Simulate fallback-chain failure" }
      ]}
      parsedSections={[
        { title: "Verdict", keys: ["verdict", "reason", "confidence", "primary_domain"] },
        { title: "Topic Detection", keys: ["matched_subjects", "detected_topics", "is_supported"] },
        { title: "Usage", keys: ["configured_model", "effective_model", "total_tokens", "cost_usd", "fallback_used"] }
      ]}
    />
  );
}
