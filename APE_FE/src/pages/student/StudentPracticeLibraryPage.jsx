import { useEffect, useMemo, useState } from "react";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { PracticeSidebar } from "../../components/student/PracticeSidebar";
import { ROUTES, navigateTo } from "../../lib/routes";
import { deleteStudentExam, getStudentCourses, getStudentExamLibrary, getStudentSystemExamLibrary } from "../../services/studentExamService";
import { createFeExamSession, createFlashcardSession, createPePracticeSession, resumeFeExamSession, resumePePracticeSession } from "../../services/studentWorkspaceService";
import { ConfirmModal } from "../../shared/ui";

function StudentExamLibraryPage({ scope = "mine" }) {
  const [courses, setCourses] = useState([]);
  const [exams, setExams] = useState([]);
  const [filters, setFilters] = useState({ courseId: "all", type: "all" });
  const [isLoading, setIsLoading] = useState(true);
  const [openingExamId, setOpeningExamId] = useState("");
  const [resumingExamId, setResumingExamId] = useState("");
  const [pendingDeleteExam, setPendingDeleteExam] = useState(null);
  const [deletingExamId, setDeletingExamId] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadData() {
      setIsLoading(true);
      setError("");

      try {
        const [courseItems, examResponse] = await Promise.all([
          getStudentCourses(),
          scope === "system"
            ? getStudentSystemExamLibrary({ page: 1, limit: 100 })
            : getStudentExamLibrary({ page: 1, limit: 100 })
        ]);

        if (ignore) {
          return;
        }

        setCourses(courseItems);
        setExams(examResponse?.items || []);
      } catch (loadError) {
        if (!ignore) {
          setError(loadError.message || "Unable to load exam library.");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadData();

    return () => {
      ignore = true;
    };
  }, [scope]);

  const rows = useMemo(() => {
    const items = exams.filter((exam) => {
      if (filters.courseId !== "all" && exam.courseId !== filters.courseId) {
        return false;
      }

      if (filters.type !== "all" && String(exam.examType || "").toLowerCase() !== filters.type) {
        return false;
      }

      return true;
    });

    return items.map((exam) => {
      const course = courses.find((item) => item.id === exam.courseId);
      return {
        ...exam,
        courseName: course?.name || exam.courseId,
        courseCode: course?.code || "N/A",
        totalQuestions: (exam.feQuestionCount || 0) + (exam.peQuestionCount || 0)
      };
    });
  }, [courses, exams, filters.courseId, filters.type]);

  async function handleOpenExam(exam) {
    setOpeningExamId(exam.id);
    setError("");

    try {
      const normalizedMode = String(exam?.mode || "").trim().toLowerCase();

      if (normalizedMode === "flashcard") {
        await createFlashcardSession({
          examId: exam.id,
          examTitle: exam.title,
          courseId: exam.courseId
        });
        navigateTo(ROUTES.studentFlashcards);
        return;
      }

      if (exam.examType === "FE") {
        await createFeExamSession({
          examId: exam.id,
          examTitle: exam.title,
          courseId: exam.courseId
        });
        navigateTo(ROUTES.studentFeExam);
        return;
      }

      if (exam.examType === "PE") {
        await createPePracticeSession({
          examId: exam.id,
          title: exam.title
        });
        navigateTo(ROUTES.studentCodingPractice);
        return;
      }

      throw new Error("Exam Mixed chua co man hinh mo truc tiep tu Library.");
    } catch (openError) {
      setError(openError.message || "Unable to open this exam.");
    } finally {
      setOpeningExamId("");
    }
  }

  async function handleResumeExam(exam) {
    setResumingExamId(exam.id);
    setError("");

    try {
      if (!exam.activeSessionId) {
        throw new Error("This exam does not have a valid resume session.");
      }

      if (String(exam?.mode || "").trim().toLowerCase() === "flashcard") {
        throw new Error("Flashcard resume is not supported from this screen yet.");
      }

      if (exam.examType === "FE") {
        await resumeFeExamSession({
          examId: exam.id,
          sessionId: exam.activeSessionId,
          examTitle: exam.title,
          courseId: exam.courseId
        });
        navigateTo(ROUTES.studentFeExam);
        return;
      }

      if (exam.examType !== "PE") {
        throw new Error("This exam type does not have a valid resume flow yet.");
      }

      await resumePePracticeSession({
        examId: exam.id,
        sessionId: exam.activeSessionId,
        examTitle: exam.title,
        courseId: exam.courseId
      });
      navigateTo(ROUTES.studentCodingPractice);
    } catch (resumeError) {
      setError(resumeError.message || "Unable to resume this exam.");
    } finally {
      setResumingExamId("");
    }
  }

  async function handleConfirmDelete() {
    if (!pendingDeleteExam?.id) {
      setPendingDeleteExam(null);
      return;
    }

    setDeletingExamId(pendingDeleteExam.id);
    setError("");
    try {
      await deleteStudentExam(pendingDeleteExam.id);
      setExams((current) => current.filter((item) => item.id !== pendingDeleteExam.id));
    } catch (deleteError) {
      setError(deleteError.message || "Unable to delete this exam.");
    } finally {
      setDeletingExamId("");
      setPendingDeleteExam(null);
    }
  }

  return (
    <StudentScaffold
      activeRoute={scope === "system" ? ROUTES.studentSystemExamLibrary : ROUTES.studentPracticeLibrary}
      title={scope === "system" ? "System Exams" : "My Exams"}
      subtitle={scope === "system" ? "Public exams from the system, ready for practice." : "Practice exams that belong to your own account."}
      topbarActionLabel="Open Exam Config"
      topbarActionHref={ROUTES.studentExams}
      showPageHeader={false}
    >
      <section className="practice-shell">
        <PracticeSidebar activeTab={scope === "system" ? "system-exams" : "my-exams"} />
        <div className="practice-shell__content practice-library-content">
          <section className="student-panel student-table-panel practice-library-workspace">
            <div className="practice-library-workspace__toolbar">
              <div className="practice-library-workspace__copy">
                <span className="student-panel__eyebrow">{scope === "system" ? "System exams" : "My exam library"}</span>
                <h2>{scope === "system" ? "Browse public exams from the system" : "Browse your own configured practice exams"}</h2>
              </div>
              <label className="student-filter-field practice-library-filter-field">
                <span>Course</span>
                <select
                  value={filters.courseId}
                  onChange={(event) => setFilters((current) => ({ ...current, courseId: event.target.value }))}
                  disabled={isLoading}
                >
                  <option value="all">All Courses</option>
                  {courses.map((course) => (
                    <option key={course.id} value={course.id}>
                      {course.code ? `${course.code} - ${course.name}` : course.name}
                    </option>
                  ))}
                </select>
              </label>

                <label className="student-filter-field practice-library-filter-field">
                  <span>Type</span>
                  <select
                    value={filters.type}
                    onChange={(event) => setFilters((current) => ({ ...current, type: event.target.value }))}
                    disabled={isLoading}
                  >
                    <option value="all">All Types</option>
                    <option value="fe">FE</option>
                    <option value="pe">PE</option>
                  </select>
                </label>
              </div>

            <div className="student-table practice-library-table">
              <div className="student-table__head practice-library-table__head">
                <span>Exam</span>
                <span>Course</span>
                <span className="practice-library-table__metric-head">Type</span>
                <span className="practice-library-table__metric-head">Mode</span>
                <span className="practice-library-table__metric-head">Questions</span>
                <span className="practice-library-table__metric-head">Action</span>
              </div>
              <div className="student-table__body practice-library-table__body">
                {isLoading ? (
                  <article className="student-table__row practice-library-table__row">
                    <span>Loading exams...</span>
                    <span />
                    <span />
                    <span />
                    <span />
                    <span />
                  </article>
                ) : null}

                {!isLoading && error ? (
                  <article className="student-table__row practice-library-table__row">
                    <strong>{error}</strong>
                    <span />
                    <span />
                    <span />
                    <span />
                    <span />
                  </article>
                ) : null}

                {!isLoading && !error && !rows.length ? (
                  <article className="student-table__row practice-library-table__row">
                    <strong>No exams found</strong>
                    <span>There are no exams matching the current course and type filters.</span>
                    <span />
                    <span />
                    <span />
                    <span />
                    <span />
                  </article>
                ) : null}

                {!isLoading && !error
                  ? rows.map((item) => (
                      <article key={item.id} className="student-table__row practice-library-table__row">
                        <div className="student-table__exam-cell practice-library-table__exam-cell">
                          <strong>{item.title}</strong>
                        </div>
                        <span className="practice-library-table__course">{item.courseCode} - {item.courseName}</span>
                        <span className={`student-pill practice-library-table__metric-cell ${item.examType === "FE" ? "is-blue" : item.examType === "PE" ? "is-orange" : "is-purple"}`}>
                          {item.examType}
                        </span>
                        <span className={`student-pill practice-library-table__metric-cell ${item.mode === "Practice" ? "is-blue" : "is-orange"}`}>
                          {item.mode}
                        </span>
                        <span className="practice-library-table__metric-copy">{item.totalQuestions}</span>
                        <div className="practice-library-table__action">
                          <button
                            type="button"
                            className="student-link-button"
                            onClick={() => handleOpenExam(item)}
                            disabled={openingExamId === item.id || resumingExamId === item.id}
                          >
                            {openingExamId === item.id ? "Opening..." : "Open"}
                          </button>
                          {item.hasActiveSession ? (
                            <button
                              type="button"
                              className="student-link-button"
                              onClick={() => handleResumeExam(item)}
                              disabled={openingExamId === item.id || resumingExamId === item.id}
                            >
                              {resumingExamId === item.id ? "Resuming..." : "Resume"}
                            </button>
                          ) : null}
                          {scope !== "system" ? (
                            <button
                              type="button"
                              className="student-link-button"
                              onClick={() => setPendingDeleteExam(item)}
                              disabled={openingExamId === item.id || resumingExamId === item.id || Boolean(deletingExamId)}
                            >
                              Delete
                            </button>
                          ) : null}
                        </div>
                      </article>
                    ))
                  : null}
              </div>
            </div>
          </section>
        </div>
      </section>

      <ConfirmModal
        open={Boolean(pendingDeleteExam)}
        title="Delete exam"
        message={pendingDeleteExam ? `Soft-delete "${pendingDeleteExam.title}"? You will no longer see it in My Exams, but your past practice history will remain.` : ""}
        confirmLabel={deletingExamId ? "Deleting..." : "Delete"}
        isConfirming={Boolean(deletingExamId)}
        onCancel={() => !deletingExamId && setPendingDeleteExam(null)}
        onConfirm={handleConfirmDelete}
      />
    </StudentScaffold>
  );
}

export function StudentPracticeLibraryPage() {
  return <StudentExamLibraryPage scope="mine" />;
}

export function StudentSystemExamLibraryPage() {
  return <StudentExamLibraryPage scope="system" />;
}
