import { useEffect, useMemo, useState } from "react";
import { Button } from "../../../components/common";
import { StudentScaffold } from "../../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../../lib/routes";
import { useInFlightGuard, useLockedFormSnapshot } from "../../../shared/guards";
import { ConfirmModal, InFlightNotice, ToastNotification } from "../../../shared/ui";
import {
  formatIngestionStageLabel,
  getDocumentPrimaryAction,
  getDuplicateMessage
} from "../adapters/studentDocumentAdapters";
import {
  deleteStudentDocument,
  downloadStudentDocument,
  getStudentCourses,
  getStudentDocuments,
  uploadByosDocument
} from "../services/studentDocumentService";

function getCourseLabel(course) {
  if (!course) {
    return "selected course";
  }

  return [course.code, course.name].filter(Boolean).join(" - ") || "selected course";
}

function formatUploadError(error, selectedCourse) {
  const rawMessage = String(error?.message || "").trim();

  if (!rawMessage) {
    return "Document upload failed. Please try again.";
  }

  if (rawMessage.includes("Document content does not match the selected course.")) {
    return `This document does not match ${getCourseLabel(selectedCourse)}. Please choose the correct course or upload a more relevant document.`;
  }

  if (rawMessage.includes("Document content is not supported by AI gatekeeper.")) {
    return "This document is outside the currently supported AI subjects or does not contain enough supported content to process.";
  }

  if (
    rawMessage.includes("The AI provider authentication failed") ||
    rawMessage.includes("He thong AI dang bi loi xac thuc voi nha cung cap") ||
    rawMessage.includes("Invalid API key") ||
    rawMessage.includes("Dich vu AI dang bi tu choi xac thuc")
  ) {
    return "The AI provider authentication is currently misconfigured. Your document was not processed. Please contact the admin or try again later.";
  }

  if (
    rawMessage.includes("The AI model or endpoint is configured incorrectly") ||
    rawMessage.includes("He thong AI dang cau hinh sai model hoac endpoint") ||
    rawMessage.includes("model or endpoint")
  ) {
    return "The AI model or endpoint is misconfigured. Your document was not processed. Please contact the admin.";
  }
  if (rawMessage.includes("Only PDF, DOCX, PPTX, and TXT files are supported")) {
    return "Only PDF, DOCX, PPTX, and TXT files are supported.";
  }

  if (rawMessage.includes("File size must not exceed 2 MB")) {
    return "The file size exceeds 2 MB.";
  }

  if (rawMessage.includes("Maximum 10 personal documents reached")) {
    return "You have reached the maximum of 10 personal documents.";
  }

  return rawMessage;
}

