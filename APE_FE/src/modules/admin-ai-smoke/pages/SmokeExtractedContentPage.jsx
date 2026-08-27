import { ROUTES } from "../../../lib/routes";
import { AdminSmokeScreenPage } from "./AdminSmokeScreenPage";

export function SmokeExtractedContentPage() {
  return (
    <AdminSmokeScreenPage
      activeRoute={ROUTES.adminAiSmokeExtractedContent}
      heroIcon="grid"
      heroTitle="AI Smoke Tests"
      heroSubtitle="Step 1 - Extracted Content"
      title="Step 1 - Smoke Extracted Content"
      subtitle="Continue step 1 by validating AI normalization, text-vs-vision mode, and parser metadata for extracted content cleanup."
      workflowStep="Step 1"
      kind="extracted"
      fields={[
        { key: "file_name", label: "File Name", initialValue: "merge-sort-slides.pdf" },
        { key: "source_type", label: "Source Type", initialValue: "pdf" },
        { key: "parser_name", label: "Parser Name", initialValue: "pdf-text" },
        { key: "estimated_page_count", label: "Estimated Page Count", type: "number", initialValue: 18 },
        { key: "is_vision_recommended", label: "Vision Recommended", type: "checkbox", initialValue: false, checkboxLabel: "Use vision path if needed" },
        { key: "warnings", label: "Warnings", initialValue: "Figure captions detected" },
        { key: "runtime_override", label: "Runtime Override", initialValue: "gemini-2.5-flash" },
        { key: "raw_text", label: "Raw Text", type: "textarea", initialValue: "Merge sort is a divide and conquer algorithm. Figure 2 shows the merge process...", fullWidth: true },
        { key: "simulate_error", label: "Simulate Fallback Error", type: "checkbox", initialValue: false, checkboxLabel: "Simulate fallback-chain failure" }
      ]}
      parsedSections={[
        { title: "Normalization", keys: ["normalized_markdown", "used_ai_normalization", "extraction_mode"] },
        { title: "Parser Metadata", keys: ["parser_name", "vision_model", "word_count", "detected_image_references", "warnings"] },
        { title: "Usage", keys: ["configured_model", "effective_model", "total_tokens", "cost_usd", "fallback_used"] }
      ]}
    />
  );
}
