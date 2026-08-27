import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { AppFooter } from "../../components/layout/AppFooter";
import { StudentTopbar } from "../../components/student/StudentTopbar";
import { footerLinks } from "../../data/landingContent";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getStudentCourses } from "../../services/studentExamService";
import { getExamConfigDocuments, getExamQuestionPool } from "../../services/studentExamConfigService";
import { startConfiguredExam } from "../../services/studentWorkspaceService";
import { ToastNotification } from "../../shared/ui";

const SOURCES = ["System", "BYOS"];
const EXAM_TYPES = ["FE", "PE"];
const FE_MODES = ["Practice", "Flashcard"];
const DIFFICULTIES = ["Easy", "Medium", "Hard"];

const EXAM_TYPE_META = {
  FE: "Filter by topic tags, enter Easy / Medium / Hard counts, then let the backend randomize the question set.",
  PE: "Filter by topic tags and difficulty, then pick questions in the exact order you want for the session."
};

const SOURCE_META = {
  System: "Use the built-in system question pool for a faster practice setup.",
  BYOS: "Use your uploaded personal materials that are ready in the My Documents workspace."
};

function getSourceLabel(source) {
  return source === "BYOS" ? "My Documents" : source || "";
}

function formatCourseLabel(course) {
  return course?.code ? `${course.code}: ${course.name}` : course?.name || "Select course";
}

function normalizeDifficulty(value) {
  const normalized = String(value || "").trim().toLowerCase();
  if (normalized === "easy") return "Easy";
  if (normalized === "medium") return "Medium";
  if (normalized === "hard") return "Hard";
  return String(value || "").trim();
}

function getRecentSessions() {
  const stored = window.localStorage.getItem("ape.student.recent-generator-sessions");
  if (!stored) {
    return [];
  }

  try {
    return JSON.parse(stored);
  } catch {
    return [];
  }
}

function saveRecentSession(session) {
  const nextSessions = [session, ...getRecentSessions()]
    .filter((item, index, array) => array.findIndex((entry) => entry.id === item.id) === index)
    .slice(0, 4);
  window.localStorage.setItem("ape.student.recent-generator-sessions", JSON.stringify(nextSessions));
}

function sumDifficultyCounts(counts) {
  return DIFFICULTIES.reduce((sum, difficulty) => sum + Number(counts[difficulty] || 0), 0);
}

function toggleValue(currentValues, value) {
  return currentValues.includes(value)
    ? currentValues.filter((item) => item !== value)
    : [...currentValues, value];
}

function buildFeDifficultyPayload(counts) {
  return DIFFICULTIES
    .map((difficulty) => ({ difficulty, count: Number(counts[difficulty] || 0) }))
    .filter((item) => item.count > 0);
}

function buildDifficultyInputState(counts) {
  return DIFFICULTIES.reduce((result, difficulty) => {
    result[difficulty] = String(Number(counts?.[difficulty] || 0));
    return result;
  }, {});
}

