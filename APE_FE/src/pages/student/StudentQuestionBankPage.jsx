import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { PracticeSidebar } from "../../components/student/PracticeSidebar";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES } from "../../lib/routes";
import { getStudentCourses } from "../../services/studentExamService";
import { ConfirmModal } from "../../shared/ui";
import {
  deleteStudentQuestion,
  getStudentQuestionBank,
  getStudentQuestionBankDetail,
  getStudentQuestionBankTopicTags,
  publishAllStudentQuestions,
  publishStudentQuestion
} from "../../services/studentQuestionBankService";

function formatCourseLabel(course) {
  return course?.code ? `${course.code} - ${course.name}` : course?.name || "Unknown course";
}

export function StudentQuestionBankPage() {
  const [courses, setCourses] = useState([]);
  const [topicTagOptions, setTopicTagOptions] = useState([]);
  const [questions, setQuestions] = useState([]);
  const [selectedQuestionId, setSelectedQuestionId] = useState("");
  const [selectedQuestion, setSelectedQuestion] = useState(null);
  const [filters, setFilters] = useState({
    courseId: "all",
    type: "all",
    difficulty: "all",
    status: "all",
    topicTags: [],
    keyword: ""
  });
  const [pagination, setPagination] = useState({ page: 1, limit: 10, total: 0, totalPages: 1 });
  const [isLoading, setIsLoading] = useState(true);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [isPublishing, setIsPublishing] = useState(false);
  const [questionPendingDelete, setQuestionPendingDelete] = useState(null);
  const [bulkPublishPending, setBulkPublishPending] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadCourses() {
      try {
        const items = await getStudentCourses();
        if (!ignore) {
          setCourses(items);
        }
      } catch (loadError) {
        if (!ignore) {
          setError(loadError.message || "Unable to load courses.");
        }
      }
    }

    loadCourses();

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    let ignore = false;

    async function loadTopicTags() {
      try {
        const tags = await getStudentQuestionBankTopicTags(filters.courseId);
        if (ignore) {
          return;
        }

        setTopicTagOptions(tags);
        setFilters((current) => ({
          ...current,
          topicTags: current.topicTags.filter((tag) => tags.includes(tag))
        }));
      } catch (loadError) {
        if (!ignore) {
          setTopicTagOptions([]);
          setError(loadError.message || "Unable to load topic tags.");
        }
      }
    }

    loadTopicTags();

    return () => {
      ignore = true;
    };
  }, [filters.courseId]);

  useEffect(() => {
    let ignore = false;
    const timeoutId = window.setTimeout(async () => {
      setIsLoading(true);
      setError("");

      try {
        const response = await getStudentQuestionBank({
          ...filters,
          page: pagination.page,
          limit: pagination.limit
        });

        if (ignore) {
          return;
        }

        const nextItems = response.items || [];
        setQuestions(nextItems);
        setPagination((current) => ({
          ...current,
          total: Number(response?.total ?? 0),
          totalPages: Number(response?.totalPages ?? 1)
        }));

        if (!nextItems.length) {
          setSelectedQuestionId("");
          setSelectedQuestion(null);
          return;
        }

        if (!selectedQuestionId || !nextItems.some((item) => item.id === selectedQuestionId)) {
          setSelectedQuestionId(nextItems[0].id);
        }
      } catch (loadError) {
        if (!ignore) {
          setQuestions([]);
          setSelectedQuestionId("");
          setSelectedQuestion(null);
          setError(loadError.message || "Unable to load question bank.");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }, 250);

    return () => {
      ignore = true;
      window.clearTimeout(timeoutId);
    };
  }, [filters, pagination.page, pagination.limit]);

  useEffect(() => {
    let ignore = false;

    async function loadDetail() {
      if (!selectedQuestionId) {
        if (!ignore) {
          setSelectedQuestion(null);
        }
        return;
      }

      setIsDetailLoading(true);

      try {
        const detail = await getStudentQuestionBankDetail(selectedQuestionId);
        if (!ignore) {
          setSelectedQuestion(detail);
        }
      } catch (loadError) {
        if (!ignore) {
          setSelectedQuestion(null);
          setError(loadError.message || "Unable to load question detail.");
        }
      } finally {
        if (!ignore) {
          setIsDetailLoading(false);
        }
      }
    }

    loadDetail();

    return () => {
      ignore = true;
    };
  }, [selectedQuestionId]);

  const selectedCourse = useMemo(
    () => courses.find((course) => course.id === filters.courseId) || null,
    [courses, filters.courseId]
  );

  function toggleTopicTag(tag) {
    setFilters((current) => ({
      ...current,
      topicTags: current.topicTags.includes(tag)
        ? current.topicTags.filter((item) => item !== tag)
        : [...current.topicTags, tag]
    }));
    setPagination((current) => ({ ...current, page: 1 }));
  }

  async function handlePublishQuestion() {
    if (!selectedQuestion?.id || !selectedQuestion?.canPublish || isPublishing) {
      return;
    }

    setIsPublishing(true);
    setError("");

    try {
      await publishStudentQuestion(selectedQuestion.id);
      const response = await getStudentQuestionBank({
        ...filters,
        page: pagination.page,
        limit: pagination.limit
      });

      const nextItems = response.items || [];
      setQuestions(nextItems);
      setPagination((current) => ({
        ...current,
        total: Number(response?.total ?? 0),
        totalPages: Number(response?.totalPages ?? 1)
      }));

      const refreshedSelected = nextItems.find((item) => item.id === selectedQuestion.id);
      if (refreshedSelected) {
        setSelectedQuestionId(refreshedSelected.id);
        const detail = await getStudentQuestionBankDetail(refreshedSelected.id);
        setSelectedQuestion(detail);
      }
    } catch (publishError) {
      setError(publishError.message || "Unable to activate this question.");
    } finally {
      setIsPublishing(false);
    }
  }

  async function handleDeleteQuestion() {
    if (!selectedQuestion?.id || isPublishing) {
      return;
    }

    setIsPublishing(true);
    setError("");

    try {
      await deleteStudentQuestion(selectedQuestion.id);
      const response = await getStudentQuestionBank({
        ...filters,
        page: pagination.page,
        limit: pagination.limit
      });

      const nextItems = response.items || [];
      setQuestions(nextItems);
      setPagination((current) => ({
        ...current,
        total: Number(response?.total ?? 0),
        totalPages: Number(response?.totalPages ?? 1)
      }));

      const nextSelectedId = nextItems[0]?.id || "";
      setSelectedQuestionId(nextSelectedId);
      if (!nextSelectedId) {
        setSelectedQuestion(null);
      }
      setQuestionPendingDelete(null);
    } catch (deleteError) {
      setError(deleteError.message || "Unable to delete this question.");
    } finally {
      setIsPublishing(false);
    }
  }

  async function handlePublishAllQuestions() {
    if (isPublishing) {
      return;
    }

    setIsPublishing(true);
    setError("");

    try {
      await publishAllStudentQuestions(filters);
      const response = await getStudentQuestionBank({
        ...filters,
        page: pagination.page,
        limit: pagination.limit
      });

      const nextItems = response.items || [];
      setQuestions(nextItems);
      setPagination((current) => ({
        ...current,
        total: Number(response?.total ?? 0),
        totalPages: Number(response?.totalPages ?? 1)
      }));

      if (selectedQuestionId) {
        const refreshedSelected = nextItems.find((item) => item.id === selectedQuestionId);
        if (refreshedSelected) {
          const detail = await getStudentQuestionBankDetail(refreshedSelected.id);
          setSelectedQuestion(detail);
        }
      }

      setBulkPublishPending(false);
    } catch (publishError) {
      setError(publishError.message || "Unable to activate your draft questions.");
    } finally {
      setIsPublishing(false);
    }
  }

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentQuestionBank}
      title="Question Bank"
      subtitle="Browse the questions you generated, filter them quickly, and inspect each one in full detail."
      topbarActionLabel="Open Practice"
      topbarActionHref={ROUTES.studentPractice}
      showPageHeader={false}
    >
      <section className="practice-shell">
        <PracticeSidebar activeTab="question-bank" />
        <div className="practice-shell__content">
          <div className="student-page-grid question-bank-page-grid">
            <section className="student-panel student-table-panel question-bank-list-panel">
              <div className="question-bank-toolbar">
                <div className="question-bank-toolbar__copy">
                  <div className="question-bank-toolbar__headline">
                    <span className="student-panel__eyebrow question-bank-toolbar__eyebrow">Student-owned bank</span>
                    <div className="question-bank-toolbar__actions">
                      <Button
                        type="button"
                        variant="secondary"
                        onClick={() => setBulkPublishPending(true)}
                        disabled={isPublishing}
                      >
                        Activate My Draft Questions
                      </Button>
                    </div>
                  </div>
                  <h2>Browse your generated FE and PE questions</h2>
                  <p>
                    {filters.courseId === "all" ? "All courses" : selectedCourse ? formatCourseLabel(selectedCourse) : "Selected course"}
                  </p>
                </div>

                <div className="question-bank-toolbar__filters">
                  <label className="student-filter-field">
                    <span>Keyword</span>
                    <input
                      type="text"
                      value={filters.keyword}
                      onChange={(event) => {
                        setFilters((current) => ({ ...current, keyword: event.target.value }));
                        setPagination((current) => ({ ...current, page: 1 }));
                      }}
                      placeholder="Search by question title"
                    />
                  </label>

                  <label className="student-filter-field">
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
                        <option key={course.id} value={course.id}>
                          {formatCourseLabel(course)}
                        </option>
                      ))}
                    </select>
                  </label>

                  <label className="student-filter-field">
                    <span>Question Type</span>
                    <select
                      value={filters.type}
                      onChange={(event) => {
                        setFilters((current) => ({ ...current, type: event.target.value }));
                        setPagination((current) => ({ ...current, page: 1 }));
                      }}
                    >
                      <option value="all">All Types</option>
                      <option value="FE">FE</option>
                      <option value="PE">PE</option>
                    </select>
                  </label>

                  <label className="student-filter-field">
                    <span>Difficulty</span>
                    <select
                      value={filters.difficulty}
                      onChange={(event) => {
                        setFilters((current) => ({ ...current, difficulty: event.target.value }));
                        setPagination((current) => ({ ...current, page: 1 }));
                      }}
                    >
                      <option value="all">All Levels</option>
                      <option value="Easy">Easy</option>
                      <option value="Medium">Medium</option>
                      <option value="Hard">Hard</option>
                    </select>
                  </label>

                  <label className="student-filter-field">
                    <span>Status</span>
                    <select
                      value={filters.status}
                      onChange={(event) => {
                        setFilters((current) => ({ ...current, status: event.target.value }));
                        setPagination((current) => ({ ...current, page: 1 }));
                      }}
                    >
                      <option value="all">All Statuses</option>
                      <option value="Active">Active</option>
                      <option value="Draft">Draft</option>
                    </select>
                  </label>
                </div>
              </div>

              <div className="question-bank-topic-filter">
                <span>Topic Tags</span>
                <div className="question-bank-topic-filter__list">
                  {topicTagOptions.length ? (
                    topicTagOptions.map((tag) => (
                      <button
                        key={tag}
                        type="button"
                        className={`question-bank-tag ${filters.topicTags.includes(tag) ? "is-active" : ""}`}
                        onClick={() => toggleTopicTag(tag)}
                      >
                        {tag}
                      </button>
                    ))
                  ) : (
                    <p>No topic tags available for the current course.</p>
                  )}
                </div>
              </div>

              <div className="student-table question-bank-table">
                <div className="student-table__head question-bank-table__head">
                  <span>Title</span>
                  <span>Type</span>
                  <span>Status</span>
                  <span>Difficulty</span>
                  <span>Topic Tags</span>
                  <span>Course</span>
                </div>
                <div className="student-table__body">
                  {isLoading ? (
                    <article className="student-table__row question-bank-table__row">
                      <strong>Loading question bank...</strong>
                      <span />
                      <span />
                      <span />
                      <span />
                      <span />
                    </article>
                  ) : null}

                  {!isLoading && error ? (
                    <article className="student-table__row question-bank-table__row">
                      <strong>{error}</strong>
                      <span />
                      <span />
                      <span />
                      <span />
                      <span />
                    </article>
                  ) : null}

                  {!isLoading && !error && !questions.length ? (
                    <article className="student-table__row question-bank-table__row">
                      <strong>No questions found</strong>
                      <span />
                      <span />
                      <span />
                      <span />
                      <span />
                    </article>
                  ) : null}

                  {!isLoading && !error
                    ? questions.map((question) => (
                        <article
                          key={question.id}
                          className={`student-table__row question-bank-table__row${selectedQuestionId === question.id ? " is-highlighted" : ""}`}
                          onClick={() => setSelectedQuestionId(question.id)}
                          role="button"
                          tabIndex={0}
                          onKeyDown={(event) => {
                            if (event.key === "Enter" || event.key === " ") {
                              event.preventDefault();
                              setSelectedQuestionId(question.id);
                            }
                          }}
                        >
                          <div className="question-bank-table__title">
                            <strong>{question.title}</strong>
                          </div>
                          <span><span className={`student-pill ${question.type === "PE" ? "is-orange" : "is-blue"}`}>{question.type}</span></span>
                          <span><span className={`student-pill ${question.status === "Draft" ? "is-orange" : "is-green"}`}>{question.status}</span></span>
                          <span>{question.difficulty}</span>
                          <span className="question-bank-table__tags">{question.topicTags.join(", ") || "No tags"}</span>
                          <span>{question.courseCode || question.courseName || question.courseId}</span>
                        </article>
                      ))
                    : null}
                </div>
              </div>

              <div className="question-bank-pagination">
                <p>{`Showing ${questions.length} of ${pagination.total} questions`}</p>
                <div className="admin-pagination">
                  <button
                    type="button"
                    className={pagination.page <= 1 ? "is-disabled" : ""}
                    disabled={pagination.page <= 1}
                    onClick={() => setPagination((current) => ({ ...current, page: Math.max(1, current.page - 1) }))}
                  >
                    &#8249;
                  </button>
                  <button type="button" className="is-active">
                    {pagination.page}
                  </button>
                  <button
                    type="button"
                    className={pagination.page >= pagination.totalPages ? "is-disabled" : ""}
                    disabled={pagination.page >= pagination.totalPages}
                    onClick={() => setPagination((current) => ({ ...current, page: Math.min(pagination.totalPages || 1, current.page + 1) }))}
                  >
                    &#8250;
                  </button>
                </div>
              </div>
            </section>

            <aside className="student-panel question-bank-detail-panel">
              <div className="student-panel__header student-panel__header--stacked">
                <span className="student-panel__eyebrow">Question detail</span>
                <h2>{selectedQuestion?.title || "Question preview"}</h2>
              </div>

              {isDetailLoading ? <p>Loading question detail...</p> : null}

              {!isDetailLoading && selectedQuestion ? (
                <div className="question-bank-detail">
                  <section className="question-bank-detail__section">
                    <h3>Problem Statement</h3>
                    <p>{selectedQuestion.description || "No description available."}</p>
                  </section>

                  <section className="question-bank-detail__section">
                    <h3>Status</h3>
                    <p>{selectedQuestion.status || "Unknown"}</p>
                  </section>

                  {selectedQuestion.type === "FE" ? (
                    <>
                      <section className="question-bank-detail__section">
                        <h3>Options</h3>
                        <div className="question-bank-detail__stack">
                          {selectedQuestion.options.map((option) => (
                            <div key={option} className="question-bank-detail__card">
                              {option}
                            </div>
                          ))}
                        </div>
                      </section>

                      <section className="question-bank-detail__section">
                        <h3>Correct Answer</h3>
                        <p>{selectedQuestion.correctAnswer.join(", ") || "No correct answer available."}</p>
                      </section>

                      <section className="question-bank-detail__section">
                        <h3>Explanation</h3>
                        <p>{selectedQuestion.explanation || "No explanation available."}</p>
                      </section>
                    </>
                  ) : (
                    <>
                      <section className="question-bank-detail__section">
                        <h3>Skeleton Code</h3>
                        <div className="question-bank-detail__stack">
                          {selectedQuestion.skeletonCode.length ? (
                            selectedQuestion.skeletonCode.map((file, index) => (
                              <div key={`${file.filename || file.Filename}-${index}`} className="question-bank-detail__card">
                                <strong>{file.filename || file.Filename || `File ${index + 1}`}</strong>
                                <pre>{file.content || file.Content || ""}</pre>
                              </div>
                            ))
                          ) : (
                            <p>No skeleton code available.</p>
                          )}
                        </div>
                      </section>

                      <section className="question-bank-detail__section">
                        <h3>Sample Test Cases</h3>
                        <div className="question-bank-detail__stack">
                          {selectedQuestion.sampleTestCases.length ? (
                            selectedQuestion.sampleTestCases.map((testCase, index) => (
                              <div key={`${testCase.input}-${index}`} className="question-bank-detail__card">
                                <strong>Case {index + 1}</strong>
                                <pre>{testCase.input || "(empty input)"}</pre>
                                <pre>{testCase.expectedOutput || "(empty output)"}</pre>
                              </div>
                            ))
                          ) : (
                            <p>No visible sample test cases.</p>
                          )}
                        </div>
                      </section>
                    </>
                  )}

                  <div className="question-bank-detail__actions">
                    {selectedQuestion?.canPublish ? (
                      <Button
                        type="button"
                        variant="primary"
                        onClick={handlePublishQuestion}
                        disabled={isPublishing}
                      >
                        {isPublishing ? "Activating..." : "Active Question"}
                      </Button>
                    ) : null}
                    <Button
                      type="button"
                      variant="secondary"
                      className="question-bank-detail__delete-button"
                      onClick={() => setQuestionPendingDelete(selectedQuestion)}
                      disabled={isPublishing}
                    >
                      {isPublishing ? "Deleting..." : "Delete Question"}
                    </Button>
                  </div>
                </div>
              ) : null}
            </aside>
          </div>
        </div>
      </section>
      <ConfirmModal
        open={Boolean(questionPendingDelete)}
        title="Delete question"
        message={
          questionPendingDelete
            ? `Delete "${questionPendingDelete.title}"? Deleted questions cannot be activated again.`
            : ""
        }
        confirmLabel="Delete"
        isConfirming={isPublishing}
        onCancel={() => !isPublishing && setQuestionPendingDelete(null)}
        onConfirm={handleDeleteQuestion}
      />
      <ConfirmModal
        open={bulkPublishPending}
        title="Activate your draft questions"
        message="Activate all of your draft questions that match the current filters? Deleted questions will remain unchanged."
        confirmLabel="Activate All"
        isConfirming={isPublishing}
        onCancel={() => !isPublishing && setBulkPublishPending(false)}
        onConfirm={handlePublishAllQuestions}
      />
    </StudentScaffold>
  );
}
