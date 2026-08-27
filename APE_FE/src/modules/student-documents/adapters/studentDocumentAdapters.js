export function formatIngestionStageLabel(stage) {
  switch (stage) {
    case "ready_for_generation":
      return "Ready";
    case "duplicate_reused":
      return "Reused";
    case "normalized_only":
      return "Normalized";
    case "gatekeeper_passed":
      return "Gatekeeper Passed";
    case "embedded":
      return "Embedded";
    case "processing":
      return "Processing";
    case "failed":
      return "Failed";
    default:
      return stage || "Unknown";
  }
}

export function getDocumentPrimaryAction(document) {
  if (!document) {
    return null;
  }

  if (document.isReadyForGeneration) {
    return "generate";
  }

  if (document.hasExtractionDraft || document.lastExtractionDraftId) {
    return "draft";
  }

  return "preview";
}

export function getDuplicateMessage(document) {
  if (!document?.duplicateDetected) {
    return "";
  }

  if (document.duplicateMatchType === "file_checksum") {
    return "This file matches a previously uploaded file. The existing result was reused, so chunking was skipped and no extra fee was charged.";
  }

  if (document.duplicateMatchType === "normalized_content_checksum") {
    return "This document matches existing content. The existing result was reused, so chunking was skipped and no extra fee was charged.";
  }

  return "This document already exists in your workspace. The previous result was reused, so no extra cost was charged.";
}
