import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { AdminGlyph } from "../../components/admin/AdminGlyph";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { ROUTES } from "../../lib/routes";
import { getCourses } from "../../services/courseService";
import {
  deleteQuestion,
  getQuestionDetail,
  getQuestions,
  importQuestionsExcel,
  publishAllQuestions,
  publishQuestion,
  toggleQuestionDisabled,
  updateQuestion
} from "../../services/questionService";
import { ConfirmModal, ToastNotification } from "../../shared/ui";
import { getAccessToken } from "../../lib/storage";

function formatCourseLabel(course) {
  return course?.code ? `${course.code} - ${course.name}` : course?.name || "Untitled course";
}

function formatDate(value) {
  if (!value) {
    return "Not updated";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric"
  });
}

function buildEditForm(question) {
  return {
    title: question?.title || "",
    description: question?.description || "",
    difficulty: question?.difficulty || "Easy",
    topicTags: Array.isArray(question?.topicTags) ? question.topicTags.join(", ") : "",
    optionsText: Array.isArray(question?.options) ? question.options.join("\n") : "",
    correctAnswerText: Array.isArray(question?.correctAnswer) ? question.correctAnswer.join(", ") : "",
    skeletonCodeText: Array.isArray(question?.skeletonCode) ? JSON.stringify(question.skeletonCode, null, 2) : "[]",
    testCasesText: Array.isArray(question?.testCases) ? JSON.stringify(question.testCases, null, 2) : "[]"
  };
}

