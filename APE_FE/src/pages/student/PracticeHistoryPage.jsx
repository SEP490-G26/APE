import { useEffect, useMemo, useState } from "react";
import { PracticeSidebar } from "../../components/student/PracticeSidebar";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getStudentInsightCourses, getStudentPracticeHistory } from "../../services/studentInsightService";
import { createFeExamSession, createPePracticeSession, openFeHistorySession, openPeHistoryResult, resumeFeExamSession, resumePePracticeSession } from "../../services/studentWorkspaceService";

function isResumable(item) {
  const status = String(item?.status || "").trim().toLowerCase();
  return status === "inprogress" || status === "in progress";
}

function formatDate(dateText) {
  return new Date(dateText).toLocaleString("vi-VN");
}

export function PracticeHistoryPage() {
  const [courses, setCourses] = useState([]);
  const [history, setHistory] = useState([]);
  const [pagination, setPagination] = useState({ page: 1, limit: 10, total: 0, totalPages: 1 });
  const [filters, setFilters] = useState({ courseId: "all", type: "All" });
  const [errorMessage, setErrorMessage] = useState("");
  const [pendingActionKey, setPendingActionKey] = useState("");

  useEffect(() => {
    getStudentInsightCourses()
      .then(setCourses)
      .catch((error) => setErrorMessage(error.message || "Unable to load the course list."));
  }, []);

  useEffect(() => {
    getStudentPracticeHistory({ ...filters, page: pagination.page, limit: pagination.limit })
      .then((result) => {
        setHistory(result.items || []);
        setPagination((current) => ({
          ...current,
          total: result.total ?? 0,
          totalPages: result.totalPages ?? 1
        }));
      })
      .catch((error) => setErrorMessage(error.message || "Unable to load practice history."));
  }, [filters, pagination.page, pagination.limit]);

  const rows = useMemo(
    () =>
      history.map((item) => ({
        ...item,
        courseName: item.courseName || courses.find((course) => course.id === item.courseId)?.name || item.courseId
      })),
    [courses, history]
  );

  async function handleOpenHistoryItem(item) {
    setErrorMessage("");
    setPendingActionKey(`view-${item.id}`);

    try {
      if (item.type === "FE") {
        await openFeHistorySession(item);
        navigateTo(ROUTES.studentFeExam);
        return;
      }

      if (item.type === "PE") {
        await openPeHistoryResult(item.sessionId, item.latestPeSubmissionId);
        navigateTo(ROUTES.studentResultViewer);
        return;
      }

      throw new Error("This practice type is not supported yet.");
    } catch (error) {
      setErrorMessage(error.message || "Unable to open the practice result.");
    } finally {
      setPendingActionKey("");
    }
  }

  async function handleRetryHistoryItem(item) {
    setErrorMessage("");
    setPendingActionKey(`retry-${item.id}`);

    try {
      if (item.examDeleted) {
        throw new Error("This exam was removed, so you can only view past results.");
      }

      if (item.type === "FE") {
        await createFeExamSession({
          examId: item.examId,
          examTitle: item.examTitle,
          courseId: item.courseId,
          forceNew: true
        });
        navigateTo(ROUTES.studentFeExam);
        return;
      }

      if (item.type === "PE") {
        await createPePracticeSession({
          examId: item.examId,
          title: item.examTitle
        });
        navigateTo(ROUTES.studentCodingPractice);
        return;
      }

      throw new Error("This practice type is not supported yet.");
    } catch (error) {
      setErrorMessage(error.message || "Unable to create a new practice session.");
    } finally {
      setPendingActionKey("");
    }
  }

  async function handleResumeHistoryItem(item) {
    setErrorMessage("");
    setPendingActionKey(`resume-${item.id}`);

    try {
      if (item.type === "FE") {
        await resumeFeExamSession({
          examId: item.examId,
          sessionId: item.sessionId,
          examTitle: item.examTitle,
          courseId: item.courseId
        });
        navigateTo(ROUTES.studentFeExam);
        return;
      }

      if (item.type === "PE") {
        await resumePePracticeSession({
          examId: item.examId,
          sessionId: item.sessionId,
          examTitle: item.examTitle,
          courseId: item.courseId
        });
        navigateTo(ROUTES.studentCodingPractice);
        return;
      }

      throw new Error("This practice type is not supported yet.");
    } catch (error) {
      setErrorMessage(error.message || "Unable to resume this practice session.");
    } finally {
      setPendingActionKey("");
    }
  }

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentPracticeHistory}
      title="Practice History"
      subtitle="Review completed FE and PE sessions, then jump back into the next attempt."
      topbarActionLabel="Open Exams"
      topbarActionHref={ROUTES.studentExams}
      showPageHeader={false}
    >
      <section className="practice-shell">
        <PracticeSidebar activeTab="practice-history" />
        <div className="practice-shell__content practice-history-content">
          {errorMessage ? <section className="student-status-banner">{errorMessage}</section> : null}

          <section className="student-panel student-table-panel practice-history-workspace">
            <div className="practice-history-workspace__toolbar">
              <div className="practice-history-workspace__copy">
                <span className="student-panel__eyebrow">Practice history</span>
                <h2>Review completed FE and PE sessions</h2>
              </div>
              <div className="practice-history-workspace__filters">
                <label className="student-filter-field practice-history-filter-field">
                  <span>Course</span>
                  <select
                    value={filters.courseId}
                    onChange={(event) => {
                      setFilters((current) => ({ ...current, courseId: event.target.value }));
                      setPagination((current) => ({ ...current, page: 1 }));
                    }}
                  >
                    <option value="all">All Courses</option>
                    {courses.map((course) => (
                      <option key={course.id} value={course.id}>{course.code}</option>
                    ))}
                  </select>
                </label>

                <label className="student-filter-field practice-history-filter-field">
                  <span>Exam Type</span>
                  <select
                    value={filters.type}
                    onChange={(event) => {
                      setFilters((current) => ({ ...current, type: event.target.value }));
                      setPagination((current) => ({ ...current, page: 1 }));
                    }}
                  >
                    <option value="All">All</option>
                    <option value="FE">FE</option>
                    <option value="PE">PE</option>
                  </select>
                </label>
              </div>
            </div>

            <div className="student-table practice-history-table">
              <div className="student-table__head practice-history-table__head">
                <span>Date</span>
                <span>Course</span>
                <span className="practice-history-table__metric-head">Type</span>
                <span className="practice-history-table__metric-head">Score</span>
                <span className="practice-history-table__metric-head">Duration</span>
                <span className="practice-history-table__metric-head">Action</span>
              </div>
              <div className="student-table__body practice-history-table__body">
                {rows.map((item) => (
                  <article key={item.id} className="student-table__row practice-history-table__row">
                    <span className="practice-history-table__date">{formatDate(item.date)}</span>
                    <strong className="practice-history-table__course">{item.courseName}</strong>
                    <span className={`student-pill practice-history-table__metric-cell ${item.type === "PE" ? "is-green" : "is-blue"}`}>{item.type}</span>
                    <span className="practice-history-table__metric-copy">{item.scoreLabel}</span>
                    <span className="practice-history-table__metric-copy">{item.durationMinutes} min</span>
                    <div className="practice-history-table__action">
                      <button
                        type="button"
                        className="student-link-button"
                        onClick={() => handleOpenHistoryItem(item)}
                        disabled={pendingActionKey === `view-${item.id}` || pendingActionKey === `retry-${item.id}` || pendingActionKey === `resume-${item.id}`}
                      >
                        {pendingActionKey === `view-${item.id}` ? "Opening..." : "View"}
                      </button>
                      {isResumable(item) ? (
                        <button
                          type="button"
                          className="student-link-button"
                          onClick={() => handleResumeHistoryItem(item)}
                          disabled={pendingActionKey === `view-${item.id}` || pendingActionKey === `retry-${item.id}` || pendingActionKey === `resume-${item.id}`}
                        >
                          {pendingActionKey === `resume-${item.id}` ? "Resuming..." : "Resume"}
                        </button>
                      ) : null}
                      <button
                        type="button"
                        className="student-link-button"
                        onClick={() => handleRetryHistoryItem(item)}
                        disabled={item.examDeleted || pendingActionKey === `view-${item.id}` || pendingActionKey === `retry-${item.id}` || pendingActionKey === `resume-${item.id}`}
                      >
                        {pendingActionKey === `retry-${item.id}` ? "Creating..." : "Retry"}
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            </div>

            <div className="admin-table-panel__footer">
              <p>Showing {rows.length} of {pagination.total} sessions</p>
              <div className="admin-pagination">
                <button
                  type="button"
                  className={pagination.page <= 1 ? "is-disabled" : ""}
                  aria-label="Previous page"
                  disabled={pagination.page <= 1}
                  onClick={() => setPagination((current) => ({ ...current, page: Math.max(1, current.page - 1) }))}
                >
                  &#8249;
                </button>
                <button type="button" className="is-active" style={{ minWidth: 36, cursor: "default" }}>
                  {pagination.page}
                </button>
                <button
                  type="button"
                  className={pagination.page >= pagination.totalPages ? "is-disabled" : ""}
                  aria-label="Next page"
                  disabled={pagination.page >= pagination.totalPages}
                  onClick={() =>
                    setPagination((current) => ({
                      ...current,
                      page: Math.min(pagination.totalPages, current.page + 1)
                    }))
                  }
                >
                  &#8250;
                </button>
              </div>
            </div>
          </section>
        </div>
      </section>
    </StudentScaffold>
  );
}

