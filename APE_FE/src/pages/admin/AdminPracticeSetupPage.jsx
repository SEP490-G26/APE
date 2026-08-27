import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { ROUTES } from "../../lib/routes";
import { getCourses } from "../../services/courseService";
import {
  createAdminPracticeSetup,
  deleteAdminPracticeSetup,
  getAdminPracticeSetupQuestionPool,
  getAdminPracticeSetups
} from "../../services/adminPracticeSetupService";
import { ConfirmModal, ToastNotification } from "../../shared/ui";

const EXAM_TYPES = ["FE", "PE"];
const FE_MODES = ["Practice", "Flashcard"];
const DIFFICULTIES = ["Easy", "Medium", "Hard"];

function formatCourseLabel(course) {
  return course?.code ? `${course.code} - ${course.name}` : course?.name || "Untitled course";
}

function normalizeDifficulty(value) {
  const normalized = String(value || "").trim().toLowerCase();
  if (normalized === "easy") return "Easy";
  if (normalized === "medium") return "Medium";
  if (normalized === "hard") return "Hard";
  return String(value || "").trim();
}

function toggleValue(currentValues, value) {
  return currentValues.includes(value)
    ? currentValues.filter((item) => item !== value)
    : [...currentValues, value];
}

function sumDifficultyCounts(counts) {
  return DIFFICULTIES.reduce((sum, difficulty) => sum + Number(counts[difficulty] || 0), 0);
}

function buildFeDifficultyPayload(counts) {
  return DIFFICULTIES
    .map((difficulty) => ({ difficulty, count: Number(counts[difficulty] || 0) }))
    .filter((item) => item.count > 0);
}

function buildDifficultyCountsFromQuestions(questions) {
  return (questions || []).reduce((result, question) => {
    const difficulty = normalizeDifficulty(question?.difficulty);
    if (DIFFICULTIES.includes(difficulty)) {
      result[difficulty] += 1;
    }
    return result;
  }, { Easy: 0, Medium: 0, Hard: 0 });
}

function buildDifficultyInputState(counts) {
  return DIFFICULTIES.reduce((result, difficulty) => {
    result[difficulty] = String(Number(counts?.[difficulty] || 0));
    return result;
  }, {});
}

function buildTopicTagsFromQuestions(questions) {
  const tags = new Set();
  (questions || []).forEach((question) => {
    (question?.topicTags || []).forEach((tag) => {
      if (tag) {
        tags.add(tag);
      }
    });
  });
  return Array.from(tags);
}

