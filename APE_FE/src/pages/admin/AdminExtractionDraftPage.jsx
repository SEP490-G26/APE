import { useEffect, useState } from "react";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { Button } from "../../components/common";
import { ROUTES, navigateTo, getRouteParamAfterPrefix } from "../../lib/routes";
import { getAdminDocumentExtractionDraft } from "../../services/adminDocumentService";

function formatIngestionStageLabel(value) {
  const normalized = String(value || "").trim().toLowerCase();
  if (!normalized) {
    return "Unknown";
  }
  if (normalized === "ready") {
    return "Ready";
  }
  if (normalized === "auto_ingested" || normalized === "auto-ingested") {
    return "Auto-ingested";
  }
  if (normalized === "ingesting") {
    return "Ingesting";
  }
  if (normalized === "failed") {
    return "Failed";
  }
  return normalized.replace(/_/g, " ").replace(/\b\w/g, (m) => m.toUpperCase());
}

export function AdminExtractionDraftPage() {
  const documentId = getRouteParamAfterPrefix(window.location.pathname, ROUTES.documentManagement, "/extraction-draft/") || "";
  const [draft, setDraft] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    async function load() {
      if (!documentId) {
        setDraft(null);
        setError("Document id is missing.");
        return;
      }

      setIsLoading(true);
      setError("");

      try {
        const payload = await getAdminDocumentExtractionDraft(documentId);
        if (!ignore) {
          setDraft(payload);
        }
      } catch (e) {
        if (!ignore) {
          setDraft(null);
          setError(e?.message || "Unable to load extraction draft.");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    load();
    return () => {
      ignore = true;
    };
  }, [documentId]);

  return (
    <AdminScaffold
      activeRoute={ROUTES.documentManagement}
      heroIcon="document"
      heroTitle="Document Management"
      heroSubtitle="System ingestion"
      title="Extraction Draft"
      subtitle={`Extraction draft details for document ${documentId}, including structure candidates, image enrichment counts, and chunking readiness.`}
    >
      <section className="student-page-grid">
        <article className="student-panel student-panel--wide">
          <div className="student-panel__header">
            <div>
              <h2>Ingest Snapshot</h2>
              <p>Extraction status</p>
            </div>
            <div className="student-panel__actions">
              <Button variant="ghost" onClick={() => navigateTo(ROUTES.documentManagement)}>
                Open Setup
              </Button>
              <Button
                variant="ghost"
                onClick={() => navigateTo(ROUTES.adminDocumentGenerate(documentId))}
                disabled={!draft?.isReadyForGeneration}
              >
                Open Preview
              </Button>
            </div>
          </div>

          {isLoading ? <div className="ai-student-callout">Loading extraction draft...</div> : null}
          {error ? <div className="student-status-banner">{error}</div> : null}

          {draft ? (
            <>
              <div className="ai-student-stat-grid">
                <div>
                  <span>Review Status</span>
                  <strong>{draft.reviewStatus || "-"}</strong>
                </div>
                <div>
                  <span>Approved Segments</span>
                  <strong>{draft.approvedSegments ?? 0}</strong>
                </div>
                <div>
                  <span>Rejected Segments</span>
                  <strong>{draft.rejectedSegments ?? 0}</strong>
                </div>
                <div>
                  <span>Chunking Ready</span>
                  <strong>{String(Boolean(draft.chunkingReady))}</strong>
                </div>
                <div>
                  <span>Ready for Generation</span>
                  <strong>{String(Boolean(draft.isReadyForGeneration))}</strong>
                </div>
                <div>
                  <span>Last Embedding Run</span>
                  <strong>{draft.lastEmbeddingRunId || "-"}</strong>
                </div>
              </div>

              <div className="student-panel__divider" />

              <div className="student-panel__header">
                <div>
                  <h2>Draft Content</h2>
                  <p>Clean markdown preview</p>
                </div>
                <Button
                  variant="primary"
                  onClick={() => navigateTo(ROUTES.adminDocumentGenerate(documentId))}
                  disabled={!draft.isReadyForGeneration}
                >
                  Go To Generation
                </Button>
              </div>

              <div className="ai-student-preview-block">
                {draft.cleanMarkdownPreview || "No extraction draft preview available."}
              </div>

              {draft.cleanupWarnings?.length ? (
                <div className="ai-student-callout">
                  <strong>Cleanup Warnings</strong>
                  <ul className="ai-bullet-list">
                    {draft.cleanupWarnings.map((warning) => (
                      <li key={warning}>{warning}</li>
                    ))}
                  </ul>
                </div>
              ) : null}

              <div className="ai-student-stat-grid ai-student-stat-grid--details">
                <div>
                  <span>Extraction Status</span>
                  <strong className={`ai-badge ai-badge--${draft.ingestionStage || "processing"}`}>
                    {formatIngestionStageLabel(draft.ingestionStage)}
                  </strong>
                </div>
                <div>
                  <span>Document Id</span>
                  <strong>{draft.documentId || "-"}</strong>
                </div>
                <div>
                  <span>Draft Id</span>
                  <strong>{draft.draftId || "-"}</strong>
                </div>
                <div>
                  <span>Subject Code</span>
                  <strong>{draft.subjectCode || "-"}</strong>
                </div>
                <div>
                  <span>Extraction Mode</span>
                  <strong>{draft.extractionMode || "-"}</strong>
                </div>
                <div>
                  <span>Ingest Parser</span>
                  <strong>{draft.ingestParser || "-"}</strong>
                </div>
                <div>
                  <span>Vision Model</span>
                  <strong>{draft.visionModel || "-"}</strong>
                </div>
                <div>
                  <span>Total Pages</span>
                  <strong>{draft.totalPages || 0}</strong>
                </div>
                <div>
                  <span>Total Segments</span>
                  <strong>{draft.totalSegments || 0}</strong>
                </div>
                <div>
                  <span>Image Placeholders</span>
                  <strong>{draft.detectedImagePlaceholderCount || 0}</strong>
                </div>
                <div>
                  <span>Embedded Images</span>
                  <strong>{draft.embeddedImageCount || 0}</strong>
                </div>
                <div>
                  <span>Vision Enrichment Success</span>
                  <strong>{draft.visionEnrichmentSucceededCount || 0}/{draft.visionEnrichmentAttemptedCount || 0}</strong>
                </div>
                <div>
                  <span>Vision Enrichment Failed</span>
                  <strong>{draft.visionEnrichmentFailedCount || 0}</strong>
                </div>
                <div>
                  <span>Unresolved Image Placeholders</span>
                  <strong>{draft.unresolvedImagePlaceholderCount || 0}</strong>
                </div>
                <div>
                  <span>Chunk Count</span>
                  <strong>{draft.chunkCount || 0}</strong>
                </div>
                <div>
                  <span>Has Embedded Chunks</span>
                  <strong>{String(Boolean(draft.hasEmbeddedChunks))}</strong>
                </div>
              </div>
            </>
          ) : null}
        </article>
      </section>
    </AdminScaffold>
  );
}

