import { ROUTES } from "../../../lib/routes";
import { AdminSmokeScreenPage } from "./AdminSmokeScreenPage";

export function SmokeEmbeddingPage() {
  return (
    <AdminSmokeScreenPage
      activeRoute={ROUTES.adminAiSmokeEmbedding}
      heroIcon="spark"
      heroTitle="AI Smoke Tests"
      heroSubtitle="Step 1 - Embedding + AutoTagging"
      title="Step 1 - Smoke Embedding + AutoTagging"
      subtitle="Finish step 1 by checking chunking, embedding usage, tagging usage, and model override behavior for document processing."
      workflowStep="Step 1"
      kind="embedding"
      fields={[
        { key: "document_id", label: "Document Id", initialValue: "DOC-14" },
        { key: "course_id", label: "Course Id", initialValue: "CSD201" },
        { key: "source_type", label: "Source Type", initialValue: "SYSTEM" },
        { key: "embedding_override", label: "Embedding Override", initialValue: "text-embedding-3-large" },
        { key: "tagging_override", label: "Tagging Override", initialValue: "gemini-2.5-flash" },
        { key: "content", label: "Content", type: "textarea", initialValue: "Binary search and merge sort are core algorithmic patterns for sorted data.", fullWidth: true },
        { key: "simulate_error", label: "Simulate Fallback Error", type: "checkbox", initialValue: false, checkboxLabel: "Simulate fallback-chain failure" }
      ]}
      parsedSections={[
        { title: "Chunks", keys: ["chunks"] },
        { title: "Embedding Usage", keys: ["embedding_usage"] },
        { title: "Tagging Usage", keys: ["tagging_usage"] }
      ]}
    />
  );
}