function formatDateTime(value) {
  if (!value) {
    return "Not updated";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return date.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

export function AdminPracticeSetupPage({ isAuthenticated = false }) {
  const [courses, setCourses] = useState([]);
  const [selectedSetupId, setSelectedSetupId] = useState("");
  const [title, setTitle] = useState("");
  const [presetScope, setPresetScope] = useState("Course");
  const [selectedCourseId, setSelectedCourseId] = useState("");
  const [examType, setExamType] = useState("FE");
  const [feMode, setFeMode] = useState("Practice");
  const [selectedTopicTags, setSelectedTopicTags] = useState([]);
  const [selectedPeDifficulties, setSelectedPeDifficulties] = useState([]);
  const [feDifficultyCounts, setFeDifficultyCounts] = useState({ Easy: 0, Medium: 0, Hard: 0 });
  const [feDifficultyInputs, setFeDifficultyInputs] = useState(() => buildDifficultyInputState({ Easy: 0, Medium: 0, Hard: 0 }));
  const [feDifficultyOverflow, setFeDifficultyOverflow] = useState({ Easy: false, Medium: false, Hard: false });
  const [selectedPeQuestionIds, setSelectedPeQuestionIds] = useState([]);
  const [pool, setPool] = useState({ availableCount: 0, items: [] });
  const [setups, setSetups] = useState([]);
  const [listFilters, setListFilters] = useState({ courseId: "all", examType: "all", mode: "all" });
  const [pagination, setPagination] = useState({ page: 1, limit: 8, total: 0, totalPages: 1 });
  const [isLoadingCourses, setIsLoadingCourses] = useState(false);
  const [isLoadingPool, setIsLoadingPool] = useState(false);
  const [isLoadingSetups, setIsLoadingSetups] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [setupPendingDelete, setSetupPendingDelete] = useState(null);
  const [deletingSetupId, setDeletingSetupId] = useState("");
  const [toast, setToast] = useState(null);

  useEffect(() => {
    let ignore = false;

    async function loadCourses() {
      if (!isAuthenticated) {
        return;
      }

      setIsLoadingCourses(true);
      try {
        const response = await getCourses({ page: 1, limit: 200, status: "Active" });
        const nextCourses = Array.isArray(response?.items) ? response.items.filter((course) => course.isActive) : [];
        if (ignore) {
          return;
        }

        setCourses(nextCourses);
        setSelectedCourseId((current) => current || nextCourses[0]?.id || "");
      } catch (error) {
        if (!ignore) {
          setToast({ type: "error", message: error.message || "Unable to load the course list." });
        }
      } finally {
        if (!ignore) {
          setIsLoadingCourses(false);
        }
      }
    }

    loadCourses();
    return () => {
      ignore = true;
    };
  }, [isAuthenticated]);

  useEffect(() => {
    setFeDifficultyInputs(buildDifficultyInputState(feDifficultyCounts));
  }, [feDifficultyCounts]);

  useEffect(() => {
    let ignore = false;

    async function loadPool() {
      if (!selectedCourseId) {
        setPool({ availableCount: 0, items: [] });
        return;
      }

      setIsLoadingPool(true);
      try {
        const nextPool = await getAdminPracticeSetupQuestionPool({ courseId: selectedCourseId, examType });
        if (ignore) {
          return;
        }

        setPool(nextPool);
        setSelectedPeQuestionIds((current) => current.filter((id) => nextPool.items.some((item) => item.id === id)));
      } catch (error) {
        if (!ignore) {
          setPool({ availableCount: 0, items: [] });
          setSelectedPeQuestionIds([]);
          setToast({ type: "error", message: error.message || "Unable to load the question pool." });
        }
      } finally {
        if (!ignore) {
          setIsLoadingPool(false);
        }
      }
    }

    loadPool();
    return () => {
      ignore = true;
    };
  }, [selectedCourseId, examType]);

  useEffect(() => {
    let ignore = false;

    async function loadSetups() {
      if (!isAuthenticated) {
        return;
      }

      setIsLoadingSetups(true);
      try {
        const response = await getAdminPracticeSetups({
          ...listFilters,
          page: pagination.page,
          limit: pagination.limit
        });

        if (ignore) {
          return;
        }

        setSetups(response?.items || []);
        setPagination((current) => ({
          ...current,
          total: Number(response?.total || 0),
          totalPages: Number(response?.totalPages || 1)
        }));
      } catch (error) {
        if (!ignore) {
          setSetups([]);
          setToast({ type: "error", message: error.message || "Unable to load practice setups." });
        }
      } finally {
        if (!ignore) {
          setIsLoadingSetups(false);
        }
      }
    }

    loadSetups();
    return () => {
      ignore = true;
    };
  }, [isAuthenticated, listFilters, pagination.limit, pagination.page]);

  useEffect(() => {
    const allowedTags = new Set(pool.items.flatMap((item) => item.topicTags || []));
    setSelectedTopicTags((current) => current.filter((tag) => allowedTags.has(tag)));
  }, [pool.items]);

  useEffect(() => {
    if (examType !== "FE") {
      setFeMode("Practice");
      setFeDifficultyCounts({ Easy: 0, Medium: 0, Hard: 0 });
    }
  }, [examType]);

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  const selectedCourse = useMemo(
    () => courses.find((course) => course.id === selectedCourseId) || null,
    [courses, selectedCourseId]
  );

  const topicTagOptions = useMemo(() => {
    const values = new Set();
    pool.items.forEach((item) => {
      (item.topicTags || []).forEach((tag) => {
        if (tag) {
          values.add(tag);
        }
      });
    });
    return Array.from(values).sort((left, right) => left.localeCompare(right, "vi"));
  }, [pool.items]);

  const topicFilteredItems = useMemo(() => {
    if (!selectedTopicTags.length) {
      return pool.items;
    }

    return pool.items.filter((item) => (item.topicTags || []).some((tag) => selectedTopicTags.includes(tag)));
  }, [pool.items, selectedTopicTags]);

  const peFilteredItems = useMemo(() => {
    if (!selectedPeDifficulties.length) {
      return topicFilteredItems;
    }

    return topicFilteredItems.filter((item) => selectedPeDifficulties.includes(normalizeDifficulty(item.difficulty)));
  }, [selectedPeDifficulties, topicFilteredItems]);

  const feDifficultyAvailability = useMemo(() => {
    return DIFFICULTIES.reduce((result, difficulty) => {
      result[difficulty] = topicFilteredItems.filter((item) => normalizeDifficulty(item.difficulty) === difficulty).length;
      return result;
    }, {});
  }, [topicFilteredItems]);

  useEffect(() => {
    if (examType !== "PE") {
      setSelectedPeDifficulties([]);
      setSelectedPeQuestionIds([]);
      return;
    }

    const allowedIds = new Set(peFilteredItems.map((item) => item.id));
    setSelectedPeQuestionIds((current) => current.filter((id) => allowedIds.has(id)));
  }, [examType, peFilteredItems]);

  useEffect(() => {
    if (examType !== "FE") {
      return;
    }

    setFeDifficultyCounts((current) => {
      const next = { ...current };
      let changed = false;

      DIFFICULTIES.forEach((difficulty) => {
        const maxValue = Math.min(50, Number(feDifficultyAvailability[difficulty] || 0));
        const safeValue = Math.max(0, Math.min(maxValue, Number(current[difficulty] || 0)));
        if (safeValue !== current[difficulty]) {
          next[difficulty] = safeValue;
          changed = true;
        }
      });

      return changed ? next : current;
    });
  }, [examType, feDifficultyAvailability]);

  const totalFeCount = sumDifficultyCounts(feDifficultyCounts);
  const totalQuestionCount = examType === "FE" ? totalFeCount : selectedPeQuestionIds.length;

  function resetForm(nextCourseId = selectedCourseId) {
    setSelectedSetupId("");
    setTitle("");
    setPresetScope("Course");
    setSelectedCourseId(nextCourseId || courses[0]?.id || "");
    setExamType("FE");
    setFeMode("Practice");
    setSelectedTopicTags([]);
    setSelectedPeDifficulties([]);
    setFeDifficultyCounts({ Easy: 0, Medium: 0, Hard: 0 });
    setSelectedPeQuestionIds([]);
  }

  function toggleTopicTag(tag) {
    setSelectedTopicTags((current) => toggleValue(current, tag));
  }

  function togglePeDifficulty(difficulty) {
    setSelectedPeDifficulties((current) => toggleValue(current, difficulty));
  }

  function togglePeQuestion(questionId) {
    setSelectedPeQuestionIds((current) => (
      current.includes(questionId)
        ? current.filter((item) => item !== questionId)
        : current.length >= 5
          ? current
          : [...current, questionId]
    ));
  }

  function updateFeDifficultyCount(difficulty, value) {
    const normalizedValue = String(value ?? "").replace(/\D/g, "");
    const maxValue = Math.min(50, Number(feDifficultyAvailability[difficulty] || 0));
    const parsedValue = normalizedValue === "" ? 0 : Number(normalizedValue);
    const safeValue = Number.isFinite(parsedValue)
      ? Math.max(0, Math.min(maxValue, Math.floor(parsedValue)))
      : 0;
    const exceededMax = normalizedValue !== "" && parsedValue > maxValue;

    setFeDifficultyInputs((current) => ({
      ...current,
      [difficulty]: String(safeValue)
    }));
    setFeDifficultyOverflow((current) => ({
      ...current,
      [difficulty]: exceededMax
    }));
    setFeDifficultyCounts((current) => ({
      ...current,
      [difficulty]: safeValue
    }));
  }

  function resetFeDifficultyCount(difficulty) {
    setFeDifficultyInputs((current) => ({
      ...current,
      [difficulty]: "0"
    }));
    setFeDifficultyOverflow((current) => ({
      ...current,
      [difficulty]: false
    }));
    setFeDifficultyCounts((current) => ({
      ...current,
      [difficulty]: 0
    }));
  }

  function handleSelectSetup(setupId) {
    setSelectedSetupId(setupId);
  }

  async function handleSave() {
    if (!selectedCourseId) {
      setToast({ type: "error", message: "Please choose a course." });
      return;
    }

    if (!title.trim()) {
      setToast({ type: "error", message: "Please enter a setup title." });
      return;
    }

    if (examType === "FE") {
      if (totalFeCount <= 0) {
        setToast({ type: "error", message: "An FE setup cannot be saved without any questions." });
        return;
      }

      if (totalFeCount > 50) {
        setToast({ type: "error", message: "FE question count cannot exceed 50." });
        return;
      }
    } else {
      if (!selectedPeQuestionIds.length) {
        setToast({ type: "error", message: "A PE setup cannot be saved without any questions." });
        return;
      }

      if (selectedPeQuestionIds.length > 5) {
        setToast({ type: "error", message: "PE question count cannot exceed 5." });
        return;
      }
    }

    const payload = {
      title: title.trim(),
      presetScope,
      courseId: selectedCourseId,
      examType,
      mode: examType === "FE" ? feMode : "Practice",
      topicTags: selectedTopicTags,
      difficulties: examType === "FE"
        ? buildFeDifficultyPayload(feDifficultyCounts).map((item) => item.difficulty)
        : selectedPeDifficulties,
      feDifficultyCounts: examType === "FE" ? buildFeDifficultyPayload(feDifficultyCounts) : [],
      peQuestionIds: examType === "PE" ? selectedPeQuestionIds : []
    };

    setIsSubmitting(true);
    try {
      await createAdminPracticeSetup(payload);

      setToast({ type: "success", message: "Setup created successfully." });
      resetForm(selectedCourseId);

      const response = await getAdminPracticeSetups({
        ...listFilters,
        page: pagination.page,
        limit: pagination.limit
      });
      setSetups(response?.items || []);
      setPagination((current) => ({
        ...current,
        total: Number(response?.total || 0),
        totalPages: Number(response?.totalPages || 1)
      }));
    } catch (error) {
      setToast({ type: "error", message: error.message || "Unable to save the setup." });
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeleteSetup() {
    if (!setupPendingDelete?.id) {
      setSetupPendingDelete(null);
      return;
    }

    setDeletingSetupId(setupPendingDelete.id);
    try {
      await deleteAdminPracticeSetup(setupPendingDelete.id);
      setToast({ type: "success", message: "Setup deleted." });

      setSetups((current) => current.filter((item) => item.id !== setupPendingDelete.id));
      setPagination((current) => ({
        ...current,
        total: Math.max(0, Number(current.total || 0) - 1)
      }));
      if (selectedSetupId === setupPendingDelete.id) {
        setSelectedSetupId("");
      }
    } catch (error) {
      setToast({ type: "error", message: error.message || "Unable to delete the setup." });
    } finally {
      setDeletingSetupId("");
      setSetupPendingDelete(null);
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminPracticeSetup}
      heroIcon="spark"
      heroTitle="Practice Setup"
      heroSubtitle="Public student presets"
      title="Student Practice Setup"
      subtitle="Configure public student presets by course, exam type, and FE/PE mode."
    >
      <section className="student-exam-hero-strip admin-practice-setup-hero">
        <div className="student-exam-hero-strip__card">
          <span>Source</span>
          <strong>System only</strong>
        </div>
        <div className="student-exam-hero-strip__card">
          <span>Course</span>
          <strong>{selectedCourse ? formatCourseLabel(selectedCourse) : "Choose course"}</strong>
        </div>
        <div className="student-exam-hero-strip__card">
          <span>Type</span>
          <strong>{examType}{examType === "FE" ? ` / ${feMode}` : ""}</strong>
        </div>
        <div className="student-exam-hero-strip__card">
          <span>Question count</span>
          <strong>{totalQuestionCount}</strong>
        </div>
      </section>

      <section className="student-exam-config admin-practice-setup-page">
        <div className="student-exam-config__primary">
          <label className="student-exam-field">
            <span>Setup title</span>
            <input
              className="student-exam-input"
              type="text"
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              placeholder="PRO192 FE Practice Course"
              disabled={isSubmitting}
            />
          </label>

          <label className="student-exam-field">
            <span>Course</span>
            <select
              value={selectedCourseId}
              onChange={(event) => setSelectedCourseId(event.target.value)}
              disabled={isLoadingCourses || !courses.length || isSubmitting}
            >
              <option value="">Choose course</option>
              {courses.map((course) => (
                <option key={course.id} value={course.id}>
                  {formatCourseLabel(course)}
                </option>
              ))}
            </select>
          </label>

          <section className="student-exam-section">
            <div className="student-exam-section__header">
              <div><h2>Exam type</h2></div>
              <span>FE uses randomized difficulty buckets, while PE lets you pick exact questions.</span>
            </div>
            <div className="student-segmented-control student-segmented-control--wide">
              {EXAM_TYPES.map((type) => (
                <button
                  key={type}
                  type="button"
                  className={examType === type ? "is-active" : ""}
                  onClick={() => setExamType(type)}
                  disabled={isSubmitting}
                >
                  {type}
                </button>
              ))}
            </div>
          </section>

          {examType === "FE" ? (
            <section className="student-exam-section">
              <div className="student-exam-section__header">
                <div><h2>Mode</h2></div>
                <span>Flashcard is available only for FE. PE always uses Practice mode.</span>
              </div>
              <div className="student-segmented-control student-segmented-control--wide">
                {FE_MODES.map((mode) => (
                  <button
                    key={mode}
                    type="button"
                    className={feMode === mode ? "is-active" : ""}
                    onClick={() => setFeMode(mode)}
                    disabled={isSubmitting}
                  >
                    {mode}
                  </button>
                ))}
              </div>
            </section>
          ) : null}

          <div className="student-exam-field">
            <span>Topic tag</span>
            <div className="student-tag-grid">
              {topicTagOptions.map((tag) => (
                <label key={tag} className="student-radio-option">
                  <input
                    type="checkbox"
                    checked={selectedTopicTags.includes(tag)}
                    onChange={() => toggleTopicTag(tag)}
                    disabled={isLoadingPool || isSubmitting}
                  />
                  <span>{tag}</span>
                </label>
              ))}
            </div>
          </div>

          {examType === "PE" ? (
            <>
              <div className="student-exam-field">
                <span>Difficulty</span>
                <div className="student-tag-grid">
                  {DIFFICULTIES.map((difficulty) => (
                    <label key={difficulty} className="student-radio-option">
                      <input
                        type="checkbox"
                        checked={selectedPeDifficulties.includes(difficulty)}
                        onChange={() => togglePeDifficulty(difficulty)}
                        disabled={isLoadingPool || isSubmitting}
                      />
                      <span>{difficulty}</span>
                    </label>
                  ))}
                </div>
              </div>

              <div className="student-exam-field">
                <span>Pick questions (max 5)</span>
                <div className="student-tag-grid">
                  {peFilteredItems.map((question) => {
                    const pickedIndex = selectedPeQuestionIds.indexOf(question.id);
                    const limitReached = !selectedPeQuestionIds.includes(question.id) && selectedPeQuestionIds.length >= 5;
                    return (
                      <label key={question.id} className="student-radio-option">
                        <input
                          type="checkbox"
                          checked={pickedIndex >= 0}
                          onChange={() => togglePeQuestion(question.id)}
                          disabled={isLoadingPool || isSubmitting || limitReached}
                        />
                        <span>
                          {pickedIndex >= 0 ? `Q${pickedIndex + 1}` : "Pick"} - {question.title} ({normalizeDifficulty(question.difficulty)})
                        </span>
                      </label>
                    );
                  })}
                </div>
              </div>
            </>
          ) : (
            <div className="student-exam-field">
              <span>Difficulty counts (max 50)</span>
              <div className="student-tag-grid">
                {DIFFICULTIES.map((difficulty) => (
                  <label key={difficulty} className="student-exam-field" style={{ minWidth: 180 }}>
                    <span>{difficulty} (max {Math.min(50, feDifficultyAvailability[difficulty] || 0)})</span>
                    <input
                      className="student-exam-input"
                      type="text"
                      inputMode="numeric"
                      pattern="[0-9]*"
                      value={feDifficultyInputs[difficulty] ?? "0"}
                      onChange={(event) => updateFeDifficultyCount(difficulty, event.target.value)}
                      onKeyDown={(event) => {
                        const currentValue = feDifficultyInputs[difficulty] ?? "0";
                        const shouldResetToZero = feDifficultyOverflow[difficulty] || currentValue.length <= 1;
                        if ((event.key === "Backspace" || event.key === "Delete") && shouldResetToZero) {
                          event.preventDefault();
                          resetFeDifficultyCount(difficulty);
                        }
                      }}
                      disabled={isLoadingPool || isSubmitting || !feDifficultyAvailability[difficulty]}
                    />
                  </label>
                ))}
              </div>
            </div>
          )}
        </div>

        <aside className="student-exam-config__secondary">
          <div className="student-exam-summary-card">
            <div className="student-exam-summary-card__header">
              <div>
                <span>Preview</span>
                <strong>{title.trim() || "Chua dat ten setup"}</strong>
              </div>
              <p>{presetScope}</p>
            </div>

            <div className="student-exam-summary-list">
              <div>
                <span>Source</span>
                <strong>System</strong>
              </div>
              <div>
                <span>Course</span>
                <strong>{selectedCourse ? formatCourseLabel(selectedCourse) : "Chua chon"}</strong>
              </div>
              <div>
                <span>Mode</span>
                <strong>{examType === "FE" ? feMode : "Practice"}</strong>
              </div>
              <div>
                <span>Pool</span>
                <strong>{examType === "FE" ? topicFilteredItems.length : peFilteredItems.length}</strong>
              </div>
              <div>
                <span>Question count</span>
                <strong>{totalQuestionCount}</strong>
              </div>
              <div>
                <span>Status</span>
                <strong>Creating new preset only</strong>
              </div>
            </div>
          </div>

          <div className="student-free-tier-panel">
            <span>Rule check</span>
            <strong>
              {examType === "FE"
                ? `${totalFeCount}/50 FE questions`
                : `${selectedPeQuestionIds.length}/5 PE questions`}
            </strong>
          </div>

          <div className="admin-practice-setup-actions">
            <Button type="button" variant="ghost" onClick={() => resetForm()} disabled={isSubmitting}>
              New Setup
            </Button>
            <Button
              type="button"
              variant="primary"
              className="student-generate-button"
              disabled={!selectedCourseId || isSubmitting || isLoadingPool}
              onClick={handleSave}
            >
              {isSubmitting ? "Saving..." : "Save Setup"}
            </Button>
          </div>
        </aside>
      </section>

      <section className="admin-filter-panel admin-practice-setup-list__filters">
        <div className="admin-filter-group">
          <label className="admin-select-field">
            <span>Course:</span>
            <select
              value={listFilters.courseId}
              onChange={(event) => {
                setListFilters((current) => ({ ...current, courseId: event.target.value }));
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
              value={listFilters.examType}
              onChange={(event) => {
                setListFilters((current) => ({ ...current, examType: event.target.value }));
                setPagination((current) => ({ ...current, page: 1 }));
              }}
            >
              <option value="all">All</option>
              <option value="FE">FE</option>
              <option value="PE">PE</option>
            </select>
          </label>

          <label className="admin-select-field">
            <span>Mode:</span>
            <select
              value={listFilters.mode}
              onChange={(event) => {
                setListFilters((current) => ({ ...current, mode: event.target.value }));
                setPagination((current) => ({ ...current, page: 1 }));
              }}
            >
              <option value="all">All</option>
              <option value="Practice">Practice</option>
              <option value="Flashcard">Flashcard</option>
            </select>
          </label>
        </div>
      </section>

      <section className="admin-table-panel admin-practice-setup-list">
        <div className="admin-detail-panel__header">
          <h2>Saved Setups</h2>
          <p>Only public system presets that students can open from the library are shown here.</p>
        </div>

        <div className="admin-table admin-table--questions admin-table--native-layout admin-table--native-questions">
          <table className="admin-table__native">
            <thead>
              <tr className="admin-table__head">
                <th scope="col">Setup</th>
                <th scope="col">Scope</th>
                <th scope="col">Type</th>
                <th scope="col">Mode</th>
                <th scope="col">Questions</th>
                <th scope="col">Created</th>
                <th scope="col">Action</th>
              </tr>
            </thead>
            <tbody className="admin-table__body">
              {setups.map((item) => {
                const course = courses.find((entry) => entry.id === item.courseId);
                const questionCount = item.examType === "FE" ? item.feQuestionCount : item.peQuestionCount;
                return (
                  <tr
                    key={item.id}
                    className={`admin-course-row admin-course-row--questions${selectedSetupId === item.id ? " is-selected" : ""}`}
                    tabIndex={0}
                    onClick={() => handleSelectSetup(item.id)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        handleSelectSetup(item.id);
                      }
                    }}
                  >
                    <td>
                      <div>
                        <strong className="admin-course-row__name">{item.title}</strong>
                        <p className="admin-course-row__description">{course ? formatCourseLabel(course) : item.courseId}</p>
                      </div>
                    </td>
                    <td><div className="admin-pill">{item.presetScope}</div></td>
                    <td><div className="admin-pill">{item.examType}</div></td>
                    <td>{item.mode}</td>
                    <td>{questionCount}</td>
                    <td>{formatDateTime(item.createdAt)}</td>
                    <td>
                      <button
                        type="button"
                        className="student-link-button"
                        onClick={(event) => {
                          event.stopPropagation();
                          setSetupPendingDelete(item);
                        }}
                        disabled={Boolean(deletingSetupId)}
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {!setups.length ? (
            <div className="admin-table__body">
              <div className="admin-empty-state">
                {isLoadingSetups ? "Loading setups..." : "No public setups match the current filters."}
              </div>
            </div>
          ) : null}
        </div>

        <div className="admin-table-panel__footer">
          <p>{isLoadingSetups ? "Syncing practice setups..." : `Showing ${setups.length} of ${pagination.total} public setups`}</p>
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

      <ConfirmModal
        open={Boolean(setupPendingDelete)}
        title="Delete practice setup"
        message={setupPendingDelete ? `Soft-delete "${setupPendingDelete.title}"? Students will no longer see it in System Exams, but past practice history will remain.` : ""}
        confirmLabel={deletingSetupId ? "Deleting..." : "Delete"}
        isConfirming={Boolean(deletingSetupId)}
        onCancel={() => !deletingSetupId && setSetupPendingDelete(null)}
        onConfirm={handleDeleteSetup}
      />
      <ToastNotification toast={toast} />
    </AdminScaffold>
  );
}