export function StudentByosDocumentsPage() {
  const [courses, setCourses] = useState([]);
  const [documents, setDocuments] = useState([]);
  const [selectedCourseId, setSelectedCourseId] = useState("");
  const [selectedFile, setSelectedFile] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [deletingDocumentId, setDeletingDocumentId] = useState("");
  const [downloadingDocumentId, setDownloadingDocumentId] = useState("");
  const [documentPendingDelete, setDocumentPendingDelete] = useState(null);
  const [toast, setToast] = useState(null);
  const uploadScopeSnapshot = useLockedFormSnapshot(
    {
      selectedCourseId,
      fileName: selectedFile?.name || ""
    },
    isUploading
  );

  useInFlightGuard(isUploading);

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  useEffect(() => {
    let mounted = true;

    async function loadInitialData() {
      setIsLoading(true);

      try {
        const [courseResult, documentResult] = await Promise.all([
          getStudentCourses(),
          getStudentDocuments()
        ]);

        if (!mounted) {
          return;
        }

        const nextCourses = courseResult?.items || [];
        setCourses(nextCourses);
        setSelectedCourseId((current) => current || nextCourses[0]?.id || nextCourses[0]?.code || "");
        setDocuments(documentResult?.items || []);
      } catch (loadError) {
        if (mounted) {
          setToast({ type: "error", message: loadError.message || "Unable to load My Documents." });
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    }

    loadInitialData();

    return () => {
      mounted = false;
    };
  }, []);

  async function handleUpload() {
    if (!selectedFile) {
      setToast({ type: "error", message: "Please choose a document file." });
      return;
    }

    if (!selectedCourseId) {
      setToast({ type: "error", message: "Please choose a course." });
      return;
    }

    setIsUploading(true);
    setToast(null);

    try {
      const uploaded = await uploadByosDocument({
        file: selectedFile,
        courseId: selectedCourseId
      });

      if (!uploaded.duplicateDetected) {
        setDocuments((current) => [uploaded, ...current.filter((item) => item.id !== uploaded.id)]);
      }
      setSelectedFile(null);
      setToast({
        type: "success",
        message: uploaded.duplicateDetected
          ? getDuplicateMessage(uploaded) || "This file matches an existing document and was reused."
          : uploaded.actualDeductedVnd > 0
            ? `The document was processed successfully. ${uploaded.actualDeductedVnd.toLocaleString("vi-VN")} VND was deducted from your AI wallet.`
            : "The document was processed successfully."
      });
    } catch (uploadError) {
      setToast({
        type: "error",
        message: formatUploadError(uploadError, selectedCourse)
      });
    } finally {
      setIsUploading(false);
    }
  }

  async function handleDelete() {
    if (!documentPendingDelete?.id || deletingDocumentId) {
      return;
    }

    setDeletingDocumentId(documentPendingDelete.id);
    setToast(null);

    try {
      await deleteStudentDocument(documentPendingDelete.id);
      setDocuments((current) => current.filter((item) => item.id !== documentPendingDelete.id));
      setDocumentPendingDelete(null);
      setToast({
        type: "success",
        message: "Document deleted. File storage, chunks, and extraction data were removed."
      });
    } catch (error) {
      setToast({
        type: "error",
        message: error.message || "Unable to delete this document."
      });
    } finally {
      setDeletingDocumentId("");
    }
  }

  async function handleDownload(document) {
    if (!document?.id || downloadingDocumentId) {
      return;
    }

    setDownloadingDocumentId(document.id);
    setToast(null);

    try {
      await downloadStudentDocument(document);
    } catch (error) {
      setToast({
        type: "error",
        message: error.message || "Unable to download this document."
      });
    } finally {
      setDownloadingDocumentId("");
    }
  }

  const selectedCourse = useMemo(
    () => courses.find((course) => (course.id || course.code) === selectedCourseId) || null,
    [courses, selectedCourseId]
  );

  const readinessSummary = useMemo(() => {
    return documents.reduce(
      (summary, item) => {
        summary.total += 1;

        if (item.isReadyForGeneration) {
          summary.ready += 1;
        }

        if (item.ingestionStage === "processing") {
          summary.processing += 1;
        }

        return summary;
      },
      { total: 0, ready: 0, processing: 0 }
    );
  }, [documents]);

  const selectedFileSummary = useMemo(() => {
    if (!selectedFile) {
      return "No document selected.";
    }

    const sizeInMb = selectedFile.size ? `${(selectedFile.size / (1024 * 1024)).toFixed(2)} MB` : null;
    return [selectedFile.name, sizeInMb].filter(Boolean).join(" - ");
  }, [selectedFile]);

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentByosDocuments}
      showPageHeader={false}
    >
      <section className="ai-student-shell">
        <div className="byos-documents-heading">
          <h1>Upload Document</h1>
        </div>

        <section className="byos-documents-layout">
          <article className="ai-student-panel byos-documents-upload">
            <div className="ai-student-panel__header">
              <div>
                <p className="ai-student-kicker">Upload</p>
                <h2>Add a new document</h2>
              </div>
            </div>
            {isUploading ? (
              <InFlightNotice
                title="Uploading and processing..."
                description={`Upload request locked to course ${uploadScopeSnapshot.selectedCourseId || "-"}${uploadScopeSnapshot.fileName ? ` and file ${uploadScopeSnapshot.fileName}` : ""}.`}
              />
            ) : null}
            <div className="byos-documents-upload__group">
              <label className="student-filter-field">
                <span>Course</span>
                <select value={selectedCourseId} onChange={(event) => setSelectedCourseId(event.target.value)} disabled={isUploading || isLoading}>
                  <option value="">Select course</option>
                  {courses.map((course) => (
                    <option key={course.id || course.code} value={course.id || course.code}>
                      {course.code || course.id} - {course.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="student-filter-field">
                <span>Document file</span>
                <input
                  type="file"
                  accept=".pdf,.docx,.txt,.pptx"
                  disabled={isUploading}
                  onChange={(event) => setSelectedFile(event.target.files?.[0] || null)}
                />
              </label>
            </div>
            <div className="byos-documents-selection">
              <div>
                <span>Selected course</span>
                <strong>{selectedCourse ? getCourseLabel(selectedCourse) : "No course selected"}</strong>
              </div>
              <div>
                <span>Selected file</span>
                <strong>{selectedFileSummary}</strong>
              </div>
            </div>
            <div className="ai-student-callout">
              Supported formats: PDF, DOCX, PPTX, TXT. Maximum 2 MB per file and 10 personal documents in the
              workspace.
            </div>
            <div className="ai-inline-actions ai-inline-actions--footer">
              <Button variant="primary" onClick={handleUpload} disabled={isUploading || !selectedFile || !selectedCourseId}>
                {isUploading ? "Uploading and processing..." : "Upload Document"}
              </Button>
            </div>
          </article>

          <article className="ai-student-panel byos-documents-library">
            <div className="ai-student-panel__header">
              <div>
                <p className="ai-student-kicker">Library</p>
                <h2>My Documents files</h2>
              </div>
              <span className="byos-documents-library__caption">{readinessSummary.total} total documents</span>
            </div>
            <div className="byos-documents-library__list">
              {isLoading ? <div className="byos-documents-empty">Loading documents...</div> : null}

              {!isLoading && !documents.length ? (
                <div className="byos-documents-empty">
                  <strong>No My Documents files yet.</strong>
                  <p>Upload a file to start preview, extraction, and generation from your own material.</p>
                </div>
              ) : null}

              {!isLoading
                ? documents.map((document) => {
                    const primaryAction = getDocumentPrimaryAction(document);

                    return (
                      <article key={document.id} className="byos-document-card">
                        <div className="byos-document-card__main">
                          <div className="byos-document-card__title-row">
                            <button
                              type="button"
                              className="student-link-button"
                              onClick={() => handleDownload(document)}
                              disabled={downloadingDocumentId === document.id}
                            >
                              {downloadingDocumentId === document.id ? "Downloading..." : document.fileName}
                            </button>
                          </div>
                          <div className="byos-document-card__meta">
                            <span>{document.subjectCode || document.courseId || "No subject"}</span>
                            <span>{document.chunkCount || 0} chunks</span>
                            <span>{document.tagCount || 0} topics</span>
                          </div>
                          {document.duplicateDetected ? (
                            <p className="ai-row-subcopy">{getDuplicateMessage(document)}</p>
                          ) : null}
                        </div>
                        <div className="byos-document-card__actions ai-inline-actions">
                          <button
                            type="button"
                            className="student-link-button"
                            onClick={() => navigateTo(ROUTES.studentByosPreview(document.id))}
                          >
                            Preview
                          </button>
                          {primaryAction === "draft" ? (
                            <button
                              type="button"
                              className="student-link-button"
                              onClick={() => navigateTo(ROUTES.studentByosExtractionDraft(document.id))}
                            >
                              Draft
                            </button>
                          ) : null}
                          {primaryAction === "generate" ? (
                            <button
                              type="button"
                              className="student-link-button"
                              onClick={() => navigateTo(ROUTES.studentByosGenerate(document.id))}
                            >
                              Generate
                            </button>
                          ) : null}
                          <button
                            type="button"
                            className="student-link-button"
                            onClick={() => setDocumentPendingDelete(document)}
                            disabled={deletingDocumentId === document.id}
                          >
                            {deletingDocumentId === document.id ? "Deleting..." : "Delete"}
                          </button>
                        </div>
                      </article>
                    );
                  })
                : null}
            </div>
          </article>
        </section>
      </section>
      <ConfirmModal
        open={Boolean(documentPendingDelete)}
        title="Delete My Documents file"
        message={
          documentPendingDelete
            ? `Delete "${documentPendingDelete.fileName}"? This will remove the file from storage and delete all extracted chunks.`
            : ""
        }
        confirmLabel="Delete"
        isConfirming={Boolean(deletingDocumentId)}
        onCancel={() => !deletingDocumentId && setDocumentPendingDelete(null)}
        onConfirm={handleDelete}
      />
      <ToastNotification toast={toast} />
    </StudentScaffold>
  );
}
