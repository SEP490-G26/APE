import { useEffect, useState } from "react";
import { Button } from "../../../components/common";
import { StudentScaffold } from "../../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../../lib/routes";
import { formatIngestionStageLabel } from "../adapters/studentDocumentAdapters";
import { getStudentDocumentExtractionDraft } from "../services/studentDocumentService";

export function StudentByosExtractionDraftPage({ documentId }) {
  const [draft, setDraft] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let mounted = true;

    async function loadDraft() {
      setIsLoading(true);
      setError("");

      try {
        const draftResult = await getStudentDocumentExtractionDraft(documentId);

        if (!mounted) {
          return;
        }

        setDraft(draftResult);
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load extraction draft.");
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    }

    loadDraft();

    return () => {
      mounted = false;
    };
  }, [documentId]);

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentByosDocuments}
      title="Extraction Draft"
      subtitle={`Extraction draft details for document ${documentId}, including structure candidates, image enrichment counts, and chunking readiness.`}
      actions={
        <div className="ai-inline-actions">
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentByosPreview(documentId))}>Open Preview</Button>
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentJourney)}>Open Setup</Button>
        </div>
      }
    >
      <section className="ai-student-shell">
        <article className="ai-student-panel">
          <div className="ai-student-panel__header">
            <div>
              <p className="ai-student-kicker">Ingest Snapshot</p>
              <h2>Extraction status</h2>
            </div>
            {draft ? (
              <strong className={`ai-badge ai-badge--${draft.ingestionStage}`}>
                {formatIngestionStageLabel(draft.ingestionStage)}
              </strong>
            ) : null}
          </div>
          {isLoading ? <div className="ai-student-callout">Loading extraction draft...</div> : null}
          {error ? <div className="student-status-banner">{error}</div> : null}
          {draft ? (
            <>
              <div className="ai-student-grid ai-student-grid--three">
                <div className="ai-meta-card">
                  <span>Review Status</span>
                  <strong>{draft.reviewStatus || "-"}</strong>
                </div>
                <div className="ai-meta-card">
                  <span>Approved Segments</span>
                  <strong>{draft.approvedSegments ?? 0}</strong>
                </div>
                <div className="ai-meta-card">
                  <span>Rejected Segments</span>
                  <strong>{draft.rejectedSegments ?? 0}</strong>
                </div>
              </div>

              <div className="ai-detail-list">
                <div><span>Chunking Ready</span><strong>{String(Boolean(draft.chunkingReady))}</strong></div>
                <div><span>Ready for Generation</span><strong>{String(Boolean(draft.isReadyForGeneration))}</strong></div>
                <div><span>Last Embedding Run</span><strong>{draft.lastEmbeddingRunId || "-"}</strong></div>
              </div>
            </>
          ) : null}
        </article>

        <article className="ai-student-panel">
          <div className="ai-student-panel__header">
            <div>
              <p className="ai-student-kicker">Draft Content</p>
              <h2>Clean markdown preview</h2>
            </div>
            <Button variant="primary" onClick={() => navigateTo(ROUTES.studentByosGenerate(documentId))}>Go To Generation</Button>
          </div>
          {draft ? (
            <div className="ai-preview-block">
              {draft.cleanMarkdownPreview || "No extraction draft preview available."}
            </div>
          ) : null}
          {draft?.cleanupWarnings?.length ? (
            <div className="ai-student-callout">
              <strong>Cleanup Warnings</strong>
              <ul className="ai-bullet-list">
                {draft.cleanupWarnings.map((warning) => (
                  <li key={warning}>{warning}</li>
                ))}
              </ul>
            </div>
          ) : null}
          {draft ? (
            <div className="ai-detail-list">
              <div><span>Draft Id</span><strong>{draft.draftId || "-"}</strong></div>
              <div><span>Document Id</span><strong>{draft.documentId || "-"}</strong></div>
              <div><span>Subject Code</span><strong>{draft.subjectCode || "-"}</strong></div>
              <div><span>Extraction Mode</span><strong>{draft.extractionMode || "-"}</strong></div>
              <div><span>Ingest Parser</span><strong>{draft.ingestParser || "-"}</strong></div>
              <div><span>Vision Model</span><strong>{draft.visionModel || "-"}</strong></div>
              <div><span>Total Pages</span><strong>{draft.totalPages || 0}</strong></div>
              <div><span>Total Segments</span><strong>{draft.totalSegments || 0}</strong></div>
              <div><span>Image Placeholders</span><strong>{draft.detectedImagePlaceholderCount || 0}</strong></div>
              <div><span>Embedded Images</span><strong>{draft.embeddedImageCount || 0}</strong></div>
              <div><span>Vision Enrichment Success</span><strong>{draft.visionEnrichmentSucceededCount || 0}/{draft.visionEnrichmentAttemptedCount || 0}</strong></div>
              <div><span>Vision Enrichment Failed</span><strong>{draft.visionEnrichmentFailedCount || 0}</strong></div>
              <div><span>Unresolved Image Placeholders</span><strong>{draft.unresolvedImagePlaceholderCount || 0}</strong></div>
              <div><span>Chunk Count</span><strong>{draft.chunkCount || 0}</strong></div>
              <div><span>Has Embedded Chunks</span><strong>{String(Boolean(draft.hasEmbeddedChunks))}</strong></div>
            </div>
          ) : null}
          {draft?.candidateTitles?.length ? (
            <div className="ai-student-callout">
              <strong>Candidate Titles</strong>
              <div className="ai-topic-list">
                {draft.candidateTitles.map((title) => (
                  <span key={title} className="ai-chip">{title}</span>
                ))}
              </div>
            </div>
          ) : null}
          {draft?.candidateChapterMarkers?.length ? (
            <div className="ai-student-callout">
              <strong>Candidate Chapter Markers</strong>
              <div className="ai-topic-list">
                {draft.candidateChapterMarkers.map((title) => (
                  <span key={title} className="ai-chip">{title}</span>
                ))}
              </div>
            </div>
          ) : null}
          {draft?.cleanDisplayTitleCandidates?.length ? (
            <div className="ai-student-callout">
              <strong>Clean Display Titles</strong>
              <div className="ai-topic-list">
                {draft.cleanDisplayTitleCandidates.map((title) => (
                  <span key={title} className="ai-chip">{title}</span>
                ))}
              </div>
            </div>
          ) : null}
          {draft?.rejectedHeadingCandidates?.length ? (
            <div className="ai-student-callout">
              <strong>Rejected Heading Candidates</strong>
              <div className="ai-topic-list">
                {draft.rejectedHeadingCandidates.map((title) => (
                  <span key={title} className="ai-chip">{title}</span>
                ))}
              </div>
            </div>
          ) : null}
          <div className="ai-inline-actions ai-inline-actions--footer">
            <Button variant="secondary" onClick={() => navigateTo(ROUTES.studentByosGenerate(documentId))}>
              Continue To Generation
            </Button>
            <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentByosDocuments)}>
              Back to Document List
            </Button>
          </div>
        </article>
      </section>
    </StudentScaffold>
  );
}