export function StudentExamConfigPage({ isAuthenticated = true, onLogout }) {
  const [courses, setCourses] = useState([]);
  const [documents, setDocuments] = useState([]);
  const [selectedSource, setSelectedSource] = useState("");
  const [selectedCourseId, setSelectedCourseId] = useState("");
  const [selectedDocumentIds, setSelectedDocumentIds] = useState([]);
  const [examType, setExamType] = useState("");
  const [feMode, setFeMode] = useState("");
  const [selectedTopicTags, setSelectedTopicTags] = useState([]);
  const [selectedPeDifficulties, setSelectedPeDifficulties] = useState([]);
  const [feDifficultyCounts, setFeDifficultyCounts] = useState({ Easy: 0, Medium: 0, Hard: 0 });
  const [feDifficultyInputs, setFeDifficultyInputs] = useState(() => buildDifficultyInputState({ Easy: 0, Medium: 0, Hard: 0 }));
  const [feDifficultyOverflow, setFeDifficultyOverflow] = useState({ Easy: false, Medium: false, Hard: false });
  const [pool, setPool] = useState({ availableCount: 0, items: [] });
  const [selectedPeQuestionIds, setSelectedPeQuestionIds] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingDocuments, setIsLoadingDocuments] = useState(false);
  const [isLoadingPool, setIsLoadingPool] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);
  const [recentSessions, setRecentSessions] = useState(() => getRecentSessions());
  const [toast, setToast] = useState(null);

  useEffect(() => {
    let ignore = false;

    async function loadCourses() {
      if (!isAuthenticated) {
        return;
      }

      setIsLoading(true);
      try {
        const items = await getStudentCourses();
        const nextCourses = Array.isArray(items) ? items : (items?.items || []);
        if (ignore) {
          return;
        }

        setCourses(nextCourses);
      } catch (error) {
        if (!ignore) {
          setToast({ type: "error", message: error.message || "Unable to load courses." });
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadCourses();
    return () => {
      ignore = true;
    };
  }, [isAuthenticated]);

  useEffect(() => {
    setSelectedDocumentIds([]);
    setSelectedTopicTags([]);
    setSelectedPeDifficulties([]);
    setSelectedPeQuestionIds([]);
    setFeDifficultyCounts({ Easy: 0, Medium: 0, Hard: 0 });
    setFeDifficultyOverflow({ Easy: false, Medium: false, Hard: false });
  }, [selectedSource, selectedCourseId]);

  useEffect(() => {
    setSelectedTopicTags([]);
    setSelectedPeDifficulties([]);
    setSelectedPeQuestionIds([]);
    setFeDifficultyCounts({ Easy: 0, Medium: 0, Hard: 0 });
    setFeDifficultyOverflow({ Easy: false, Medium: false, Hard: false });
  }, [examType]);

  useEffect(() => {
    setFeDifficultyInputs(buildDifficultyInputState(feDifficultyCounts));
  }, [feDifficultyCounts]);

  useEffect(() => {
    if (examType === "FE") {
      setFeMode((current) => current || "Practice");
      return;
    }

    setFeMode("");
  }, [examType]);

  useEffect(() => {
    let ignore = false;

    async function loadDocuments() {
      if (selectedSource !== "BYOS") {
        setDocuments([]);
        setSelectedDocumentIds([]);
        return;
      }

      if (!selectedCourseId) {
        setDocuments([]);
        setSelectedDocumentIds([]);
        return;
      }

      setIsLoadingDocuments(true);
      try {
        const nextDocuments = await getExamConfigDocuments(selectedCourseId, selectedSource);
        if (ignore) {
          return;
        }

        setDocuments(nextDocuments);
        setSelectedDocumentIds([]);
      } catch (error) {
        if (!ignore) {
          setDocuments([]);
          setSelectedDocumentIds([]);
          setToast({ type: "error", message: error.message || "Unable to load documents for this course." });
        }
      } finally {
        if (!ignore) {
          setIsLoadingDocuments(false);
        }
      }
    }

    loadDocuments();
    return () => {
      ignore = true;
    };
  }, [selectedCourseId, selectedSource]);

  useEffect(() => {
    let ignore = false;

    async function loadPool() {
      const requiresDocuments = selectedSource === "BYOS";
      if (!selectedCourseId || !examType || (requiresDocuments && !selectedDocumentIds.length)) {
        setPool({ availableCount: 0, items: [] });
        setSelectedPeQuestionIds([]);
        return;
      }

      setIsLoadingPool(true);
      try {
        const nextPool = await getExamQuestionPool({
          courseId: selectedCourseId,
          examType,
          documentIds: selectedDocumentIds,
          source: selectedSource
        });

        if (ignore) {
          return;
        }

        setPool(nextPool);
        setSelectedPeQuestionIds((current) =>
          current.filter((id) => nextPool.items.some((item) => item.id === id))
        );
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
  }, [selectedCourseId, selectedDocumentIds, examType, selectedSource]);

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

  const selectedDocuments = useMemo(
    () => documents.filter((document) => selectedDocumentIds.includes(document.id)),
    [documents, selectedDocumentIds]
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

  useEffect(() => {
    const allowedTags = new Set(topicTagOptions);
    setSelectedTopicTags((current) => current.filter((tag) => allowedTags.has(tag)));
  }, [topicTagOptions]);

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
      return;
    }

    const allowedIds = new Set(peFilteredItems.map((item) => item.id));
    setSelectedPeQuestionIds((current) => current.filter((id) => allowedIds.has(id)));
  }, [examType, peFilteredItems]);

  const FE_MAX_QUESTIONS = 50;

  useEffect(() => {
    if (examType !== "FE") {
      return;
    }

    setFeDifficultyCounts((current) => {
      const next = { ...current };
      let changed = false;

      DIFFICULTIES.forEach((difficulty) => {
        const maxValue = Math.min(FE_MAX_QUESTIONS, feDifficultyAvailability[difficulty] || 0);
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
  const configSummary = examType === "PE"
    ? `${selectedPeQuestionIds.length} PE questions`
    : `${totalFeCount} FE questions`;
  const canChooseType = Boolean(selectedSource);
  const canChooseMode = examType === "FE";
  const hasSetupCore = Boolean(selectedSource && examType && (examType === "PE" || feMode));
  const canChooseCourse = hasSetupCore;
  const canChooseDocuments = selectedSource === "BYOS" && Boolean(selectedCourseId);
  const canUseQuestionPool = Boolean(selectedCourseId && examType) && (selectedSource === "System" || selectedDocumentIds.length > 0);
  const canChooseTopics = canUseQuestionPool;
  const canChoosePeDifficultyFilter = examType === "PE" && canUseQuestionPool;
  const canChoosePeQuestions = examType === "PE" && canUseQuestionPool;
  const canChooseFeDifficultyCounts = examType === "FE" && canUseQuestionPool;
  const hasRequiredSourceInput = selectedCourseId && (selectedSource === "System" || selectedDocumentIds.length);
  const canStartPractice = hasRequiredSourceInput && !isGenerating && !isLoadingPool;
  const summarySourceValue = selectedSource || "Choose a source";
  const summaryTypeValue = selectedSource
    ? (examType ? `${examType}${examType === "FE" ? ` / ${feMode || "Practice"}` : " / Practice"}` : "Choose a type")
    : "-";
  const summaryCourseValue = !selectedSource
    ? "-"
    : examType
      ? (selectedCourse ? formatCourseLabel(selectedCourse) : "Choose a course")
      : "-";
  const summaryDocumentsValue = selectedSource !== "BYOS"
    ? null
    : !selectedCourseId
      ? "-"
      : selectedDocuments.length
        ? String(selectedDocuments.length)
        : "Choose documents";
  const summaryTopicValue = !selectedCourseId || (selectedSource === "BYOS" && !selectedDocumentIds.length)
    ? "-"
    : canChooseTopics
      ? String(selectedTopicTags.length || 0)
      : "Choose topics";
  const summaryPoolValue = !canUseQuestionPool
    ? "-"
    : String(examType === "PE" ? peFilteredItems.length : topicFilteredItems.length);
  const summaryQuestionCountValue = !canUseQuestionPool
    ? "-"
    : configSummary;
  const summaryDocumentDisplayValue = summaryDocumentsValue && !Number.isNaN(Number(summaryDocumentsValue))
    ? `${summaryDocumentsValue} selected`
    : summaryDocumentsValue;
  const summaryTopicDisplayValue = summaryTopicValue !== "-" && !Number.isNaN(Number(summaryTopicValue))
    ? `${summaryTopicValue} selected`
    : summaryTopicValue;
  const summaryPoolDisplayValue = summaryPoolValue !== "-" && !Number.isNaN(Number(summaryPoolValue))
    ? `${summaryPoolValue} available`
    : summaryPoolValue;
  const summaryQuestionCountDisplayValue = summaryQuestionCountValue === "-"
    ? summaryQuestionCountValue
    : configSummary;
  const summaryStatus = !selectedCourseId
    ? !selectedSource
      ? "Choose a source to begin."
      : !examType
        ? "Choose a type for this session."
        : examType === "FE" && !feMode
          ? "Choose a mode for the FE flow."
          : "Choose a course to continue."
    : selectedSource === "BYOS" && !selectedDocumentIds.length
      ? "Choose at least one ready My Documents file."
      : examType === "PE" && !selectedPeQuestionIds.length
        ? "Pick at least one PE question to continue."
        : examType === "FE" && !totalFeCount
          ? "Enter FE question counts by difficulty."
          : "Ready to generate from this configuration.";

  function toggleDocument(documentId) {
    setSelectedDocumentIds((current) => toggleValue(current, documentId));
  }

  function toggleTopicTag(tag) {
    setSelectedTopicTags((current) => toggleValue(current, tag));
  }

  function togglePeDifficulty(difficulty) {
    setSelectedPeDifficulties((current) => toggleValue(current, difficulty));
  }

  function togglePeQuestion(questionId) {
    setSelectedPeQuestionIds((current) =>
      current.includes(questionId)
        ? current.filter((item) => item !== questionId)
        : [...current, questionId]
    );
  }

  function updateFeDifficultyCount(difficulty, value) {
    const normalizedValue = String(value ?? "").replace(/\D/g, "");
    const maxValue = Math.min(FE_MAX_QUESTIONS, feDifficultyAvailability[difficulty] || 0);
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

  async function handleGenerate() {
    if (!selectedCourseId) {
      setToast({ type: "error", message: "Please choose a course." });
      return;
    }

    if (selectedSource === "BYOS" && !selectedDocumentIds.length) {
      setToast({ type: "error", message: "Please choose at least one document." });
      return;
    }

    if (examType === "PE") {
      if (!selectedPeQuestionIds.length) {
        setToast({ type: "error", message: "Please pick at least one PE question." });
        return;
      }
    } else {
      if (!totalFeCount) {
        setToast({ type: "error", message: "Please enter an FE question count for at least one difficulty." });
        return;
      }

      if (totalFeCount > FE_MAX_QUESTIONS) {
        setToast({ type: "error", message: `FE question count cannot exceed ${FE_MAX_QUESTIONS}. Current total: ${totalFeCount}.` });
        return;
      }

      const invalidDifficulty = DIFFICULTIES.find((difficulty) => Number(feDifficultyCounts[difficulty] || 0) > Math.min(FE_MAX_QUESTIONS, Number(feDifficultyAvailability[difficulty] || 0)));
      if (invalidDifficulty) {
        setToast({
          type: "error",
          message: `${invalidDifficulty} question count exceeds the available limit (${Math.min(FE_MAX_QUESTIONS, feDifficultyAvailability[invalidDifficulty] || 0)}).`
        });
        return;
      }
    }

    setIsGenerating(true);
    try {
      const result = await startConfiguredExam({
        courseId: selectedCourseId,
        source: selectedSource,
        documentIds: selectedDocumentIds,
        examType,
        mode: examType === "FE" ? feMode : "Practice",
        topicTags: selectedTopicTags,
        difficulties: examType === "PE"
          ? selectedPeDifficulties
          : buildFeDifficultyPayload(feDifficultyCounts).map((item) => item.difficulty),
        feDifficultyCounts: examType === "FE" ? buildFeDifficultyPayload(feDifficultyCounts) : [],
        peQuestionIds: examType === "PE" ? selectedPeQuestionIds : []
      });

      saveRecentSession({
        id: result?.session?.examId || `${selectedCourseId}-${examType}-${feMode}`,
        examType,
        value: examType === "PE" ? selectedPeQuestionIds.length : totalFeCount,
        title: `${selectedCourse?.code || selectedCourseId} ${examType} ${examType === "FE" ? feMode : "Practice"}`
      });
      setRecentSessions(getRecentSessions());
      setToast({ type: "success", message: `${examType} session created successfully.` });

      if (examType === "PE") {
        navigateTo(ROUTES.studentCodingPractice);
        return;
      }

      navigateTo(String(result?.mode || feMode).toLowerCase() === "flashcard"
        ? ROUTES.studentFlashcards
        : ROUTES.studentFeExam);
    } catch (error) {
      setToast({ type: "error", message: error.message || "Unable to generate from the current configuration." });
    } finally {
      setIsGenerating(false);
    }
  }

  return (
    <div className="student-exam-shell">
      <StudentTopbar activeSection="journey" actionLabel="Open Setup" actionHref={ROUTES.studentJourney} />

      <main className="student-exam-main">
        <section className="student-exam-page-header">
          <div>
            <h1>Practice Configuration</h1>
          </div>
          <div className="ai-inline-actions">
            <Button variant="ghost" className="student-exam-logout" onClick={() => navigateTo(ROUTES.studentJourney)}>
              Open Setup
            </Button>
          </div>
        </section>

        <section className="student-exam-config">
          <div className="student-exam-config__primary">
              <section className="student-exam-section student-exam-section--panel">
                <div className="student-exam-section__header">
                  <div>
                    <p className="student-exam-section__eyebrow">1. Practice Setup</p>
                    <h2>Choose the source and practice type</h2>
                  </div>
                </div>
                <div className="student-exam-form-grid">
                  <section className="student-exam-section student-exam-section--nested">
                    <div className="student-exam-section__header">
                      <div><h2>Source</h2></div>
                    </div>
                    <div className="student-segmented-control student-segmented-control--wide">
                      {SOURCES.map((source) => (
                        <button
                          key={source}
                          type="button"
                          className={selectedSource === source ? "is-active" : ""}
                          onClick={() => {
                            setSelectedSource(source);
                            setExamType("");
                            setSelectedCourseId("");
                          }}
                          disabled={isGenerating}
                        >
                          {getSourceLabel(source)}
                        </button>
                      ))}
                    </div>
                  </section>
                </div>

                <section className="student-exam-section student-exam-section--nested">
                  <div className="student-exam-section__header">
                    <div><h2>Type</h2></div>
                  </div>
                  <div className="student-segmented-control student-segmented-control--wide">
                    {EXAM_TYPES.map((type) => (
                      <button
                        key={type}
                        type="button"
                        className={examType === type ? "is-active" : ""}
                        onClick={() => setExamType(type)}
                        disabled={!canChooseType || isGenerating}
                      >
                        {type}
                      </button>
                    ))}
                  </div>
                </section>

                {examType === "FE" ? (
                  <section className="student-exam-section student-exam-section--nested">
                    <div className="student-exam-section__header">
                      <div><h2>Mode</h2></div>
                    </div>
                    <div className="student-segmented-control student-segmented-control--wide">
                      {FE_MODES.map((mode) => (
                        <button
                          key={mode}
                          type="button"
                          className={feMode === mode ? "is-active" : ""}
                          onClick={() => setFeMode(mode)}
                          disabled={!canChooseMode || isGenerating}
                        >
                          {mode}
                        </button>
                      ))}
                    </div>
                  </section>
                ) : null}
              </section>

            <section className="student-exam-section student-exam-section--panel">
              <div className="student-exam-section__header">
                <div>
                  <p className="student-exam-section__eyebrow">2. Content Filter</p>
                  <h2>Choose the course, content, and filter scope</h2>
                </div>
              </div>

              <label className="student-exam-field">
                <span>Course</span>
                <select
                  value={selectedCourseId}
                  onChange={(event) => setSelectedCourseId(event.target.value)}
                  disabled={!canChooseCourse || isLoading || !courses.length || isGenerating}
                >
                  <option value="">Select course</option>
                  {courses.map((course) => (
                    <option key={course.id} value={course.id}>
                      {formatCourseLabel(course)}
                    </option>
                  ))}
                </select>
              </label>

              {selectedSource === "BYOS" ? (
                <div className="student-exam-field">
                  <span>Documents</span>
                  <div className={`student-tag-grid student-tag-grid--selection${!documents.length ? " is-empty" : ""}`}>
                    {documents.map((document) => (
                      <label key={document.id} className="student-radio-option">
                        <input
                          type="checkbox"
                          checked={selectedDocumentIds.includes(document.id)}
                          onChange={() => toggleDocument(document.id)}
                          disabled={!canChooseDocuments || isLoadingDocuments || isGenerating}
                        />
                        <span>{document.fileName}</span>
                      </label>
                    ))}
                  </div>
                  {selectedCourseId && !documents.length && !isLoadingDocuments ? (
                    <div className="student-exam-empty-state">
                      <strong>No ready My Documents files for this course yet.</strong>
                      <p>Open My Documents to upload or process materials before starting this practice flow.</p>
                      <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentByosDocuments)}>
                        Open My Documents
                      </Button>
                    </div>
                  ) : null}
                </div>
              ) : null}

              <div className="student-exam-field">
                <span>Topic</span>
                <div className={`student-tag-grid student-tag-grid--selection${!topicTagOptions.length ? " is-empty" : ""}`}>
                  {topicTagOptions.map((tag) => (
                    <label key={tag} className="student-radio-option">
                      <input
                        type="checkbox"
                        checked={selectedTopicTags.includes(tag)}
                        onChange={() => toggleTopicTag(tag)}
                        disabled={!canChooseTopics || isLoadingPool || isGenerating}
                      />
                      <span>{tag}</span>
                    </label>
                  ))}
                </div>
              </div>

              {examType === "PE" ? (
                <div className="student-exam-field">
                  <span>Difficulty Filter</span>
                  <div className="student-tag-grid student-tag-grid--compact">
                    {DIFFICULTIES.map((difficulty) => (
                      <label key={difficulty} className="student-radio-option">
                        <input
                          type="checkbox"
                          checked={selectedPeDifficulties.includes(difficulty)}
                          onChange={() => togglePeDifficulty(difficulty)}
                          disabled={!canChoosePeDifficultyFilter || isLoadingPool || isGenerating}
                        />
                        <span>{difficulty}</span>
                      </label>
                    ))}
                  </div>
                  <div className="student-exam-helper-card">
                    <strong>PE filter only</strong>
                    <p>This difficulty setting only filters PE questions in the pool before you pick the final order.</p>
                  </div>
                </div>
              ) : null}
            </section>

            <section className="student-exam-section student-exam-section--panel">
              <div className="student-exam-section__header">
                <div>
                  <p className="student-exam-section__eyebrow">3. Output</p>
                  <h2>Finalize what this session will contain</h2>
                </div>
              </div>

              {examType === "PE" ? (
                <div className="student-exam-field">
                  <span>Pick questions</span>
                  <div className={`student-tag-grid student-tag-grid--selection${!peFilteredItems.length ? " is-empty" : ""}`}>
                    {peFilteredItems.map((question) => {
                      const pickedIndex = selectedPeQuestionIds.indexOf(question.id);
                      return (
                        <label key={question.id} className="student-radio-option">
                          <input
                            type="checkbox"
                            checked={pickedIndex >= 0}
                            onChange={() => togglePeQuestion(question.id)}
                            disabled={!canChoosePeQuestions || isLoadingPool || isGenerating}
                          />
                          <span>
                            {pickedIndex >= 0 ? `Q${pickedIndex + 1}` : "Pick"} - {question.title} ({normalizeDifficulty(question.difficulty)})
                          </span>
                        </label>
                      );
                    })}
                  </div>
                  {!canChoosePeQuestions ? (
                    <div className="student-exam-empty-state student-exam-empty-state--muted">
                      <strong>Complete the filters before picking questions.</strong>
                      <p>The question list will unlock after a valid course and source content have been selected.</p>
                    </div>
                  ) : null}
                  {!peFilteredItems.length && !isLoadingPool ? (
                    <div className="student-exam-empty-state student-exam-empty-state--muted">
                      <strong>No PE questions match the current filters.</strong>
                      <p>Try adjusting the selected topics or difficulty to widen the available pool.</p>
                    </div>
                  ) : null}
                </div>
              ) : (
                <div className="student-exam-field">
                  <span>Difficulty Counts</span>
                  <div className="student-exam-difficulty-grid">
                    {DIFFICULTIES.map((difficulty) => (
                      <label key={difficulty} className="student-exam-field student-exam-field--card">
                        <span>{difficulty}</span>
                        <strong className="student-exam-field__meta">Max {Math.min(FE_MAX_QUESTIONS, feDifficultyAvailability[difficulty] || 0)}</strong>
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
                          disabled={!canChooseFeDifficultyCounts || isLoadingPool || isGenerating || !feDifficultyAvailability[difficulty]}
                        />
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </section>
          </div>

          <aside className="student-exam-config__secondary">
            <div className="student-exam-summary-card">
              <div className="student-exam-summary-card__header">
                <div>
                  <span>Practice summary</span>
                  <strong>
                    {!selectedSource
                      ? "Choose a source"
                      : !examType
                        ? "Choose a type"
                        : !selectedCourse
                          ? "Choose a course"
                          : `${selectedCourse.code || selectedCourse.id} ${examType} ${examType === "FE" ? feMode : "Practice"}`}
                  </strong>
                </div>
                <p>{getSourceLabel(selectedSource) || "Setup required"}</p>
              </div>

              <div className="student-exam-summary-list">
                <div>
                  <span>Source</span>
                  <strong>{selectedSource ? getSourceLabel(summarySourceValue) : summarySourceValue}</strong>
                </div>
                <div>
                  <span>Type</span>
                  <strong>{summaryTypeValue}</strong>
                </div>
                <div>
                  <span>Course</span>
                  <strong>{summaryCourseValue}</strong>
                </div>
                {selectedSource === "BYOS" ? (
                  <div>
                    <span>Selected Documents</span>
                    <strong>{summaryDocumentDisplayValue}</strong>
                  </div>
                ) : null}
                <div>
                  <span>Selected Topics</span>
                  <strong>{summaryTopicDisplayValue}</strong>
                </div>
                <div>
                  <span>Available Pool</span>
                  <strong>{summaryPoolDisplayValue}</strong>
                </div>
                <div>
                  <span>Planned Questions</span>
                  <strong>{summaryQuestionCountDisplayValue}</strong>
                </div>
                <div>
                  <span>Timer</span>
                  <strong>Starts when practice begins</strong>
                </div>
              </div>
            </div>

            <div className="student-free-tier-panel student-free-tier-panel--summary">
              <span>Status</span>
              <strong>{summaryStatus}</strong>
              <p>
                {selectedSource === "BYOS"
                  ? "This flow depends on ready My Documents files and the matching question pool."
                  : "This flow uses the built-in system question pool for a faster start."}
              </p>
            </div>

            <Button
              variant="primary"
              className="student-generate-button"
              disabled={!canStartPractice}
              onClick={handleGenerate}
            >
              {isGenerating ? "Generating..." : "Generate and Start"}
            </Button>
          </aside>
        </section>

        <AppFooter links={footerLinks} className="app-footer--workspace" />
      </main>

      <ToastNotification toast={toast} />
    </div>
  );
}
