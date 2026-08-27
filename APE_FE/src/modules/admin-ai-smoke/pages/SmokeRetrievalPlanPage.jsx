import { ROUTES } from "../../../lib/routes";
import { AdminSmokeScreenPage } from "./AdminSmokeScreenPage";

export function SmokeRetrievalPlanPage() {
  return (
    <AdminSmokeScreenPage
      activeRoute={ROUTES.adminAiSmokeRetrievalPlan}
      heroIcon="search"
      heroTitle="AI Smoke Tests"
      heroSubtitle="Support - Retrieval Plan"
      title="Support - Smoke Retrieval Plan"
      subtitle="Optional support check for topic-bounded retrieval, chunk selection reasons, and token packing before generation."
      kind="retrievalPlan"
      fields={[
        { key: "user_id", label: "User Id", initialValue: "student-001" },
        { key: "course_id", label: "Course Id", initialValue: "CSD201" },
        { key: "document_id", label: "Document Id", initialValue: "DOC-14" },
        { key: "chapter_key", label: "Chapter Key", initialValue: "chapter-02" },
        { key: "subject", label: "Subject", initialValue: "Searching and Sorting" },
        { key: "question_type", label: "Question Type", initialValue: "FE" },
        { key: "difficulty", label: "Difficulty", initialValue: "Medium" },
        { key: "target_topics", label: "Target Topics", initialValue: "binary search, complexity" },
        { key: "retrieval_query", label: "Retrieval Query", initialValue: "binary search complexity sorted arrays" },
        { key: "max_candidate_count", label: "Max Candidate Count", type: "number", initialValue: 24 },
        { key: "max_packed_tokens", label: "Max Packed Tokens", type: "number", initialValue: 2200 },
        { key: "include_chunk_text", label: "Include Chunk Text", type: "checkbox", initialValue: false, checkboxLabel: "Include chunk text in output" },
        { key: "simulate_error", label: "Simulate Fallback Error", type: "checkbox", initialValue: false, checkboxLabel: "Simulate fallback-chain failure" }
      ]}
      parsedSections={[
        { title: "Plan Summary", keys: ["source_scope", "course_id", "document_id", "chapter_key", "subject", "question_type", "difficulty"] },
        { title: "Coverage", keys: ["covered_topics", "uncovered_topics", "topic_coverage_ratio", "dominant_chapter_key"] },
        { title: "Retrieved Chunks", keys: ["retrieved_chunks", "candidate_stats", "token_budget", "context_pack"] }
      ]}
    />
  );
}
