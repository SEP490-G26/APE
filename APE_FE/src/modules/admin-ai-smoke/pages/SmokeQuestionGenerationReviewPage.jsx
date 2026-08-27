import { ROUTES } from "../../../lib/routes";
import { AdminSmokeScreenPage } from "./AdminSmokeScreenPage";

export function SmokeQuestionGenerationReviewPage() {
  return (
    <AdminSmokeScreenPage
      activeRoute={ROUTES.adminAiSmokeQuestionGenerationReview}
      heroIcon="doc"
      heroTitle="AI Smoke Tests"
      heroSubtitle="Step 2 - Generation + Review"
      title="Step 2 - Smoke Generation + Review"
      subtitle="Move to step 2 once upstream content checks are stable, then run generator and reviewer together."
      workflowStep="Step 2"
      kind="questionGenerationReview"
      fields={[
        { key: "course_id", label: "Course Id", initialValue: "CSD201" },
        { key: "document_id", label: "Document Id", initialValue: "DOC-14" },
        { key: "subject", label: "Subject", initialValue: "Searching and Sorting" },
        { key: "difficulty", label: "Difficulty", initialValue: "Medium" },
        { key: "question_type", label: "Question Type", initialValue: "FE" },
        { key: "count", label: "Count", type: "number", initialValue: 2 },
        { key: "mode", label: "Mode", initialValue: "review" },
        { key: "generator_override", label: "Generator Override", initialValue: "gpt-4.1" },
        { key: "reviewer_override", label: "Reviewer Override", initialValue: "gpt-4.1-mini" },
        { key: "persist_questions", label: "Persist Questions", type: "checkbox", initialValue: false, checkboxLabel: "Persist generated questions" },
        { key: "chunks", label: "Chunks", initialValue: "chunk-510, chunk-511" },
        { key: "simulate_error", label: "Simulate Fallback Error", type: "checkbox", initialValue: false, checkboxLabel: "Simulate fallback-chain failure" }
      ]}
      parsedSections={[
        { title: "Run Summary", keys: ["subject", "question_type", "difficulty", "mode", "attempts_used"] },
        { title: "Models / Review", keys: ["generator_model", "reviewer_model", "review", "questions"] },
        { title: "Persistence / Metrics", keys: ["persistence", "context_pack", "retrieval_plan", "metrics"] }
      ]}
    />
  );
}
