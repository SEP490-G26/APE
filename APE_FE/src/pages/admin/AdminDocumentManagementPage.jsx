import { useEffect, useMemo, useState } from "react";
import { AdminGlyph } from "../../components/admin/AdminGlyph";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { Button } from "../../components/common";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getCourses } from "../../services/courseService";
import { deleteAdminDocument, downloadAdminDocument, getAdminDocumentPreview, getAdminDocuments, uploadAdminDocument } from "../../services/adminDocumentService";
import { ConfirmModal, ToastNotification } from "../../shared/ui";

const ACCEPTED_EXTENSIONS = ["txt", "doc", "docx", "pptx", "pdf", "md"];
const MAX_FILE_SIZE = 20 * 1024 * 1024;
const STATUS_LABELS = {
  uploading: "Uploading",
  ingesting: "Ingesting",
  ready: "Ready",
  generating: "Generating",
  completed: "Completed",
  failed: "Failed"
};

function slugify(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function createLogEntry(stage, message) {
  return {
    id: `${stage}-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`,
    stage,
    message,
    time: new Date().toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })
  };
}

function CloseCircleIcon() {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" width={24} height={24} fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M14.83 7.76 12 10.59 9.17 7.76 7.76 9.17 10.59 12l-2.83 2.83 1.41 1.41L12 13.41l2.83 2.83 1.41-1.41L13.41 12l2.83-2.83z" />
      <path d="M12 2C9.33 2 6.82 3.04 4.93 4.93S2 9.33 2 12s1.04 5.18 2.93 7.07c1.95 1.95 4.51 2.92 7.07 2.92s5.12-.97 7.07-2.92S22 14.67 22 12s-1.04-5.18-2.93-7.07A9.93 9.93 0 0 0 12 2m5.66 15.66c-3.12 3.12-8.19 3.12-11.31 0-1.51-1.51-2.34-3.52-2.34-5.66s.83-4.15 2.34-5.66S9.87 4 12.01 4s4.15.83 5.66 2.34 2.34 3.52 2.34 5.66-.83 4.15-2.34 5.66Z" />
    </svg>
  );
}

