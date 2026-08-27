import { useEffect, useState } from "react";
import { Button } from "../../components/common";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES } from "../../lib/routes";
import { getAuthSession } from "../../lib/storage";
import { getAdminPipelineDocuments, getByosDocuments, updatePipelineDocumentStatus } from "../../services/studentWorkspaceService";

function StudentPreview({ documents }) {
  const [selectedId, setSelectedId] = useState(documents[0]?.id || "");
  const selected = documents.find((item) => item.id === selectedId) || documents[0];

  return (
    <StudentScaffold activeRoute={ROUTES.studentDataPreview} title="Data Approval & Preview" subtitle="Preview extracted content from your personal uploads before using it for generation.">
      <section className="student-page-grid">
        <article className="student-panel student-table-panel">
          <div className="student-table">
            <div className="student-table__head">
              <span>File</span><span>Status</span><span>Gatekeeper</span><span>Tags</span><span>Type</span>
            </div>
            <div className="student-table__body">
              {documents.map((item) => (
                <button key={item.id} type="button" className={`student-table__row student-table__row--compact${selectedId === item.id ? " is-highlighted" : ""}`} onClick={() => setSelectedId(item.id)}>
                  <span>{item.fileName}</span><span>{item.status}</span><span>{item.gatekeeper}</span><span>{item.tags.join(", ")}</span><span>BYOS</span>
                </button>
              ))}
            </div>
          </div>
        </article>
        <article className="student-panel">
          <div className="student-panel__header"><div><h2>{selected?.fileName}</h2><p>{selected?.course}</p></div></div>
          <div className="student-tag-grid">{(selected?.tags || []).map((tag) => <span key={tag} className="student-tag-chip">#{tag}</span>)}</div>
          <p className="student-advice-copy">{selected?.extractedText}</p>
          <div className="student-weak-list">
            {(selected?.chunks || []).map((chunk) => <div key={chunk.order} className="student-weak-list__item"><strong>Chunk {chunk.order}</strong><span>{chunk.fullText}</span><em>{chunk.tags.join(", ")}</em></div>)}
          </div>
        </article>
      </section>
    </StudentScaffold>
  );
}

function AdminPreview() {
  const [documents, setDocuments] = useState([]);
  const [selectedId, setSelectedId] = useState("");
  const [draftText, setDraftText] = useState("");

  useEffect(() => {
    getAdminPipelineDocuments().then((items) => {
      setDocuments(items);
      setSelectedId(items[0]?.id || "");
      setDraftText(items[0]?.extractedText || "");
    });
  }, []);

  const selected = documents.find((item) => item.id === selectedId) || null;

  function handleSelect(item) {
    setSelectedId(item.id);
    setDraftText(item.extractedText);
  }

  async function handleStatus(status) {
    if (!selected) {
      return;
    }
    const updated = await updatePipelineDocumentStatus(selected.id, status, draftText);
    setDocuments((current) => current.map((item) => item.id === updated.id ? updated : item));
  }

  return (
    <AdminScaffold activeRoute={ROUTES.adminDataPreview} heroIcon="edit" heroTitle="Doc Preview" heroSubtitle="Data approval workflow" title="Data Approval & Preview" subtitle="Review OCR output, adjust extracted text, and approve or reject document chunks.">
      <section className="admin-split-layout">
        <section className="admin-table-panel">
          <div className="admin-table admin-table--preview admin-table--native-layout">
            <table className="admin-table__native">
              <colgroup>
                <col className="admin-table__col admin-table__col--file" />
                <col className="admin-table__col admin-table__col--course" />
                <col className="admin-table__col admin-table__col--status" />
                <col className="admin-table__col admin-table__col--gatekeeper" />
                <col className="admin-table__col admin-table__col--chunks" />
              </colgroup>
              <thead>
                <tr className="admin-table__head">
                  <th scope="col">File</th>
                  <th scope="col">Course</th>
                  <th scope="col">Status</th>
                  <th scope="col">Gatekeeper</th>
                  <th scope="col">Chunks</th>
                </tr>
              </thead>
              <tbody>
              {documents.map((item) => (
                <tr
                  key={item.id}
                  className={`admin-course-row admin-course-row--questions${selectedId === item.id ? " is-selected" : ""}`}
                  tabIndex={0}
                  onClick={() => handleSelect(item)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") {
                      event.preventDefault();
                      handleSelect(item);
                    }
                  }}
                >
                  <td><strong>{item.fileName}</strong></td>
                  <td>{item.course}</td>
                  <td>{item.status}</td>
                  <td>{item.gatekeeper}</td>
                  <td>{item.chunks.length}</td>
                </tr>
              ))}
              </tbody>
            </table>
          </div>
        </section>
        <aside className="admin-detail-panel">
          {selected ? (
            <div className="admin-detail-panel__body">
              <div className="admin-detail-panel__header"><h2>{selected.fileName}</h2><p>{selected.uploadedBy} • {new Date(selected.uploadDate).toLocaleString("vi-VN")}</p></div>
              <div className="student-tag-grid">{selected.tags.map((tag) => <span key={tag} className="student-tag-chip">#{tag}</span>)}</div>
              <textarea className="admin-prompt-editor" value={draftText} onChange={(event) => setDraftText(event.target.value)} />
              <div className="student-weak-list">
                {selected.chunks.map((chunk) => <div key={chunk.order} className="student-weak-list__item"><strong>Chunk {chunk.order}</strong><span>{chunk.fullText}</span><em>{chunk.tags.join(", ")}</em></div>)}
              </div>
              <div className="admin-modal__actions">
                <Button variant="secondary" onClick={() => handleStatus("Rejected")}>Reject</Button>
                <Button variant="primary" onClick={() => handleStatus("Approved")}>Approve</Button>
              </div>
            </div>
          ) : null}
        </aside>
      </section>
    </AdminScaffold>
  );
}

export function DataApprovalPreviewPage() {
  const role = getAuthSession()?.user?.role?.toLowerCase();
  const [studentDocs, setStudentDocs] = useState([]);

  useEffect(() => {
    if (role !== "admin") {
      getByosDocuments().then(setStudentDocs);
    }
  }, [role]);

  if (role === "admin") {
    return <AdminPreview />;
  }

  return <StudentPreview documents={studentDocs} />;
}
