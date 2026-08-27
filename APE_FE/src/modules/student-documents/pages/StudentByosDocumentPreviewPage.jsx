import { useEffect, useState } from "react";
import { Button } from "../../../components/common";
import { StudentScaffold } from "../../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../../lib/routes";
import { formatIngestionStageLabel } from "../adapters/studentDocumentAdapters";
import {
  getStudentDocumentChapters,
  getStudentDocumentPreview,
  getStudentDocumentTopics
} from "../services/studentDocumentService";

export function StudentByosDocumentPreviewPage({ documentId }) {
  const [preview, setPreview] = useState(null);
  const [topics, setTopics] = useState(null);
  const [chapters, setChapters] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let mounted = true;

    async function loadPreview() {
      setIsLoading(true);
      setError("");

      try {
        const [previewResult, topicResult, chapterResult] = await Promise.all([
          getStudentDocumentPreview(documentId),
          getStudentDocumentTopics(documentId).catch(() => null),
          getStudentDocumentChapters(documentId).catch(() => null)
        ]);

        if (!mounted) {
          return;
        }

        setPreview(previewResult);
        setTopics(topicResult);
        setChapters(chapterResult);
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load document preview.");
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    }

    loadPreview();

    return () => {
      mounted = false;
    };
  }, [documentId]);

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentByosDocuments}
      title="Document Preview"
      subtitle={`Preview runtime state, extracted text, topics, and chapter scope for document ${documentId}.`}
      actions={
        <div className="ai-inline-actions">
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentByosDocuments)}>Back to Documents</Button>
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentJourney)}>Open Setup</Button>
        </div>
      }
    >
      <section className="ai-student-shell">
        <article className="ai-student-panel">
          <div className="ai-student-panel__header">
            <div>
              <p className="ai-student-kicker">Preview</p>
              <h2>Parsed metadata</h2>
            </div>
            {preview ? (
              <strong className={`ai-badge ai-badge--${preview.ingestionStage}`}>
                {formatIngestionStageLabel(preview.ingestionStage)}
              </strong>
            ) : null}
          </div>
          {isLoading ? <div className="ai-student-callout">Loading preview...</div> : null}
          {error ? <div className="student-status-banner">{error}</div> : null}
          {preview ? (
            <div className="ai-detail-list">
              <div><span>Document ID</span><strong>{preview.documentId || preview.id}</strong></div>
              <div><span>File Name</span><strong>{preview.fileName || "-"}</strong></div>
              <div><span>File Type</span><strong>{preview.fileType || "-"}</strong></div>
              <div><span>Source</span><strong>{preview.source || "BYOS"}</strong></div>
              <div><span>Subject Code</span><strong>{preview.subjectCode || "-"}</strong></div>
              <div><span>Gatekeeper Verdict</span><strong>{preview.gatekeeperVerdict || "-"}</strong></div>
              <div><span>Ingestion Status</span><strong>{preview.ingestionStatus || "-"}</strong></div>
              <div><span>Chunk Count</span><strong>{preview.chunkCount || 0}</strong></div>
              <div><span>Topic Tag Count</span><strong>{preview.tags?.length || 0}</strong></div>
              <div><span>Ready for Generation</span><strong>{String(Boolean(preview.isReadyForGeneration))}</strong></div>
            </div>
          ) : null}
        </article>

        <article className="ai-student-panel">
          <div className="ai-student-panel__header">
            <div>
              <p className="ai-student-kicker">Preview Text</p>
              <h2>Readable excerpt panel</h2>
            </div>
            <Button variant="primary" onClick={() => navigateTo(ROUTES.studentByosGenerate(documentId))} disabled={!preview?.isReadyForGeneration}>
              Generate From This Document
            </Button>
          </div>
          <div className="ai-preview-block">
            {preview?.previewText || "No preview text available."}
          </div>
          {preview?.cleanupWarnings?.length ? (
            <div className="ai-student-callout">
              <strong>Cleanup Warnings</strong>
              <ul className="ai-bullet-list">
                {preview.cleanupWarnings.map((warning) => (
                  <li key={warning}>{warning}</li>
                ))}
              </ul>
            </div>
          ) : null}
          {topics?.topics?.length ? (
            <>
              <div className="ai-student-panel__header">
                <div>
                  <p className="ai-student-kicker">Topic Summary</p>
                  <h2>Detected retrieval tags</h2>
                </div>
              </div>
              <div className="ai-topic-list">
                {topics.topics.map((topic) => (
                  <span key={topic.tag} className="ai-chip">{topic.tag}</span>
                ))}
              </div>
            </>
          ) : null}
          {chapters?.chapters?.length ? (
            <>
              <div className="ai-student-panel__header">
                <div>
                  <p className="ai-student-kicker">Chapter Scope</p>
                  <h2>Current chapter candidates</h2>
                </div>
              </div>
              <div className="ai-chapter-list">
                {chapters.chapters.map((chapter) => (
                  <article key={chapter.chapterKey} className="ai-chapter-card">
                    <strong>{chapter.chapterTitle}</strong>
                    <p>{chapter.overviewShort || "No overview available."}</p>
                    <small>{chapter.chunkCount || 0} chunks • {chapter.coveredTopics?.length || 0} topics</small>
                  </article>
                ))}
              </div>
            </>
          ) : null}
          <div className="ai-inline-actions ai-inline-actions--footer">
            <Button variant="secondary" onClick={() => navigateTo(ROUTES.studentByosExtractionDraft(documentId))}>
              Open Extraction Draft
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