function formatFileSize(bytes) {
  if (!bytes) {
    return "0 MB";
  }

  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

function getCourseLabel(document) {
  return [document.courseCode, document.courseName].filter(Boolean).join(" - ");
}

function hydrateDocumentCourse(document, courses) {
  const matchedCourse = courses.find((course) => course.id === document.courseId);
  return {
    ...document,
    courseCode: document.courseCode || matchedCourse?.code || "",
    courseName: document.courseName || matchedCourse?.name || ""
  };
}

function createDocumentFromApi(document, course, file) {
  const extension = document.fileExtension || file?.name?.split(".").pop()?.toLowerCase() || "";
  const warning =
    extension === "pdf" && String(document.fileName || file?.name || "").toLowerCase().includes("scan")
      ? "Potential scanned-only PDF detected. Recommend replacing with the original text-layer source."
      : "";

  return {
    ...document,
    name: document.name || document.fileName || "Untitled document",
    description: "",
    courseId: document.courseId || course.id,
    courseCode: course.code,
    courseName: course.name,
    fileName: document.fileName || file?.name || "",
    fileExtension: extension,
    fileSize: document.fileSize || file?.size || 0,
    status: document.status || "uploading",
    progress: document.status === "completed" ? 100 : document.status === "failed" ? 100 : 12,
    warning,
    chunkCount: document.chunkCount || 0,
    topicCount: document.topicCount || 0,
    source: document.source || "System",
    extractionPreview:
      document.extractionPreview || "Upload staged. The extraction draft will appear here once the ingest worker starts chunking the document.",
    failureReason: document.failureReason || "",
    logs: [createLogEntry("uploading", "Upload accepted. Waiting for background ingest worker.")],
    originalUploadAt: new Date().toISOString()
  };
}

function advanceDocument(document) {
  if (document.status === "uploading") {
    return {
      ...document,
      status: "ingesting",
      progress: 58,
      chunkCount: Math.max(document.chunkCount || 0, 12),
      topicCount: Math.max(document.topicCount || 0, 3),
      extractionPreview:
        document.extractionPreview === "Upload staged. The extraction draft will appear here once the ingest worker starts chunking the document."
          ? `Gatekeeper accepted ${document.name}. Extraction draft is now building chunks and retrieval tags.`
          : document.extractionPreview,
      logs: [...document.logs, createLogEntry("ingesting", "Gatekeeper accepted the document. Extraction and chunking started.")]
    };
  }

  if (document.status === "ingesting") {
    return {
      ...document,
      status: "ready",
      progress: 100,
      chunkCount: Math.max(document.chunkCount || 0, 26),
      topicCount: Math.max(document.topicCount || 0, 6),
      logs: [...document.logs, createLogEntry("ready", "Ingest complete. Document is ready for retrieval and generation.")]
    };
  }

  if (document.status === "generating") {
    return {
      ...document,
      status: "completed",
      progress: 100,
      logs: [...document.logs, createLogEntry("completed", "Generation completed. Questions remain even if this document is later deleted.")]
    };
  }

  return document;
}

function getDocumentPreviewModel(document) {
  if (!document) {
    return null;
  }

  const baseTags =
    document.courseCode === "DBI202"
      ? ["transactions", "recovery", "logging"]
      : document.courseCode === "OSG202"
        ? ["scheduler", "processes", "threads"]
        : document.courseCode === "PRO192"
          ? ["inheritance", "classes", "objects"]
          : document.courseCode === "PRN211"
            ? ["frontend", "routing", "services"]
            : ["documents", "chunks", "topics"];

  return {
    documentId: document.id,
    fileName: document.fileName,
    fileType: document.fileExtension?.toUpperCase() || "-",
    source: document.source || "System",
    subjectCode: document.courseCode || "-",
    gatekeeperVerdict: document.gatekeeperVerdict || "-",
    ingestionStatus: STATUS_LABELS[document.status] || document.status || "-",
    ingestionStage: document.status,
    chunkCount: document.chunkCount || 0,
    tags: baseTags.slice(0, Math.max(1, Math.min(document.topicCount || baseTags.length, baseTags.length))).map((tag) => ({ tag })),
    chapters: [
      {
        chapterKey: `${document.id}-chapter-1`,
        chapterTitle: document.name.replace(/\.(pdf|docx|doc|pptx|txt|md)$/i, "").split(" ").slice(0, 3).join(" "),
        overviewShort: document.description || "No overview available.",
        chunkCount: Math.max(1, Math.min(document.chunkCount || 1, 8)),
        coveredTopics: baseTags.slice(0, Math.max(1, Math.min(2, baseTags.length)))
      }
    ],
    isReadyForGeneration: document.status === "ready" || document.status === "completed",
    previewText: document.extractionPreview || "No preview text available.",
    cleanupWarnings: document.warning ? [document.warning] : []
  };
}

export function AdminDocumentManagementPage() {
  const [isUploadOpen, setIsUploadOpen] = useState(false);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [documents, setDocuments] = useState([]);
  const [courses, setCourses] = useState([]);
  const [selectedPreview, setSelectedPreview] = useState(null);
  const [selectedDocumentId, setSelectedDocumentId] = useState("sys-doc-001");
  const [filters, setFilters] = useState({ search: "", courseId: "all" });
  const [form, setForm] = useState({
    courseId: "",
    file: null
  });
  const [toast, setToast] = useState(null);
  const [isUploading, setIsUploading] = useState(false);
  const [downloadingId, setDownloadingId] = useState("");
  const [deletingId, setDeletingId] = useState("");
  const [documentPendingDelete, setDocumentPendingDelete] = useState(null);

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  useEffect(() => {
    let ignore = false;

    async function loadInitialData() {
      try {
        const coursePayload = await getCourses({ page: 1, limit: 100 });
        const items = Array.isArray(coursePayload?.items) ? coursePayload.items : [];
        if (ignore) {
          return;
        }

        setCourses(items);
        const documentPayload = await getAdminDocuments({ page: 1, limit: 100 });
        if (ignore) {
          return;
        }

        const nextDocuments = Array.isArray(documentPayload?.items)
          ? documentPayload.items.map((document) => hydrateDocumentCourse(document, items))
          : [];
        setDocuments(nextDocuments);
        setForm((current) => ({
          ...current,
          courseId: current.courseId || items[0]?.id || ""
        }));
      } catch (error) {
        if (!ignore) {
          setDocuments([]);
          setToast({ type: "error", message: error.message || "Could not load admin document data." });
        }
      }
    }

    loadInitialData();
    return () => {
      ignore = true;
    };
  }, []);

  const filteredDocuments = useMemo(() => {
    const keyword = filters.search.trim().toLowerCase();

    return documents.filter((document) => {
      const matchesSearch =
        !keyword ||
        String(document.name || document.fileName || "").toLowerCase().includes(keyword) ||
        document.fileName.toLowerCase().includes(keyword) ||
        document.courseCode.toLowerCase().includes(keyword);
      const matchesCourse = filters.courseId === "all" || document.courseId === filters.courseId;

      return matchesSearch && matchesCourse;
    });
  }, [documents, filters]);

  const selectedDocument = useMemo(() => {
    return documents.find((document) => document.id === selectedDocumentId) || filteredDocuments[0] || documents[0] || null;
  }, [documents, filteredDocuments, selectedDocumentId]);

  const stats = useMemo(() => {
    return documents.reduce(
      (summary, document) => {
        summary.total += 1;

        if (document.status === "ready" || document.status === "completed") {
          summary.ready += 1;
        }

        return summary;
      },
      { total: 0, ready: 0 }
    );
  }, [documents]);

  const selectedCourse = useMemo(
    () => courses.find((course) => course.id === form.courseId) || null,
    [courses, form.courseId]
  );

  useEffect(() => {
    if (selectedDocument && selectedDocument.id !== selectedDocumentId) {
      setSelectedDocumentId(selectedDocument.id);
    }
  }, [selectedDocument, selectedDocumentId]);

  useEffect(() => {
    let ignore = false;

    async function loadPreview() {
      if (!isDetailOpen || !selectedDocumentId) {
        return;
      }

      try {
        const preview = await getAdminDocumentPreview(selectedDocumentId);
        if (!ignore) {
          setSelectedPreview(preview);
        }
      } catch (error) {
        if (!ignore) {
          setSelectedPreview(selectedDocument ? getDocumentPreviewModel(selectedDocument) : null);
          setToast({ type: "error", message: error.message || "Could not load document preview." });
        }
      }
    }

    loadPreview();
    return () => {
      ignore = true;
    };
  }, [isDetailOpen, selectedDocument, selectedDocumentId]);

  function updateForm(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function resetForm() {
    setForm({
      courseId: courses[0]?.id || "",
      file: null
    });
  }

  async function handleUpload() {
    const nextCourse = courses.find((course) => course.id === form.courseId);
    const file = form.file;

    if (!nextCourse || !file) {
      setToast({ type: "error", message: "Course and file are required before uploading." });
      return;
    }

    const extension = file.name.split(".").pop()?.toLowerCase() || "";

    if (!ACCEPTED_EXTENSIONS.includes(extension)) {
      setToast({ type: "error", message: "Accepted formats are txt, doc, docx, pptx, pdf, and md only." });
      return;
    }

    if (file.size > MAX_FILE_SIZE) {
      setToast({ type: "error", message: "File size must not exceed 2 MB." });
      return;
    }

    setIsUploading(true);
    try {
      const uploaded = await uploadAdminDocument({
        file,
        courseId: nextCourse.id
      });
      const created = createDocumentFromApi(uploaded, nextCourse, file);

      setDocuments((current) => [created, ...current.filter((item) => item.id !== created.id)]);
      setSelectedDocumentId(created.id);
      setIsUploadOpen(false);
      setToast({
        type: "success",
        message: uploaded.duplicateDetected
          ? `Document da ton tai, he thong da tai su dung "${created.name}".`
          : "System document uploaded. Live ingest tracking has started."
      });
      resetForm();
    } catch (error) {
      setToast({ type: "error", message: error.message || "Could not upload the system document." });
    } finally {
      setIsUploading(false);
    }
  }

  async function handleDelete() {
    if (!documentPendingDelete || deletingId) {
      return;
    }

    setDeletingId(documentPendingDelete.id);
    try {
      await deleteAdminDocument(documentPendingDelete.id);
      setDocuments((current) => current.filter((item) => item.id !== documentPendingDelete.id));
      setSelectedPreview(null);
      setSelectedDocumentId((current) => {
        if (current !== documentPendingDelete.id) {
          return current;
        }

        const next = filteredDocuments.find((item) => item.id !== documentPendingDelete.id);
        return next?.id || "";
      });
      setToast({
        type: "success",
        message: `Document deleted. Storage file, chunks, and extraction draft were removed.`
      });
      if (selectedDocumentId === documentPendingDelete.id) {
        setIsDetailOpen(false);
      }
      setDocumentPendingDelete(null);
    } catch (error) {
      setToast({ type: "error", message: error.message || "Could not delete the document." });
    } finally {
      setDeletingId("");
    }
  }

  async function handleDownload(selectedItem) {
    if (!selectedItem) {
      return;
    }

    setDownloadingId(selectedItem.id);
    try {
      await downloadAdminDocument(selectedItem);
      setToast({ type: "success", message: `Downloaded "${selectedItem.fileName}".` });
    } catch (error) {
      setToast({ type: "error", message: error.message || "Could not download the document." });
    } finally {
      setDownloadingId("");
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.documentManagement}
      heroIcon="doc"
      heroTitle="Management"
      heroSubtitle="System documents"
      title="Document Management"
      subtitle="Upload and monitor system-scoped documents with live ingest status, retry controls, and extraction debug visibility."
    >
      <section className="admin-document-stat-grid">
        <article className="admin-summary-card admin-document-stat-card">
          <p>Total documents</p>
          <strong>{stats.total}</strong>
          <span>System-scoped library items</span>
        </article>
        <article className="admin-summary-card admin-document-stat-card">
          <p>Ready or completed</p>
          <strong>{stats.ready}</strong>
          <span>Available to retrieval flows now</span>
        </article>
      </section>

      <article className="admin-table-panel admin-document-library-panel admin-document-library-panel--solo">
        <div className="admin-document-panel__header">
          <div>
            <p className="admin-document-kicker">Library</p>
            <h2>System document list</h2>
          </div>
          <div className="admin-document-upload-panel__actions">
            <span className="admin-document-note">{filteredDocuments.length} visible documents</span>
            <Button variant="primary" onClick={() => setIsUploadOpen(true)}>
              Upload System Document
            </Button>
          </div>
        </div>

        <section className="admin-filter-panel admin-filter-panel--stack admin-document-filter-panel">
          <label className="admin-search-box">
            <span className="admin-search-box__icon">
              <AdminGlyph kind="search" />
            </span>
            <input
              type="text"
              value={filters.search}
              onChange={(event) => setFilters((current) => ({ ...current, search: event.target.value }))}
              placeholder="Search by name, file, or course code..."
            />
          </label>
          <div className="admin-filter-group">
            <label className="admin-select-field">
              <span>Course</span>
              <select value={filters.courseId} onChange={(event) => setFilters((current) => ({ ...current, courseId: event.target.value }))}>
                <option value="all">All</option>
                {courses.map((course) => (
                  <option key={course.id} value={course.id}>
                    {course.code}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </section>

        <div className="admin-table admin-table--documents admin-table--native-layout">
          {!filteredDocuments.length ? (
            <div className="admin-empty-state">
              <strong>No documents match the current filters.</strong>
            </div>
          ) : null}

          {filteredDocuments.length ? (
            <table className="admin-table__native admin-table__native--documents">
              <colgroup>
                <col className="admin-table__col admin-table__col--document-name" />
                <col className="admin-table__col admin-table__col--document-course" />
                <col className="admin-table__col admin-table__col--document-size" />
                <col className="admin-table__col admin-table__col--document-chunks" />
                <col className="admin-table__col admin-table__col--document-status" />
                <col className="admin-table__col admin-table__col--document-actions" />
              </colgroup>
              <thead>
                <tr className="admin-table__head admin-table__head--documents">
                  <th scope="col">Document</th>
                  <th scope="col">Course</th>
                  <th scope="col">Size</th>
                  <th scope="col">Chunks</th>
                  <th scope="col">Status</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody className="admin-table__body">
                {filteredDocuments.map((document) => (
                  <tr
                    key={document.id}
                    className={`admin-course-row admin-course-row--documents${selectedDocument?.id === document.id ? " is-selected" : ""}`}
                    onClick={() => setSelectedDocumentId(document.id)}
                  >
                    <td>
                      <div className="admin-document-cell">
                        <strong>{document.name || document.fileName}</strong>
                        <p>{document.fileName}</p>
                        {document.warning ? <small className="admin-document-warning">{document.warning}</small> : null}
                      </div>
                    </td>
                    <td>
                      <span className="admin-document-cell__muted">{getCourseLabel(document)}</span>
                    </td>
                    <td>
                      <span className="admin-document-cell__muted">{formatFileSize(document.fileSize)}</span>
                    </td>
                    <td>
                      <span className="admin-document-cell__muted">{document.chunkCount}</span>
                    </td>
                    <td>
                      <span className={`admin-document-status admin-document-status--${document.status}`}>
                        {STATUS_LABELS[document.status] || document.status}
                      </span>
                    </td>
                    <td>
                      <div className="admin-document-table-actions">
                        <button
                          type="button"
                          className="student-link-button"
                          onClick={(event) => {
                            event.stopPropagation();
                            setSelectedDocumentId(document.id);
                            setIsDetailOpen(true);
                          }}
                        >
                          Preview
                        </button>
                        <button
                          type="button"
                          className="student-link-button"
                          onClick={(event) => {
                            event.stopPropagation();
                            navigateTo(ROUTES.adminDocumentGenerate(document.id));
                          }}
                          disabled={!getDocumentPreviewModel(document)?.isReadyForGeneration}
                        >
                          Generate
                        </button>
                        <button
                          type="button"
                          className="student-link-button"
                          onClick={(event) => {
                            event.stopPropagation();
                            setDocumentPendingDelete(document);
                          }}
                          disabled={deletingId === document.id}
                        >
                          {deletingId === document.id ? "Deleting..." : "Delete"}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>
      </article>

      {isUploadOpen ? (
        <div className="admin-document-modal-backdrop" role="presentation" onClick={() => setIsUploadOpen(false)}>
          <article className="admin-table-panel admin-document-upload-panel admin-document-panel--single admin-document-modal" onClick={(event) => event.stopPropagation()}>
          <button type="button" className="admin-document-icon-button admin-document-icon-button--floating" aria-label="Close upload dialog" onClick={() => setIsUploadOpen(false)}>
            <CloseCircleIcon />
          </button>
          <div className="admin-document-panel__header">
            <div>
              <p className="admin-document-kicker">Upload form</p>
              <h2>New system document</h2>
            </div>
            <div className="admin-document-upload-panel__actions" />
          </div>
          <div className="admin-document-form-grid">
            <label className="admin-document-field">
              <span>Course</span>
              <select value={form.courseId} onChange={(event) => updateForm("courseId", event.target.value)}>
                {courses.map((course) => (
                  <option key={course.id} value={course.id}>
                    {course.code} - {course.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="admin-document-field admin-document-field--full">
              <span>File</span>
              <input type="file" accept=".txt,.doc,.docx,.pptx,.pdf,.md" onChange={(event) => updateForm("file", event.target.files?.[0] || null)} />
              <small className="admin-document-field__hint">Accepted: txt, doc, docx, pptx, pdf, md up to 2 MB</small>
            </label>
          </div>
          <div className="admin-document-callout">
            <strong>{selectedCourse ? `${selectedCourse.code} - ${selectedCourse.name}` : "No course selected"}</strong>
            <p>
              Gatekeeper validates course-domain match before persisting the document. Scanned-only PDFs should be
              replaced with the original text-layer source when possible.
            </p>
            <small>{form.file ? `${form.file.name} - ${formatFileSize(form.file.size)}` : "No file selected yet."}</small>
          </div>
          <div className="admin-document-modal__footer">
            <Button variant="primary" onClick={handleUpload} disabled={isUploading}>
              {isUploading ? "Uploading..." : "Upload System Document"}
            </Button>
          </div>
          </article>
        </div>
      ) : null}

      {isDetailOpen ? (
        <div className="admin-document-modal-backdrop" role="presentation" onClick={() => setIsDetailOpen(false)}>
          <article className="admin-detail-panel admin-document-detail-panel admin-document-panel--single admin-document-modal" onClick={(event) => event.stopPropagation()}>
          <button type="button" className="admin-document-icon-button admin-document-icon-button--floating" aria-label="Close detail dialog" onClick={() => setIsDetailOpen(false)}>
            <CloseCircleIcon />
          </button>
          <div className="admin-detail-panel__header">
            <div>
              <p className="admin-document-kicker">Document detail</p>
              <h2>{selectedDocument?.name || "Select a document"}</h2>
            </div>
            <div className="admin-document-upload-panel__actions">
              {selectedDocument ? (
                <span className={`admin-document-status admin-document-status--${selectedDocument.status}`}>
                  {STATUS_LABELS[selectedDocument.status] || selectedDocument.status}
                </span>
              ) : null}
            </div>
          </div>

          {!selectedDocument ? (
            <div className="admin-empty-state">Select a document to inspect its ingest lifecycle.</div>
          ) : (
            <div className="admin-detail-panel__body admin-document-detail-body">
              <div className="admin-document-detail-grid">
                <div><span>Document ID</span><strong>{selectedPreview?.documentId || "-"}</strong></div>
                <div><span>File Name</span><strong>{selectedPreview?.fileName || "-"}</strong></div>
                <div><span>File Type</span><strong>{selectedPreview?.fileType || "-"}</strong></div>
                <div><span>Source</span><strong>{selectedPreview?.source || "-"}</strong></div>
                <div><span>Subject Code</span><strong>{selectedPreview?.subjectCode || "-"}</strong></div>
                <div><span>Gatekeeper Verdict</span><strong>{selectedPreview?.gatekeeperVerdict || "-"}</strong></div>
                <div><span>Ingestion Status</span><strong>{selectedPreview?.ingestionStatus || "-"}</strong></div>
                <div><span>Chunk Count</span><strong>{selectedPreview?.chunkCount || 0}</strong></div>
                <div><span>Topic Tag Count</span><strong>{selectedPreview?.tags?.length || 0}</strong></div>
                <div><span>Ready for Generation</span><strong>{String(Boolean(selectedPreview?.isReadyForGeneration))}</strong></div>
              </div>

              <section className="admin-document-debug-card">
                <div className="admin-document-debug-card__header">
                  <div>
                    <p className="admin-document-kicker">Preview Text</p>
                    <h3>Readable excerpt panel</h3>
                  </div>
                </div>
                <div className="admin-preview-block">{selectedPreview?.previewText || "No preview text available."}</div>
                {selectedPreview?.cleanupWarnings?.length ? (
                  <div className="admin-document-callout">
                    <strong>Cleanup Warnings</strong>
                    <ul className="ai-bullet-list">
                      {selectedPreview.cleanupWarnings.map((warning) => (
                        <li key={warning}>{warning}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}
                {selectedPreview?.tags?.length ? (
                  <>
                    <div className="ai-student-panel__header">
                      <div>
                        <p className="ai-student-kicker">Topic Summary</p>
                        <h2>Detected retrieval tags</h2>
                      </div>
                    </div>
                    <div className="ai-topic-list">
                      {selectedPreview.tags.map((topic) => (
                        <span key={topic.tag} className="ai-chip">{topic.tag}</span>
                      ))}
                    </div>
                  </>
                ) : null}
                {selectedPreview?.chapters?.length ? (
                  <>
                    <div className="ai-student-panel__header">
                      <div>
                        <p className="ai-student-kicker">Chapter Scope</p>
                        <h2>Current chapter candidates</h2>
                      </div>
                    </div>
                    <div className="ai-chapter-list">
                      {selectedPreview.chapters.map((chapter) => (
                        <article key={chapter.chapterKey} className="ai-chapter-card">
                          <strong>{chapter.chapterTitle}</strong>
                          <p>{chapter.overviewShort || "No overview available."}</p>
                          <small>{chapter.chunkCount || 0} chunks - {chapter.coveredTopics?.length || 0} topics</small>
                        </article>
                      ))}
                    </div>
                  </>
                ) : null}
                <div className="ai-inline-actions ai-inline-actions--footer">
                  <Button
                    variant="primary"
                    disabled={!selectedPreview?.isReadyForGeneration}
                    onClick={() => navigateTo(ROUTES.adminDocumentGenerate(selectedDocument.id))}
                  >
                    Generate From This Document
                  </Button>
                  <Button
                    variant="secondary"
                    disabled={!selectedPreview?.hasExtractionDraft}
                    onClick={() => navigateTo(ROUTES.adminDocumentExtractionDraft(selectedDocument.id))}
                  >
                    Open Extraction Draft
                  </Button>
                </div>
              </section>

              <div className="ai-inline-actions">
                <Button variant="secondary" onClick={() => handleDownload(selectedDocument)} disabled={downloadingId === selectedDocument.id}>
                  {downloadingId === selectedDocument.id ? "Downloading..." : "Download Original"}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => setDocumentPendingDelete(selectedDocument)}
                  disabled={deletingId === selectedDocument.id}
                >
                  {deletingId === selectedDocument.id ? "Deleting..." : "Delete Document"}
                </Button>
              </div>
            </div>
          )}
          </article>
        </div>
      ) : null}
      <ConfirmModal
        open={Boolean(documentPendingDelete)}
        title="Delete system document"
        message={
          documentPendingDelete
            ? `Soft-delete "${documentPendingDelete.name}"? Its chunks will be removed from retrieval now, while generated questions remain and hard-delete happens after 30 days.`
            : ""
        }
        confirmLabel="Delete"
        isConfirming={Boolean(deletingId)}
        onCancel={() => !deletingId && setDocumentPendingDelete(null)}
        onConfirm={handleDelete}
      />
      <ToastNotification toast={toast} />
    </AdminScaffold>
  );
}