function formatDateTime(value) {
  if (!value) {
    return "Not available";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function formatNullable(value, fallback = "Not available") {
  return value === null || value === undefined || value === "" ? fallback : String(value);
}

function parseJsonArrayField(value, fieldLabel) {
  const trimmed = value.trim();
  if (!trimmed) {
    return [];
  }

  let parsed;
  try {
    parsed = JSON.parse(trimmed);
  } catch {
    throw new Error(`${fieldLabel} must be valid JSON array.`);
  }

  if (!Array.isArray(parsed)) {
    throw new Error(`${fieldLabel} must be a JSON array.`);
  }

  return parsed;
}

function extractQuestionItems(response) {
  if (Array.isArray(response?.items)) {
    return response.items;
  }

  const feItems = Array.isArray(response?.fe) ? response.fe : Array.isArray(response?.FE) ? response.FE : [];
  const peItems = Array.isArray(response?.pe) ? response.pe : Array.isArray(response?.PE) ? response.PE : [];
  return [...feItems, ...peItems];
}

function extractQuestionTotal(response, items) {
  if (typeof response?.total === "number") {
    return response.total;
  }

  const feTotal = typeof response?.feTotal === "number" ? response.feTotal : 0;
  const peTotal = typeof response?.peTotal === "number" ? response.peTotal : 0;
  return feTotal + peTotal || items.length;
}

export function AdminQuestionsPage({ isAuthenticated = false }) {
  const [courses, setCourses] = useState([]);
  const [filters, setFilters] = useState({
    courseId: "all",
    type: "all",
    difficulty: "all",
    status: "all"
  });
  const [questions, setQuestions] = useState([]);
  const [selectedQuestionDetail, setSelectedQuestionDetail] = useState(null);
  const [pagination, setPagination] = useState({ page: 1, limit: 10, total: 0, totalPages: 1 });
  const [isLoading, setIsLoading] = useState(false);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [toast, setToast] = useState(null);
  const [selectedQuestionId, setSelectedQuestionId] = useState("");
  const [editForm, setEditForm] = useState(() => buildEditForm(null));
  const [disableReason, setDisableReason] = useState("No longer suitable for current question bank.");
  const [importFile, setImportFile] = useState(null);
  const [importResult, setImportResult] = useState(null);
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [isBulkPublishModalOpen, setIsBulkPublishModalOpen] = useState(false);

  useEffect(() => {
    let ignore = false;

    async function loadCourses() {
      if (!isAuthenticated) {
        return;
      }

      try {
        const response = await getCourses({ page: 1, limit: 100 });
        const nextCourses = response?.items || [];

        if (!ignore) {
          setCourses(nextCourses);
        }
      } catch (error) {
        if (!ignore) {
          showToast("error", error.message || "Unable to load courses.");
        }
      }
    }

    loadCourses();

    return () => {
      ignore = true;
    };
  }, [isAuthenticated]);

  useEffect(() => {
    let ignore = false;

    async function loadQuestions() {
      if (!isAuthenticated) {
        return;
      }

      setIsLoading(true);

      try {
        const response = await getQuestions({
          ...filters,
          page: pagination.page,
          limit: pagination.limit
        });

        if (ignore) {
          return;
        }

        const nextItems = extractQuestionItems(response);

        setQuestions(nextItems);
        setPagination((current) => ({
          ...current,
          total: extractQuestionTotal(response, nextItems),
          totalPages: response?.totalPages || 1
        }));

        if (nextItems.length && !selectedQuestionId) {
          const firstQuestion = nextItems[0];
          setSelectedQuestionId(firstQuestion.id);
          setEditForm(buildEditForm(firstQuestion));
        }

        if (selectedQuestionId) {
          const refreshedQuestion = nextItems.find((item) => item.id === selectedQuestionId);
          if (refreshedQuestion) {
            setEditForm((current) =>
              current.title || current.description || current.topicTags
                ? current
                : buildEditForm(refreshedQuestion)
            );
          }
        }
      } catch (error) {
        if (!ignore) {
          setQuestions([]);
          showToast("error", error.message || "Unable to load questions.");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadQuestions();

    return () => {
      ignore = true;
    };
  }, [filters, isAuthenticated, pagination.limit, pagination.page, selectedQuestionId]);

  useEffect(() => {
    let ignore = false;

    async function loadQuestionDetail() {
      if (!isAuthenticated || !selectedQuestionId) {
        if (!ignore) {
          setSelectedQuestionDetail(null);
          setEditForm(buildEditForm(null));
        }
        return;
      }

      setIsDetailLoading(true);

      try {
        const detail = await getQuestionDetail(selectedQuestionId);
        if (ignore) {
          return;
        }

        setSelectedQuestionDetail(detail);
        setEditForm(buildEditForm(detail));
      } catch (error) {
        if (!ignore) {
          setSelectedQuestionDetail(null);
          showToast("error", error.message || "Unable to load question detail.");
        }
      } finally {
        if (!ignore) {
          setIsDetailLoading(false);
        }
      }
    }

    loadQuestionDetail();

    return () => {
      ignore = true;
    };
  }, [isAuthenticated, selectedQuestionId]);

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  function showToast(type, message) {
    setToast({ type, message });
  }

  function handleSelectQuestion(question) {
    setSelectedQuestionId(question.id);
  }

  async function refreshQuestions() {
    const response = await getQuestions({
      ...filters,
      page: pagination.page,
      limit: pagination.limit
    });

    const nextItems = extractQuestionItems(response);

    setQuestions(nextItems);
    setPagination((current) => ({
      ...current,
      total: extractQuestionTotal(response, nextItems),
      totalPages: response?.totalPages || 1
    }));

    if (selectedQuestionId) {
      const refreshedQuestion = nextItems.find((item) => item.id === selectedQuestionId);
      if (refreshedQuestion) {
        const detail = await getQuestionDetail(refreshedQuestion.id);
        setSelectedQuestionDetail(detail);
        setEditForm(buildEditForm(detail));
      }
    }
  }

  const selectedQuestion = useMemo(
    () => selectedQuestionDetail || questions.find((item) => item.id === selectedQuestionId) || null,
    [questions, selectedQuestionDetail, selectedQuestionId]
  );

  async function handleSaveQuestion(event) {
    event.preventDefault();

    if (!selectedQuestion) {
      return;
    }

    setIsSubmitting(true);

    try {
      const payload = {
        title: editForm.title.trim(),
        description: editForm.description.trim(),
        difficulty: editForm.difficulty,
        topicTags: editForm.topicTags
          .split(",")
          .map((item) => item.trim())
          .filter(Boolean)
      };

      if (selectedQuestion.type === "FE") {
        payload.options = editForm.optionsText
          .split("\n")
          .map((item) => item.trim())
          .filter(Boolean);
        payload.correctAnswer = editForm.correctAnswerText
          .split(",")
          .map((item) => item.trim())
          .filter(Boolean);
      }

      if (selectedQuestion.type === "PE") {
        payload.skeletonCode = parseJsonArrayField(editForm.skeletonCodeText, "Skeleton code");
        payload.testCases = parseJsonArrayField(editForm.testCasesText, "Test cases");
      }

      await updateQuestion(selectedQuestion.id, payload);
      showToast("success", "Question updated successfully.");
      await refreshQuestions();
    } catch (error) {
      showToast("error", error.message || "Unable to update question.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleToggleQuestion() {
    if (!selectedQuestion) {
      return;
    }

    if (!disableReason.trim()) {
      showToast("error", "Disable reason is required by BE.");
      return;
    }

    setIsSubmitting(true);

    try {
      const updated = await toggleQuestionDisabled(selectedQuestion.id, disableReason.trim());
      const nextStatus = updated?.question?.status || (selectedQuestion.status === "Disabled" ? "Active" : "Disabled");
      showToast(nextStatus === "Disabled" ? "error" : "success", nextStatus);
      await refreshQuestions();
    } catch (error) {
      showToast("error", error.message || "Unable to change question status.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handlePublishQuestion() {
    if (!selectedQuestion) {
      return;
    }

    setIsSubmitting(true);

    try {
      await publishQuestion(selectedQuestion.id);
      showToast("success", "Question activated successfully.");
      await refreshQuestions();
    } catch (error) {
      showToast("error", error.message || "Unable to activate question.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeleteQuestion() {
    if (!selectedQuestion?.id) {
      return;
    }

    setIsSubmitting(true);

    try {
      await deleteQuestion(selectedQuestion.id);
      showToast("success", "Question deleted successfully.");

      const response = await getQuestions({
        ...filters,
        page: pagination.page,
        limit: pagination.limit
      });

      const nextItems = extractQuestionItems(response);
      setQuestions(nextItems);
      setPagination((current) => ({
        ...current,
        total: extractQuestionTotal(response, nextItems),
        totalPages: response?.totalPages || 1
      }));

      const nextSelectedId = nextItems[0]?.id || "";
      setSelectedQuestionId(nextSelectedId);
      if (!nextSelectedId) {
        setSelectedQuestionDetail(null);
        setEditForm(buildEditForm(null));
      }
    } catch (error) {
      showToast("error", error.message || "Unable to delete question.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleImportQuestions(event) {
    event.preventDefault();

    if (!importFile) {
      showToast("error", "Please select an .xlsx file first.");
      return;
    }

    setIsSubmitting(true);

    try {
      const result = await importQuestionsExcel(importFile);
      setImportResult(result);
      setImportFile(null);
      setIsImportModalOpen(false);
      const fileInput = document.getElementById("admin-question-import");
      if (fileInput) {
        fileInput.value = "";
      }
      if ((result?.successCount || 0) > 0 && (result?.failCount || 0) === 0) {
        showToast("success", `Imported ${result.successCount} question(s) successfully.`);
      } else if ((result?.successCount || 0) > 0 && (result?.failCount || 0) > 0) {
        showToast("error", `Imported ${result.successCount} question(s), but ${result.failCount} row(s) failed.`);
      } else {
        showToast("error", `No questions were imported. ${result?.failCount || 0} row(s) failed.`);
      }
      await refreshQuestions();
    } catch (error) {
      showToast("error", error.message || "Unable to import questions.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handlePublishAllQuestions() {
    setIsSubmitting(true);

    try {
      const result = await publishAllQuestions(filters);
      showToast("success", `Activated ${result?.updatedCount ?? 0} draft question(s).`);
      setIsBulkPublishModalOpen(false);
      await refreshQuestions();
    } catch (error) {
      showToast("error", error.message || "Unable to activate draft questions.");
    } finally {
      setIsSubmitting(false);
    }
  }

  function handleDownloadImportTemplate() {
    (async () => {
      try {
        const baseUrl = (import.meta.env.VITE_API_BASE_URL || "http://localhost:5292").replace(/\/+$/, "");
        const token = getAccessToken();

        const response = await fetch(`${baseUrl}/api/admin/questions/import-template`, {
          method: "GET",
          headers: token ? { Authorization: `Bearer ${token}` } : {}
        });

        if (!response.ok) {
          throw new Error("Unable to download the Excel template.");
        }

        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = "fe_questions_import_template.xlsx";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      } catch (error) {
        showToast("error", error?.message || "Unable to download the Excel template.");
      }
    })();
  }

  const selectedCourse = useMemo(
    () => (filters.courseId === "all" ? null : courses.find((course) => course.id === filters.courseId) || null),
    [courses, filters.courseId]
  );

  return (
    <AdminScaffold
      activeRoute={ROUTES.questionManagement}
      heroIcon="edit"
      heroTitle="Question"
      heroSubtitle="Question bank control"
      title="Question Bank"
      subtitle="Manage question inventory, import FE question sheets and update status from one admin screen."
    >
      <div className="admin-question-toolbar">
        <Button type="button" variant="primary" disabled={isSubmitting} onClick={() => setIsImportModalOpen(true)}>
          Import FE Questions
        </Button>
        <Button
          type="button"
          variant="secondary"
          disabled={isSubmitting}
          onClick={() => setIsBulkPublishModalOpen(true)}
        >
          Activate All Draft
        </Button>
      </div>

      <section className="admin-filter-panel">
        <div className="admin-filter-group">
          <label className="admin-select-field">
            <span>Course:</span>
            <select
              value={filters.courseId}
              onChange={(event) => {
                setFilters((current) => ({ ...current, courseId: event.target.value }));
                setPagination((current) => ({ ...current, page: 1 }));
              }}
            >
              <option value="all">All</option>
              {courses.map((course) => (
                <option key={course.id} value={course.id}>
                  {formatCourseLabel(course)}
                </option>
              ))}
            </select>
          </label>

          <label className="admin-select-field">
            <span>Type:</span>
            <select
              value={filters.type}
              onChange={(event) => {
                setFilters((current) => ({ ...current, type: event.target.value }));
                setPagination((current) => ({ ...current, page: 1 }));
              }}
            >
              <option value="all">All</option>
              <option value="FE">FE</option>
              <option value="PE">PE</option>
            </select>
          </label>

          <label className="admin-select-field">
            <span>Difficulty:</span>
            <select
              value={filters.difficulty}
              onChange={(event) => {
                setFilters((current) => ({ ...current, difficulty: event.target.value }));
                setPagination((current) => ({ ...current, page: 1 }));
              }}
            >
              <option value="all">All</option>
              <option value="Easy">Easy</option>
              <option value="Medium">Medium</option>
              <option value="Hard">Hard</option>
            </select>
          </label>

          <label className="admin-select-field">
            <span>Status:</span>
            <select
              value={filters.status}
              onChange={(event) => {
                setFilters((current) => ({ ...current, status: event.target.value }));
                setPagination((current) => ({ ...current, page: 1 }));
              }}
            >
              <option value="all">All</option>
              <option value="Active">Active</option>
              <option value="Draft">Draft</option>
              <option value="Disabled">Disabled</option>
            </select>
          </label>
        </div>
      </section>

      <section className="admin-split-layout">
        <section className="admin-table-panel">
          <div className="admin-table admin-table--questions admin-table--native-layout admin-table--native-questions">
            <table className="admin-table__native">
              <colgroup>
                <col className="admin-table__col admin-table__col--question-title" />
                <col className="admin-table__col admin-table__col--question-type" />
                <col className="admin-table__col admin-table__col--question-difficulty" />
                <col className="admin-table__col admin-table__col--question-status" />
                <col className="admin-table__col admin-table__col--question-updated" />
              </colgroup>
              <thead>
                <tr className="admin-table__head">
                  <th scope="col">Question</th>
                  <th scope="col">Type</th>
                  <th scope="col">Difficulty</th>
                  <th scope="col">Status</th>
                  <th scope="col">Updated</th>
                </tr>
              </thead>
              <tbody className="admin-table__body">
                {questions.map((question) => (
                  <tr
                    key={question.id}
                    className={`admin-course-row admin-course-row--questions${selectedQuestion?.id === question.id ? " is-selected" : ""}`}
                    tabIndex={0}
                    onClick={() => handleSelectQuestion(question)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        handleSelectQuestion(question);
                      }
                    }}
                  >
                    <td><div>
                      <strong className="admin-course-row__name">{question.title}</strong>
                      <p className="admin-course-row__description">
                        {question.topicTags?.length ? question.topicTags.join(", ") : "No topic tags"}
                      </p>
                    </div></td>
                    <td><div className="admin-pill">{question.type}</div></td>
                    <td>{question.difficulty}</td>
                    <td><div className={`admin-pill ${question.status === "Disabled" ? "admin-pill--danger" : "admin-pill--success"}`}>
                      {question.status}
                    </div></td>
                    <td>{formatDate(question.lastModifiedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!questions.length ? (
              <div className="admin-table__body">
                <div className="admin-empty-state">
                  {isLoading ? "Loading questions..." : "No questions found for the selected filters."}
                </div>
              </div>
            ) : null}
          </div>

          <div className="admin-table-panel__footer">
            <p>{isLoading ? "Syncing question bank..." : `Showing ${questions.length} of ${pagination.total} questions`}</p>
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
                onClick={() =>
                  setPagination((current) => ({
                    ...current,
                    page: Math.min(pagination.totalPages || 1, current.page + 1)
                  }))
                }
              >
                &#8250;
              </button>
            </div>
          </div>
        </section>

        <aside className="admin-detail-panel">
          <div className="admin-detail-panel__header">
            <h2>Question Detail</h2>
            <p>Edit summary fields, import FE sheets and toggle question availability.</p>
          </div>

          <div className="admin-detail-panel__body">
            {importResult ? (
              <div className="admin-question-import-result">
                <div className="detail-tile">
                  <div className="detail-tile__icon">
                    <AdminGlyph kind="spark" />
                  </div>
                  <div>
                    <p>Imported Rows</p>
                    <strong>
                      {importResult.successCount || 0}/{importResult.totalRows || 0}
                    </strong>
                  </div>
                </div>
                <p className="admin-question-import-meta">
                  Failed: {importResult.failCount || 0}
                </p>
                {importResult.rowErrors?.length ? (
                  <div className="admin-question-import-errors">
                    {importResult.rowErrors.slice(0, 5).map((item, index) => (
                      <p key={`${item.row}-${index}`}>
                        Row {item.row}: {item.error}
                      </p>
                    ))}
                  </div>
                ) : null}
              </div>
            ) : null}

            {selectedQuestion ? (
              <>
                {isDetailLoading ? (
                  <div className="admin-empty-state">Loading question detail...</div>
                ) : (
                  <>
                    <form className="admin-inline-form" onSubmit={handleSaveQuestion}>
                      <label className="admin-modal__field admin-modal__field--full">
                        <span>Title</span>
                        <input
                          type="text"
                          value={editForm.title}
                          onChange={(event) =>
                            setEditForm((current) => ({ ...current, title: event.target.value }))
                          }
                        />
                      </label>

                      <label className="admin-modal__field admin-modal__field--full">
                        <span>Description</span>
                        <textarea
                          value={editForm.description}
                          onChange={(event) =>
                            setEditForm((current) => ({ ...current, description: event.target.value }))
                          }
                          placeholder="Question description from BE can be updated here."
                        />
                      </label>

                      <div className="admin-detail-grid">
                        <label className="admin-modal__field">
                          <span>Difficulty</span>
                          <select
                            value={editForm.difficulty}
                            onChange={(event) =>
                              setEditForm((current) => ({ ...current, difficulty: event.target.value }))
                            }
                          >
                            <option value="Easy">Easy</option>
                            <option value="Medium">Medium</option>
                            <option value="Hard">Hard</option>
                          </select>
                        </label>

                      <label className="admin-modal__field">
                        <span>Status</span>
                        <input type="text" value={selectedQuestion.status || ""} readOnly />
                      </label>
                      </div>

                      <label className="admin-modal__field admin-modal__field--full">
                        <span>Topic Tags</span>
                        <input
                          type="text"
                          value={editForm.topicTags}
                          onChange={(event) =>
                            setEditForm((current) => ({ ...current, topicTags: event.target.value }))
                          }
                          placeholder="Arrays, Sorting, Complexity"
                        />
                      </label>

                      {selectedQuestion.type === "FE" ? (
                        <>
                          <label className="admin-modal__field admin-modal__field--full">
                            <span>Options</span>
                            <textarea
                              value={editForm.optionsText}
                              onChange={(event) =>
                                setEditForm((current) => ({ ...current, optionsText: event.target.value }))
                              }
                              placeholder={"A. ...\nB. ...\nC. ...\nD. ..."}
                            />
                          </label>

                          <label className="admin-modal__field admin-modal__field--full">
                            <span>Correct Answer</span>
                            <input
                              type="text"
                              value={editForm.correctAnswerText}
                              onChange={(event) =>
                                setEditForm((current) => ({ ...current, correctAnswerText: event.target.value }))
                              }
                              placeholder="A"
                            />
                          </label>

                          <label className="admin-modal__field admin-modal__field--full">
                            <span>Explanation</span>
                            <textarea value={selectedQuestion.explanation || ""} readOnly placeholder="No explanation." />
                          </label>
                        </>
                      ) : null}

                      {selectedQuestion.type === "PE" ? (
                        <>
                          <label className="admin-modal__field admin-modal__field--full">
                            <span>Skeleton Code (JSON array)</span>
                            <textarea
                              className="admin-code-block"
                              value={editForm.skeletonCodeText}
                              onChange={(event) =>
                                setEditForm((current) => ({ ...current, skeletonCodeText: event.target.value }))
                              }
                            />
                          </label>

                          <label className="admin-modal__field admin-modal__field--full">
                            <span>Test Cases (JSON array)</span>
                            <textarea
                              className="admin-code-block"
                              value={editForm.testCasesText}
                              onChange={(event) =>
                                setEditForm((current) => ({ ...current, testCasesText: event.target.value }))
                              }
                            />
                          </label>

                          <label className="admin-modal__field admin-modal__field--full">
                            <span>Solution Code</span>
                            <textarea
                              className="admin-code-block"
                              value={Array.isArray(selectedQuestion.solutionCode) ? JSON.stringify(selectedQuestion.solutionCode, null, 2) : "[]"}
                              readOnly
                            />
                          </label>
                        </>
                      ) : null}

                      <div className="admin-modal__actions">
                        <Button type="submit" variant="secondary" disabled={isSubmitting}>
                          Save Changes
                        </Button>
                        {selectedQuestion.status === "Draft" ? (
                          <Button type="button" variant="primary" disabled={isSubmitting} onClick={handlePublishQuestion}>
                            Active Question
                          </Button>
                        ) : null}
                        <Button type="button" variant="ghost" disabled={isSubmitting} onClick={handleDeleteQuestion}>
                          Delete
                        </Button>
                      </div>
                    </form>

                    <div className="admin-inline-form">
                      <label className="admin-modal__field admin-modal__field--full">
                        <span>Disable Reason</span>
                        <textarea
                          value={disableReason}
                          onChange={(event) => setDisableReason(event.target.value)}
                          placeholder="Reason required by /disable endpoint"
                        />
                      </label>

                      <div className="admin-modal__actions">
                        <Button type="button" variant="primary" disabled={isSubmitting} onClick={handleToggleQuestion}>
                          {selectedQuestion.status === "Disabled" ? "Enable Question" : "Disable Question"}
                        </Button>
                      </div>
                    </div>
                  </>
                )}
              </>
            ) : (
              <div className="admin-empty-state">Select a question to edit or change its status.</div>
            )}
          </div>
        </aside>
      </section>

      {isImportModalOpen ? (
        <div className="admin-modal-backdrop" role="presentation" onClick={() => !isSubmitting && setIsImportModalOpen(false)}>
          <div
            className="admin-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="admin-question-import-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="admin-modal__header">
              <div>
                <h2 id="admin-question-import-title">Import FE Questions</h2>
                <p>Upload an `.xlsx` file to add frontend questions into the admin bank.</p>
              </div>
              <button
                type="button"
                className="admin-modal__close"
                aria-label="Close import dialog"
                onClick={() => !isSubmitting && setIsImportModalOpen(false)}
              >
                ×
              </button>
            </div>

            <form className="admin-modal__form admin-modal__form--single" onSubmit={handleImportQuestions}>
              <label className="admin-modal__field admin-modal__field--full">
                <span>Excel File (.xlsx)</span>
                <div className="admin-file-field__row">
                  <input
                    id="admin-question-import"
                    type="file"
                    accept=".xlsx"
                    onChange={(event) => setImportFile(event.target.files?.[0] || null)}
                  />
                  <Button type="button" variant="secondary" disabled={isSubmitting} onClick={handleDownloadImportTemplate}>
                    Download Template
                  </Button>
                </div>
              </label>

              <div className="admin-modal__actions">
                <Button type="button" variant="ghost" disabled={isSubmitting} onClick={() => setIsImportModalOpen(false)}>
                  Cancel
                </Button>
                <Button type="submit" variant="primary" disabled={isSubmitting}>
                  Import Excel
                </Button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      <ConfirmModal
        open={isBulkPublishModalOpen}
        title="Activate all draft questions"
        message="Activate all draft admin questions that match the current filters? Deleted questions will remain unchanged."
        confirmLabel="Activate All"
        isConfirming={isSubmitting}
        onCancel={() => !isSubmitting && setIsBulkPublishModalOpen(false)}
        onConfirm={handlePublishAllQuestions}
      />

      <ToastNotification toast={toast} />
    </AdminScaffold>
  );
}
