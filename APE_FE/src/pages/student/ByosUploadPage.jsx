import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getAuthSession } from "../../lib/storage";
import { getStudentInsightCourses } from "../../services/studentInsightService";
import { getByosDocuments, uploadByosDocuments } from "../../services/studentWorkspaceService";

function getProfile() {
  const user = getAuthSession()?.user;
  return {
    aiWalletBalanceVnd: Number(user?.aiWalletBalanceVnd ?? 0),
    byokActive: Boolean(user?.byokActive ?? user?.hasByok ?? false)
  };
}

function formatVnd(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} VND`;
}

const MIN_BALANCE_VND = 1000;

export function ByosUploadPage() {
  const [courses, setCourses] = useState([]);
  const [courseId, setCourseId] = useState("personal");
  const [documents, setDocuments] = useState([]);
  const [files, setFiles] = useState([]);
  const profile = useMemo(() => getProfile(), []);
  const hasEnoughBalance = profile.byokActive || profile.aiWalletBalanceVnd >= MIN_BALANCE_VND;

  useEffect(() => {
    getStudentInsightCourses().then(setCourses);
    getByosDocuments().then(setDocuments);
  }, []);

  async function handleUpload() {
    if (!files.length) {
      return;
    }
    const next = await uploadByosDocuments(files, { courseId });
    setDocuments(next);
    setFiles([]);
  }

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentByosUpload}
      title="BYOS Upload"
      subtitle="Upload personal study materials, inspect upload quota, and send extracted content to preview."
      actions={<Button variant="ghost" onClick={() => navigateTo(ROUTES.studentDataPreview)}>Open Preview</Button>}
    >
      {!hasEnoughBalance ? <div className="student-status-banner">Your AI wallet balance is too low. You need at least {formatVnd(MIN_BALANCE_VND)} to process BYOS documents.</div> : null}
      <section className="student-page-grid">
        <article className="student-panel">
          <div className="student-panel__header"><div><h2>Upload Materials</h2><p>{documents.length} / 10 personal documents used</p></div></div>
          <label className="student-filter-field">
            <span>Course</span>
            <select value={courseId} onChange={(event) => setCourseId(event.target.value)}>
              <option value="personal">Personal Material</option>
              {courses.map((course) => <option key={course.id} value={course.code}>{course.code}</option>)}
            </select>
          </label>
          <input type="file" multiple accept=".pdf,.docx,.txt" onChange={(event) => setFiles(Array.from(event.target.files || []))} />
          <div className="student-status-banner">Supported formats: PDF, DOCX, TXT. Max 2MB per file.</div>
          <div className="student-status-banner">AI Wallet: {formatVnd(profile.aiWalletBalanceVnd)}</div>
          <Button variant="primary" onClick={handleUpload} disabled={!files.length || (!hasEnoughBalance && !profile.byokActive)}>
            Upload Documents
          </Button>
        </article>

        <article className="student-panel student-table-panel">
          <div className="student-table">
            <div className="student-table__head">
              <span>File</span>
              <span>Course</span>
              <span>Size</span>
              <span>Status</span>
              <span>Action</span>
            </div>
            <div className="student-table__body">
              {documents.map((doc) => (
                <article key={doc.id} className="student-table__row student-table__row--compact">
                  <span>{doc.fileName}</span>
                  <span>{doc.course}</span>
                  <span>{doc.size}</span>
                  <span>{doc.status}</span>
                  <button type="button" className="student-link-button" onClick={() => navigateTo(ROUTES.studentDataPreview)}>Preview</button>
                </article>
              ))}
            </div>
          </div>
        </article>
      </section>
    </StudentScaffold>
  );
}

