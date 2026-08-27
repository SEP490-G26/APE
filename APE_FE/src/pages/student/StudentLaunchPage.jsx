import { useEffect, useMemo, useRef, useState } from "react";
import Editor from "@monaco-editor/react";
import { Button } from "../../components/common";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import {
  getMcqSession,
  getPePracticeSession,
  pausePePracticeSession,
  runPeCode,
  saveMcqSession,
  savePePracticeSessionSnapshot,
  savePeDraftCode,
  savePracticeSessionAnswers,
  submitPeCode
} from "../../services/studentWorkspaceService";

const LEFT_TABS = [
  { id: "description", label: "Description" }
];
const BOTTOM_TABS = [
  { id: "stdin", label: "Testcase" },
  { id: "console", label: "Test Result" }
];

function formatElapsedTime(totalSeconds) {
  const safeSeconds = Math.max(0, Number(totalSeconds) || 0);
  const hours = Math.floor(safeSeconds / 3600);
  const minutes = Math.floor((safeSeconds % 3600) / 60);
  const seconds = safeSeconds % 60;

  if (hours > 0) {
    return [hours, minutes, seconds].map((value) => String(value).padStart(2, "0")).join(":");
  }

  return [minutes, seconds].map((value) => String(value).padStart(2, "0")).join(":");
}

function getElapsedSeconds(startTime) {
  if (!startTime) {
    return 0;
  }

  const start = new Date(startTime).getTime();
  if (!Number.isFinite(start)) {
    return 0;
  }

  return Math.max(0, Math.floor((Date.now() - start) / 1000));
}

function getActiveElapsedSeconds(session) {
  if (!session) {
    return 0;
  }

  const baseSeconds = Math.max(0, Number(session?.activeDurationSeconds ?? 0));
  if (session?.isPaused) {
    return baseSeconds;
  }

  const resumedAt = new Date(session?.lastResumedAt ?? session?.startTime ?? 0).getTime();
  if (!Number.isFinite(resumedAt)) {
    return baseSeconds;
  }

  return Math.max(0, baseSeconds + Math.floor((Date.now() - resumedAt) / 1000));
}

function renderMarkdownBlocks(markdown = "") {
  return markdown.split("\n").map((line, index) => {
    const trimmed = line.trim();

    if (!trimmed) {
      return <div key={`space-${index}`} className="coding-problem-panel__spacer" />;
    }

    if (trimmed.startsWith("## ")) {
      return <h3 key={index}>{trimmed.slice(3)}</h3>;
    }

    if (trimmed.startsWith("- ")) {
      return (
        <div key={index} className="coding-problem-panel__bullet">
          <span />
          <p>{trimmed.slice(2)}</p>
        </div>
      );
    }

    return <p key={index}>{trimmed}</p>;
  });
}

function inferMonacoLanguage(fileName = "", fallbackLanguage = "") {
  const normalized = String(fileName || "").toLowerCase();
  if (normalized.endsWith(".c") || normalized.endsWith(".h")) {
    return "c";
  }
  if (normalized.endsWith(".cpp") || normalized.endsWith(".cc") || normalized.endsWith(".cxx")) {
    return "cpp";
  }
  if (normalized.endsWith(".java")) {
    return "java";
  }
  if (normalized.endsWith(".js")) {
    return "javascript";
  }
  if (normalized.endsWith(".ts")) {
    return "typescript";
  }

  const fallback = String(fallbackLanguage || "").toLowerCase();
  if (fallback === "c") {
    return "c";
  }
  if (fallback === "java") {
    return "java";
  }

  return "plaintext";
}

function resolveFlashcardAnswerText(question) {
  if (!question) {
    return "No answer";
  }

  if (typeof question.answer === "number" && Array.isArray(question.options) && question.answer >= 0 && question.answer < question.options.length) {
    return question.options[question.answer];
  }

  if (Array.isArray(question.correctAnswer) && question.correctAnswer.length > 0) {
    return question.correctAnswer.join(", ");
  }

  return "No answer";
}

function renderFlashcardOptions(options = []) {
  return options.map((option, index) => (
    <div key={`${index}-${option}`} className="flashcard-viewer__option">
      <strong>{String.fromCharCode(65 + index)}</strong>
      <p>{option}</p>
    </div>
  ));
}

function getBaseName(fileName = "") {
  return String(fileName || "").split(/[\\/]/).pop() || "";
}

function parseJudge0Markers(output, files, monaco) {
  if (!output || !monaco) {
    return {};
  }

  const markersByFile = {};
  const knownFiles = (files || []).map((file) => getBaseName(file.name));
  const lines = String(output).split(/\r?\n/);

  lines.forEach((line) => {
    const match = line.match(/^([^:\r\n]+):(\d+):(?:(\d+):)?\s*(fatal error|error|warning|note):\s*(.+)$/i);
    if (!match) {
      return;
    }

    const [, rawFile, rawLine, rawColumn, rawKind, rawMessage] = match;
    const fileName = getBaseName(rawFile);
    const resolvedFile = knownFiles.find((item) => item.toLowerCase() === fileName.toLowerCase()) || knownFiles[0] || fileName;
    const severity = String(rawKind || "").toLowerCase().includes("warning")
      ? monaco.MarkerSeverity.Warning
      : String(rawKind || "").toLowerCase().includes("note")
        ? monaco.MarkerSeverity.Info
        : monaco.MarkerSeverity.Error;

    markersByFile[resolvedFile] ??= [];
    markersByFile[resolvedFile].push({
      startLineNumber: Math.max(1, Number(rawLine) || 1),
      startColumn: Math.max(1, Number(rawColumn) || 1),
      endLineNumber: Math.max(1, Number(rawLine) || 1),
      endColumn: Math.max((Number(rawColumn) || 1) + 1, 2),
      message: rawMessage?.trim() || line,
      severity
    });
  });

  return markersByFile;
}

function buildGenericMarkers(output, monaco) {
  if (!output || !monaco) {
    return [];
  }

  return [
    {
      startLineNumber: 1,
      startColumn: 1,
      endLineNumber: 1,
      endColumn: 1,
      message: String(output),
      severity: monaco.MarkerSeverity.Error
    }
  ];
}

function formatCodeFallback(content = "") {
  const lines = String(content).replace(/\t/g, "    ").split("\n");
  let indentLevel = 0;

  return lines
    .map((line) => {
      const trimmed = line.trim();
      if (!trimmed) {
        return "";
      }

      if (/^[}\])]/.test(trimmed)) {
        indentLevel = Math.max(0, indentLevel - 1);
      }

      const formatted = `${"    ".repeat(indentLevel)}${trimmed}`;

      if (/[{[(]$/.test(trimmed)) {
        indentLevel += 1;
      }

      return formatted;
    })
    .join("\n");
}

function formatConsoleValue(value, emptyLabel = "(no output)") {
  const normalized = String(value ?? "");
  return normalized.trim().length ? normalized : emptyLabel;
}

function mapRunExecutionStatus(status = "", executionStatus = "") {
  const normalizedExecutionStatus = String(executionStatus || "").trim().toLowerCase();
  switch (normalizedExecutionStatus) {
    case "passed":
      return "Passed";
    case "failed":
      return "Failed";
    case "notexecuted":
      return "Not executed";
    case "completed":
      return "Completed";
    default:
      break;
  }

  switch (String(status || "").trim().toLowerCase()) {
    case "accepted":
      return "Passed";
    case "compilation error":
      return "Compilation failed";
    case "runtime error":
      return "Runtime error";
    case "time limit exceeded":
      return "Time limit exceeded";
    case "memory limit exceeded":
      return "Memory limit exceeded";
    case "output limit exceeded":
      return "Output limit exceeded";
    case "judge0 error":
      return "Execution service error";
    case "queued":
      return "Queued";
    case "running":
      return "Running";
    case "wrong answer":
      return "Wrong answer";
    default:
      return status || executionStatus || "Unknown";
  }
}

function formatDiagnosticLine(diagnostic) {
  if (!diagnostic) {
    return "";
  }

  const location = diagnostic.filename
    ? [diagnostic.filename, diagnostic.line, diagnostic.column].filter((value) => value !== null && value !== undefined && value !== "").join(":")
    : [diagnostic.line, diagnostic.column].filter((value) => value !== null && value !== undefined && value !== "").join(":");
  const headline = diagnostic.title || diagnostic.code || diagnostic.category || "Diagnostic";
  const detail = diagnostic.message || "";

  return [headline, detail, location ? `Location: ${location}` : ""].filter(Boolean).join("\n");
}

function formatDiagnosticsBlock(diagnostics = []) {
  if (!Array.isArray(diagnostics) || !diagnostics.length) {
    return "";
  }

  return [
    "Diagnostics:",
    ...diagnostics.map((item, index) => `${index + 1}. ${formatDiagnosticLine(item)}`)
  ].join("\n");
}

export function StudentLaunchPage({ mode = "Practice" }) {
  const isCoding = mode === "Coding";
  const [mcqSession, setMcqSession] = useState(null);
  const [codingSession, setCodingSession] = useState(null);
  const [isFlipped, setIsFlipped] = useState(false);
  const [cardRatings, setCardRatings] = useState({});
  const [touchStartX, setTouchStartX] = useState(null);
  const [questionDrafts, setQuestionDrafts] = useState({});
  const [selectedPeQuestionId, setSelectedPeQuestionId] = useState("");
  const [currentIndex, setCurrentIndex] = useState(0);
  const [selectedOption, setSelectedOption] = useState(null);
  const [checkedQuestions, setCheckedQuestions] = useState({});
  const [collapsedExplanation, setCollapsedExplanation] = useState(false);
  const [practiceSummary, setPracticeSummary] = useState(null);
  const [consoleOutput, setConsoleOutput] = useState("Compilation successful.\nReady to run custom test cases.");
  const [stdin, setStdin] = useState("");
  const [files, setFiles] = useState([]);
  const [activeFile, setActiveFile] = useState("");
  const [activeLeftTab, setActiveLeftTab] = useState("description");
  const [activeBottomTab, setActiveBottomTab] = useState("stdin");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isRunning, setIsRunning] = useState(false);
  const [runMode, setRunMode] = useState("sample");
  const [codingError, setCodingError] = useState("");
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [codingSplit, setCodingSplit] = useState(54);
  const [descriptionSplit, setDescriptionSplit] = useState(62);
  const submitDebounceRef = useRef(null);
  const codingWorkspaceRef = useRef(null);
  const codingSidePaneRef = useRef(null);
  const dragStateRef = useRef(null);
  const monacoRef = useRef(null);
  const editorRef = useRef(null);
  const [markersByFile, setMarkersByFile] = useState({});
  const codingSessionRef = useRef(null);
  const currentPeQuestionRef = useRef(null);
  const filesRef = useRef([]);
  const questionDraftsRef = useRef({});
  const selectedPeQuestionIdRef = useRef("");
  const isSubmittingRef = useRef(false);
  const activeEditorValueRef = useRef("");

  useEffect(() => {
    if (isCoding) {
      getPePracticeSession().then((session) => {
        setCodingSession(session);
        const questions = Array.isArray(session.questions) && session.questions.length
          ? session.questions
          : [{
              id: session.questionId || session.id,
              title: session.title,
              description: session.description,
              difficulty: session.difficulty,
              topicTags: session.topicTags || [],
              constraints: session.constraints || [],
              samples: session.samples || [],
              discussionHints: session.discussionHints || [],
              skeletonFiles: session.skeletonFiles || []
            }];
        const initialQuestionId = session.selectedQuestionId || questions[0]?.id || "";
        const draftMap = Object.fromEntries(
          questions.map((question) => [question.id, question.skeletonFiles || []])
        );

        setQuestionDrafts(draftMap);
        setSelectedPeQuestionId(initialQuestionId);
        setFiles(draftMap[initialQuestionId] || []);
        setActiveFile((draftMap[initialQuestionId] || [])[0]?.name || "");
      });
      return;
    }

    getMcqSession(mode === "Practice" ? "Flashcard" : mode).then(setMcqSession);
  }, [isCoding, mode]);

  useEffect(() => {
    if (!isCoding) {
      return undefined;
    }

    const handlePointerMove = (event) => {
      const dragState = dragStateRef.current;
      if (!dragState) {
        return;
      }

      if (dragState.type === "vertical" && codingWorkspaceRef.current) {
        const bounds = codingWorkspaceRef.current.getBoundingClientRect();
        const nextSplit = ((event.clientX - bounds.left) / bounds.width) * 100;
        const clampedSplit = Math.min(72, Math.max(28, nextSplit));
        setCodingSplit(clampedSplit);
      }

      if (dragState.type === "horizontal" && codingSidePaneRef.current) {
        const bounds = codingSidePaneRef.current.getBoundingClientRect();
        const nextSplit = ((event.clientY - bounds.top) / bounds.height) * 100;
        const clampedSplit = Math.min(76, Math.max(24, nextSplit));
        setDescriptionSplit(clampedSplit);
      }
    };

    const stopDragging = () => {
      if (!dragStateRef.current) {
        return;
      }

      dragStateRef.current = null;
      document.body.style.cursor = "";
      document.body.style.userSelect = "";
    };

    window.addEventListener("pointermove", handlePointerMove);
    window.addEventListener("pointerup", stopDragging);

    return () => {
      window.removeEventListener("pointermove", handlePointerMove);
      window.removeEventListener("pointerup", stopDragging);
      document.body.style.cursor = "";
      document.body.style.userSelect = "";
    };
  }, [isCoding]);

  useEffect(() => {
    const activeSession = isCoding ? codingSession : mcqSession;

    if (isCoding) {
      if (!activeSession?.sessionId) {
        setElapsedSeconds(0);
        return undefined;
      }

      const syncElapsed = () => setElapsedSeconds(getActiveElapsedSeconds(activeSession));
      syncElapsed();

      const intervalId = window.setInterval(syncElapsed, 1000);
      return () => window.clearInterval(intervalId);
    }

    const startTime = activeSession?.startTime ?? activeSession?.startedAt;
    if (!startTime) {
      setElapsedSeconds(0);
      return undefined;
    }

    const syncElapsed = () => setElapsedSeconds(getElapsedSeconds(startTime));
    syncElapsed();

    const intervalId = window.setInterval(syncElapsed, 1000);
    return () => window.clearInterval(intervalId);
  }, [codingSession, isCoding, mcqSession]);

  const handleSplitterPointerDown = (event, type) => {
    dragStateRef.current = { type };
    document.body.style.cursor = type === "vertical" ? "ew-resize" : "ns-resize";
    document.body.style.userSelect = "none";
    event.currentTarget.setPointerCapture?.(event.pointerId);
  };

  const currentQuestion = useMemo(() => mcqSession?.questions?.[currentIndex] || null, [currentIndex, mcqSession]);
  const peQuestions = useMemo(() => {
    if (!codingSession) {
      return [];
    }

    if (Array.isArray(codingSession.questions) && codingSession.questions.length) {
      return codingSession.questions;
    }

    return [{
      id: codingSession.questionId || codingSession.id,
      title: codingSession.title,
      description: codingSession.description,
      difficulty: codingSession.difficulty,
      topicTags: codingSession.topicTags || [],
      constraints: codingSession.constraints || [],
      samples: codingSession.samples || [],
      discussionHints: codingSession.discussionHints || [],
      skeletonFiles: codingSession.skeletonFiles || []
    }];
  }, [codingSession]);
  const currentPeQuestion = useMemo(
    () => peQuestions.find((question) => question.id === selectedPeQuestionId) || peQuestions[0] || null,
    [peQuestions, selectedPeQuestionId]
  );

  useEffect(() => {
    codingSessionRef.current = codingSession;
  }, [codingSession]);

  useEffect(() => {
    currentPeQuestionRef.current = currentPeQuestion;
  }, [currentPeQuestion]);

  useEffect(() => {
    filesRef.current = files;
  }, [files]);

  useEffect(() => {
    questionDraftsRef.current = questionDrafts;
  }, [questionDrafts]);

  useEffect(() => {
    selectedPeQuestionIdRef.current = selectedPeQuestionId;
  }, [selectedPeQuestionId]);

  useEffect(() => {
    isSubmittingRef.current = isSubmitting;
  }, [isSubmitting]);

  const answeredCount = Object.keys(checkedQuestions).length;
  const flashcardCounters = useMemo(() => {
    const values = Object.values(cardRatings);
    return {
      knew: values.filter((value) => value === "knew").length,
      didntKnow: values.filter((value) => value === "didnt-know").length
    };
  }, [cardRatings]);
  const activeFileContent = files.find((item) => item.name === activeFile);
  const activeFileIndex = files.findIndex((item) => item.name === activeFile);
  const activeEditorLanguage = inferMonacoLanguage(activeFile, codingSession?.language);
  const activeEditorValue = activeFileContent?.content ?? "";
  activeEditorValueRef.current = activeEditorValue;

  useEffect(() => {
    if (!monacoRef.current || !editorRef.current) {
      return;
    }

    const model = editorRef.current.getModel();
    if (!model) {
      return;
    }

    const nextMarkers = markersByFile[getBaseName(activeFile)] || [];
    monacoRef.current.editor.setModelMarkers(model, "judge0", nextMarkers);
  }, [activeFile, markersByFile]);

  useEffect(() => {
    if (!isCoding || !currentPeQuestion) {
      return;
    }

    // Only re-load files when the question itself changes, NOT on every questionDrafts update
    // (questionDrafts updates on every keystroke which would reset the editor mid-typing)
    const nextFiles = questionDraftsRef.current[currentPeQuestion.id] || currentPeQuestion.skeletonFiles || [];
    setFiles(nextFiles);
    setActiveFile((current) =>
      nextFiles.some((file) => file.name === current) ? current : nextFiles[0]?.name || ""
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentPeQuestion?.id, isCoding]);

  useEffect(() => {
    if (!isCoding || !codingSession?.sessionId) {
      return;
    }

    const nextQuestions = peQuestions.map((question) => ({
      ...question,
      skeletonFiles: questionDrafts[question.id] || question.skeletonFiles || []
    }));
    const activeQuestion = nextQuestions.find((question) => question.id === selectedPeQuestionId) || nextQuestions[0] || null;

    savePePracticeSessionSnapshot({
      ...codingSession,
      selectedQuestionId: selectedPeQuestionId,
      questions: nextQuestions,
      skeletonFiles: activeQuestion?.skeletonFiles || codingSession.skeletonFiles || []
    });
  }, [codingSession, isCoding, peQuestions, questionDrafts, selectedPeQuestionId]);

  useEffect(() => {
    if (!isCoding || !codingSession?.sessionId || !currentPeQuestion?.id) {
      return undefined;
    }

    const intervalId = window.setInterval(() => {
      persistDrafts().catch(() => {});
    }, 5 * 60 * 1000);

    return () => window.clearInterval(intervalId);
  }, [codingSession?.sessionId, currentPeQuestion?.id, isCoding, selectedPeQuestionId]);

  useEffect(() => {
    if (!isCoding || !codingSession?.sessionId) {
      return undefined;
    }

    const pauseAndPersist = () => {
      const activeSession = codingSessionRef.current;
      const activeQuestion = currentPeQuestionRef.current;
      if (!activeSession?.sessionId || isSubmittingRef.current) {
        return;
      }

      persistDrafts(activeQuestion?.id || "").catch(() => {});

      pausePePracticeSession(activeSession.sessionId, { keepalive: true }).catch(() => {});
    };

    window.addEventListener("pagehide", pauseAndPersist);
    return () => {
      window.removeEventListener("pagehide", pauseAndPersist);
      pauseAndPersist();
    };
  }, [codingSession?.sessionId, isCoding]);

  const practiceSidebar = (
    <aside className="practice-sidebar practice-sidebar--workspace">
      <nav className="practice-sidebar__nav">
        <button type="button" className="practice-sidebar__link is-active">
          {isCoding ? "Practice Space" : "Workspace"}
        </button>
      </nav>
    </aside>
  );

  function buildLatestFilesSnapshot(baseFiles = filesRef.current) {
    const normalizedFiles = Array.isArray(baseFiles) ? baseFiles : [];
    const editorValue = editorRef.current?.getValue?.();

    if (!activeFile || editorValue === undefined || activeFileIndex < 0) {
      return normalizedFiles;
    }

    return normalizedFiles.map((file, index) =>
      index === activeFileIndex
        ? { ...file, content: editorValue ?? "" }
        : file
    );
  }

  function syncLatestEditorContent() {
    const latestFiles = buildLatestFilesSnapshot();

    setFiles(latestFiles);

    if (currentPeQuestion?.id) {
      setQuestionDrafts((drafts) => ({
        ...drafts,
        [currentPeQuestion.id]: latestFiles
      }));
    }

    filesRef.current = latestFiles;

    if (currentPeQuestion?.id) {
      questionDraftsRef.current = {
        ...(questionDraftsRef.current || {}),
        [currentPeQuestion.id]: latestFiles
      };
    }

    return latestFiles;
  }

  async function persistDrafts(questionIdOverride = "") {
    if (!codingSession?.sessionId) {
      return;
    }

    const latestFiles = syncLatestEditorContent();
    const drafts = {
      ...(questionDraftsRef.current || {}),
      ...(questionIdOverride || currentPeQuestion?.id
        ? { [questionIdOverride || currentPeQuestion?.id]: latestFiles }
        : {})
    };
    const entries = questionIdOverride
      ? [[questionIdOverride, drafts[questionIdOverride] || latestFiles]]
      : Object.entries(drafts);

    await Promise.all(entries
      .filter(([questionId, draftFiles]) => questionId && Array.isArray(draftFiles))
      .map(([questionId, draftFiles]) =>
        savePeDraftCode({
          sessionId: codingSession.sessionId,
          questionId,
          selectedQuestionId: selectedPeQuestionIdRef.current || questionId,
          files: draftFiles
        })
      ));
  }

  async function handleSelectPeQuestion(nextQuestionId) {
    if (!nextQuestionId || nextQuestionId === selectedPeQuestionId) {
      return;
    }

    try {
      if (currentPeQuestion?.id && codingSession?.sessionId) {
        await persistDrafts(currentPeQuestion.id);
      }
    } catch {
      // keep local drafts even if background sync fails
    }

    setSelectedPeQuestionId(nextQuestionId);
  }

  async function handleCheck() {
    if (!currentQuestion || selectedOption === null) {
      return;
    }

    const isCorrect = currentQuestion.answer === selectedOption;
    const nextChecked = {
      ...checkedQuestions,
      [currentQuestion.id]: {
        selectedOption,
        isCorrect
      }
    };

    setCheckedQuestions(nextChecked);
    setCollapsedExplanation(false);
    await saveMcqSession({ ...mcqSession, checkedQuestions: nextChecked });
  }

  async function handleFinishPractice() {
    if (!mcqSession?.questions?.length) {
      return;
    }

    const correctCount = mcqSession.questions.filter((question) => checkedQuestions[question.id]?.isCorrect).length;
    const summary = {
      totalQuestions: mcqSession.questions.length,
      correctCount,
      scorePercent: Math.round((correctCount / mcqSession.questions.length) * 100)
    };

    setPracticeSummary(summary);
    await savePracticeSessionAnswers({
      mode,
      fe_answers: mcqSession.questions.map((question) => ({
        questionId: question.id,
        selectedOption: checkedQuestions[question.id]?.selectedOption ?? null,
        isCorrect: Boolean(checkedQuestions[question.id]?.isCorrect)
      })),
      ...summary
    });
  }

  async function handleRun(runTarget = "sample") {
    if (!currentPeQuestion?.id || isRunning) {
      return;
    }

    setIsRunning(true);
    setRunMode(runTarget);
    setCodingError("");

    try {
      const latestFiles = syncLatestEditorContent();

      if (codingSession?.sessionId && currentPeQuestion?.id) {
        await persistDrafts(currentPeQuestion.id);
      }

      let result;

      if (runTarget === "custom") {
        const customRun = await runPeCode({
          questionId: currentPeQuestion?.id,
          files: latestFiles,
          stdin
        });
        const customRunDetails = customRun.customRunResult || {
          label: "Custom input",
          status: customRun.status,
          executionStatus: customRun.executionStatus,
          isJudged: customRun.isJudged,
          input: stdin,
          actualOutput: customRun.stdout || "",
          stderr: customRun.stderr || "",
          compileOutput: customRun.compileOutput || "",
          runtimeMs: customRun.runtimeMs || 0,
          memoryKb: customRun.memoryKb || 0,
          diagnostics: customRun.diagnostics || []
        };
        result = {
          ...customRun,
          customRunResult: {
            ...customRunDetails,
            input: customRunDetails.input ?? stdin
          }
        };
      } else {
        result = await runPeCode({
          questionId: currentPeQuestion?.id,
          files: latestFiles
        });
      }

      setActiveBottomTab("console");

      const sections = [
        runTarget === "sample"
          ? `Sample run result: ${result.status || "Unknown"}`
          : `Custom run result: ${result.status || "Unknown"}`,
        result.totalSampleCases
          ? `Sample cases passed: ${result.passedSampleCases}/${result.totalSampleCases}`
          : "",
        result.sampleResults?.length
          ? [
              "Sample details:",
              ...result.sampleResults.map((item) => {
                const blocks = [
                  `${item.label || `Sample ${item.number || item.index}`}: ${mapRunExecutionStatus(item.status, item.executionStatus) || "Unknown"}`,
                  item.isJudged ? `Verdict: ${item.status || "Unknown"}` : "Verdict: Not judged",
                  `Input:\n${formatConsoleValue(item.input, "(empty)")}`,
                  `Expected Output:\n${formatConsoleValue(item.expectedOutput)}`,
                  `Your Output:\n${formatConsoleValue(item.actualOutput)}`,
                  item.stderr ? `Stderr:\n${item.stderr}` : "",
                  item.compileOutput ? `Compile Output:\n${item.compileOutput}` : "",
                  formatDiagnosticsBlock(item.diagnostics),
                  item.runtimeMs ? `Runtime: ${item.runtimeMs} ms` : "",
                  item.memoryKb ? `Memory: ${item.memoryKb} KB` : ""
                ].filter(Boolean);
                return blocks.join("\n");
              })
            ].join("\n\n")
          : "",
        result.customRunResult
          ? [
              "Custom input details:",
              `Execution: ${mapRunExecutionStatus(result.customRunResult.status, result.customRunResult.executionStatus) || "Unknown"}`,
              `Input:\n${formatConsoleValue(result.customRunResult.input, "(empty)")}`,
              `Output:\n${formatConsoleValue(result.customRunResult.actualOutput)}`,
              result.customRunResult.stderr ? `Stderr:\n${result.customRunResult.stderr}` : "",
              result.customRunResult.compileOutput ? `Compile Output:\n${result.customRunResult.compileOutput}` : "",
              formatDiagnosticsBlock(result.customRunResult.diagnostics),
              result.customRunResult.runtimeMs ? `Runtime: ${result.customRunResult.runtimeMs} ms` : "",
              result.customRunResult.memoryKb ? `Memory: ${result.customRunResult.memoryKb} KB` : ""
            ].filter(Boolean).join("\n\n")
          : "",
      ].filter(Boolean);

      setConsoleOutput(sections.join("\n\n") || "Code executed without output.");
      if (monacoRef.current) {
        const markerSource = [
          result.compileOutput,
          result.stderr,
          result.customRunResult?.compileOutput,
          result.customRunResult?.stderr,
          ...(result.sampleResults || []).flatMap((item) => [item?.compileOutput, item?.stderr])
        ].filter(Boolean).join("\n");
        const parsedMarkers = parseJudge0Markers(markerSource, files, monacoRef.current);
        if (Object.keys(parsedMarkers).length === 0 && markerSource) {
          parsedMarkers[getBaseName(activeFile)] = buildGenericMarkers(markerSource, monacoRef.current);
        }
        setMarkersByFile(parsedMarkers);
      }
    } catch (error) {
      setActiveBottomTab("console");
      setCodingError(error.message || "Unable to run this code right now.");
      setConsoleOutput("Run failed.");
    } finally {
      setIsRunning(false);
      setRunMode("sample");
    }
  }

  async function handleSubmit() {
    if (isSubmitting || submitDebounceRef.current) {
      return;
    }

    submitDebounceRef.current = window.setTimeout(() => {
      submitDebounceRef.current = null;
    }, 900);

    setIsSubmitting(true);
    setCodingError("");

    try {
      syncLatestEditorContent();

      if (codingSession?.sessionId && currentPeQuestion?.id) {
        await persistDrafts();
      }

      await submitPeCode({
        sessionId: codingSession?.sessionId,
        questions: peQuestions,
        draftFilesByQuestion: questionDraftsRef.current
      });
      navigateTo(ROUTES.studentResultViewer);
    } catch (error) {
      setActiveBottomTab("console");
      setCodingError(error.message || "Unable to submit this code right now.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleFormatCode() {
    if (!editorRef.current) {
      return;
    }

    try {
      await editorRef.current.getAction("editor.action.formatDocument")?.run();
    } catch {
      const fallback = formatCodeFallback(activeFileContent?.content || "");
      setFiles((current) => {
        const nextFiles = current.map((file, index) =>
          index === activeFileIndex ? { ...file, content: fallback } : file
        );
        if (currentPeQuestion?.id) {
          setQuestionDrafts((drafts) => ({
            ...drafts,
            [currentPeQuestion.id]: nextFiles
          }));
        }
        return nextFiles;
      });
    }
  }

  function goToFlashcard(nextIndex) {
    if (!mcqSession?.questions?.length) {
      return;
    }

    const boundedIndex = Math.max(0, Math.min(mcqSession.questions.length - 1, nextIndex));
    setCurrentIndex(boundedIndex);
    setIsFlipped(false);
  }

  function markFlashcard(status) {
    if (!currentQuestion) {
      return;
    }

    setCardRatings((current) => ({
      ...current,
      [currentQuestion.id]: status
    }));
  }

  if (isCoding && codingSession) {
    return (
      <StudentScaffold
        activeRoute={ROUTES.studentCodingPractice}
        title="Practice Coding Space"
        subtitle="Read the problem, edit code across multiple files, run custom tests, submit, then continue into result and mentor review."
        actions={
          <div className="ai-inline-actions">
            <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentExams)}>Back to Configuration</Button>
            <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentResultViewer)}>Last Result</Button>
          </div>
      }
      topbarActionLabel="Open Result"
      topbarActionHref={ROUTES.studentResultViewer}
      showPageHeader={false}
      showFooter={false}
    >
      <section className="practice-shell practice-shell--coding">
          <div className="practice-shell__content practice-shell__content--coding">
            <section
              ref={codingWorkspaceRef}
              className="coding-workspace coding-workspace--leetcode"
              style={{
                gridTemplateColumns: `minmax(420px, ${codingSplit}fr) 8px minmax(520px, ${100 - codingSplit}fr)`
              }}
            >
              <article className="student-panel coding-problem-panel coding-problem-panel--leetcode">
                <div className="coding-panel-tabs" role="tablist" aria-label="Problem tabs">
                  {LEFT_TABS.map((tab) => (
                    <button
                      key={tab.id}
                      type="button"
                      className={`coding-panel-tab${activeLeftTab === tab.id ? " is-active" : ""}`}
                      onClick={() => setActiveLeftTab(tab.id)}
                    >
                      {tab.label}
                    </button>
                  ))}
                </div>
                <div className="coding-problem-scroll">
                  {activeLeftTab === "description" ? (
                    <>
                      <div className="coding-problem-panel__header">
                        <div>
                          <h2>{currentPeQuestion?.title || codingSession.title}</h2>
                        </div>
                        <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
                          <span className={`coding-difficulty-badge coding-difficulty-badge--${String(currentPeQuestion?.difficulty || codingSession.difficulty).toLowerCase()}`}>
                            {currentPeQuestion?.difficulty || codingSession.difficulty}
                          </span>
                          <span className="coding-utility-pill">Elapsed {formatElapsedTime(elapsedSeconds)}</span>
                        </div>
                      </div>

                      {peQuestions.length > 1 ? (
                        <div className="question-nav-grid">
                          {peQuestions.map((question, index) => (
                            <button
                              key={question.id}
                              type="button"
                              className={`question-nav-grid__item${currentPeQuestion?.id === question.id ? " is-active" : ""}`}
                              onClick={() => handleSelectPeQuestion(question.id)}
                            >
                              Q{index + 1}
                            </button>
                          ))}
                        </div>
                      ) : null}

                      <div className="coding-problem-panel__markdown">
                        {renderMarkdownBlocks(currentPeQuestion?.description || codingSession.description)}
                      </div>

                      <div className="coding-constraint-panel">
                        <h3>Constraints</h3>
                        <div className="coding-constraint-list">
                          {((currentPeQuestion?.constraints || codingSession.constraints || [])).map((constraint) => (
                            <div key={constraint} className="coding-constraint-item">
                              {constraint}
                            </div>
                          ))}
                        </div>
                      </div>

                      <div className="student-tag-grid">
                        {((currentPeQuestion?.topicTags || codingSession.topicTags || [])).map((tag) => <span key={tag} className="student-tag-chip">#{tag}</span>)}
                      </div>

                      <div className="coding-sample-grid">
                        {((currentPeQuestion?.samples || codingSession.samples || [])).map((sample, index) => (
                          <div key={index} className="student-distribution-card coding-sample-card">
                            <strong>Example {index + 1}</strong>
                            <div className="coding-sample-card__block">
                              <span>Input</span>
                              <pre>{sample.input}</pre>
                            </div>
                            <div className="coding-sample-card__block">
                              <span>Output</span>
                              <pre>{sample.output}</pre>
                            </div>
                            {sample.explanation ? (
                              <div className="coding-sample-card__block">
                                <span>Explanation</span>
                                <p>{sample.explanation}</p>
                              </div>
                            ) : null}
                          </div>
                        ))}
                      </div>

                      {((currentPeQuestion?.discussionHints || codingSession.discussionHints || [])).length ? (
                        <div className="coding-hints-panel">
                          <h3>Discussion Hints</h3>
                          <div className="student-tag-grid">
                            {(currentPeQuestion?.discussionHints || codingSession.discussionHints || []).map((hint) => (
                              <span key={hint} className="student-tag-chip">{hint}</span>
                            ))}
                          </div>
                        </div>
                      ) : null}
                    </>
                  ) : null}

                  {activeLeftTab === "editorial" ? (
                    <div className="coding-placeholder-panel">
                      <h3>Editorial</h3>
                      <p>Review the intended approach, edge cases, and implementation notes for this problem here.</p>
                    </div>
                  ) : null}

                  {activeLeftTab === "submissions" ? (
                    <div className="coding-placeholder-panel">
                      <h3>Submissions</h3>
                      <p>Your recent runs and grading attempts will appear here so you can compare outputs and scores.</p>
                    </div>
                  ) : null}

                  {activeLeftTab === "solutions" ? (
                    <div className="coding-placeholder-panel">
                      <h3>Solutions</h3>
                      <p>Reference implementations and alternative answers can be displayed here for review.</p>
                    </div>
                  ) : null}
                </div>
              </article>

              <div
                className="coding-workspace__splitter coding-workspace__splitter--vertical"
                role="separator"
                aria-label="Resize description and coding panels"
                aria-orientation="vertical"
                aria-valuemin={28}
                aria-valuemax={72}
                aria-valuenow={Math.round(codingSplit)}
                onPointerDown={(event) => handleSplitterPointerDown(event, "vertical")}
              />

              <div
                ref={codingSidePaneRef}
                className="coding-side-stack"
                style={{
                  gridTemplateRows: `minmax(280px, ${descriptionSplit}fr) 8px minmax(220px, ${100 - descriptionSplit}fr)`
                }}
              >
                <article className="student-panel coding-editor-panel coding-editor-panel--leetcode">
                  <div className="coding-editor-header">
                    <div className="coding-editor-title-row">
                      <div className="coding-editor-title">Code</div>
                      <div className="coding-editor-actions">
                        <button type="button" className="coding-utility-pill">{codingSession.language}</button>
                        <button type="button" className="coding-utility-pill">Auto</button>
                        <button type="button" className="coding-utility-pill" onClick={handleFormatCode}>Format</button>
                      </div>
                    </div>

                    <div className="coding-editor-toolbar">
                      <div className="coding-file-tabs">
                        {files.map((file) => (
                          <button key={file.name} type="button" className={`coding-file-tab${activeFile === file.name ? " is-active" : ""}`} onClick={() => setActiveFile(file.name)}>
                            <span>{file.name}</span>
                            {file.isReadonly ? <span className="coding-file-tab__lock">Lock</span> : null}
                          </button>
                        ))}
                      </div>
                      <div className="coding-editor-language">Saved</div>
                    </div>
                  </div>

                  <div className="coding-monaco-shell">
                    <div className="coding-editor coding-editor--monaco">
                      <Editor
                        key={`${selectedPeQuestionId || "session"}:${activeFile || "file"}`}
                        height="100%"
                        width="100%"
                        theme="vs-dark"
                        language={activeEditorLanguage}
                        path={activeFile || "main.txt"}
                        value={activeEditorValue}
                        options={{
                          readOnly: activeFileContent?.isReadonly,
                          minimap: { enabled: false },
                          fontSize: 15,
                          tabSize: 4,
                          automaticLayout: true,
                          scrollBeyondLastLine: false,
                          formatOnPaste: true,
                          formatOnType: true,
                          wordWrap: "on",
                          glyphMargin: true
                        }}
                        onMount={(editor, monaco) => {
                          editorRef.current = editor;
                          monacoRef.current = monaco;
                          // Do NOT call editor.setValue() here — the `value` prop + `key` already
                          // initialises the correct content. Calling setValue() would trigger
                          // onChange with a stale closure value (Q1 code) and overwrite Q2's draft.
                          const model = editor.getModel();
                          if (model) {
                            monaco.editor.setModelMarkers(
                              model,
                              "judge0",
                              markersByFile[getBaseName(activeFile)] || []
                            );
                          }
                        }}
                        onChange={(value) =>
                          setFiles((current) => {
                            const nextFiles = current.map((file, index) =>
                              index === activeFileIndex ? { ...file, content: value ?? "" } : file
                            );
                            if (currentPeQuestion?.id) {
                              setQuestionDrafts((drafts) => ({
                                ...drafts,
                                [currentPeQuestion.id]: nextFiles
                              }));
                            }
                            return nextFiles;
                          })
                        }
                      />
                    </div>
                  </div>
                </article>

                <div
                  className="coding-workspace__splitter coding-workspace__splitter--horizontal"
                  role="separator"
                  aria-label="Resize code and testcase panels"
                  aria-orientation="horizontal"
                  aria-valuemin={24}
                  aria-valuemax={76}
                  aria-valuenow={Math.round(descriptionSplit)}
                  onPointerDown={(event) => handleSplitterPointerDown(event, "horizontal")}
                />

                <article className="student-panel coding-testcase-panel">
                  <div className="coding-testcase-panel__header">
                    <div>
                      <span className="coding-testcase-panel__eyebrow">Execution</span>
                      <h3>Testcase</h3>
                    </div>
                    <div className="admin-modal__actions coding-action-row coding-action-row--leetcode">
                      <Button variant="secondary" onClick={() => handleRun("sample")} disabled={isRunning || isSubmitting}>
                        {isRunning && runMode === "sample" ? "Running Sample..." : "Run Sample"}
                      </Button>
                      <Button variant="secondary" onClick={() => handleRun("custom")} disabled={isRunning || isSubmitting || !String(stdin || "").trim()}>
                        {isRunning && runMode === "custom" ? "Running Input..." : "Run Custom"}
                      </Button>
                      <Button variant="primary" onClick={handleSubmit} disabled={isSubmitting}>{isSubmitting ? "Grading..." : "Submit"}</Button>
                    </div>
                  </div>

                  {codingError ? (
                    <div className="student-status-banner">
                      {codingError}
                    </div>
                  ) : null}

                  <div className="coding-bottom-panel">
                    <div className="coding-panel-tabs coding-panel-tabs--bottom" role="tablist" aria-label="Console tabs">
                      {BOTTOM_TABS.map((tab) => (
                        <button
                          key={tab.id}
                          type="button"
                          className={`coding-panel-tab${activeBottomTab === tab.id ? " is-active" : ""}`}
                          onClick={() => setActiveBottomTab(tab.id)}
                        >
                          {tab.label}
                        </button>
                      ))}
                    </div>

                    <div className="coding-bottom-panel__content">
                      {activeBottomTab === "stdin" ? (
                        <textarea
                          className="coding-stdin coding-stdin--leetcode"
                          value={stdin}
                          onChange={(event) => setStdin(event.target.value)}
                          placeholder="Custom input for Run mode"
                        />
                      ) : (
                        <pre className="coding-console coding-console--result">{consoleOutput}</pre>
                      )}
                    </div>
                  </div>
                </article>
              </div>
            </section>
          </div>
        </section>
      </StudentScaffold>
    );
  }

  if (!currentQuestion) {
    return null;
  }

  const progressPercent = mcqSession?.questions?.length ? Math.round(((currentIndex + 1) / mcqSession.questions.length) * 100) : 0;
  const answerText = resolveFlashcardAnswerText(currentQuestion);
  const currentRating = cardRatings[currentQuestion.id];

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentPractice}
      title="Flashcard Viewer"
      subtitle="Flip each card, review the answer, and move through the deck inside the Practice workspace."
      actions={
        <div className="ai-inline-actions">
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentExams)}>Back to Configuration</Button>
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentPracticeHistory)}>Practice History</Button>
        </div>
      }
      topbarActionLabel="Open Result"
      topbarActionHref={ROUTES.studentPracticeHistory}
      showPageHeader={false}
    >
      <section className="practice-shell">
        {practiceSidebar}
        <div className="practice-shell__content">
          <section className="flashcard-viewer">
            <header className="flashcard-viewer__header">
              <div>
                <span className="flashcard-viewer__eyebrow">Active Recall</span>
                <h1>Flashcard Viewer</h1>
                <p>Card {currentIndex + 1} of {mcqSession.questions.length}</p>
              </div>

              <div className="flashcard-viewer__stats">
                <strong>Knew: {flashcardCounters.knew}</strong>
                <span>Didn&apos;t Know: {flashcardCounters.didntKnow}</span>
                <span>Elapsed: {formatElapsedTime(elapsedSeconds)}</span>
              </div>
            </header>

            <div className="flashcard-viewer__progress" aria-hidden="true">
              <span style={{ width: `${progressPercent}%` }} />
            </div>

            <div className="flashcard-viewer__body">
              <aside className="flashcard-viewer__rail">
                <div className="flashcard-viewer__rail-copy">
                  <strong>Navigator</strong>
                  <p>Jump between cards and review in your own rhythm.</p>
                </div>

                <div className="flashcard-viewer__rail-grid">
                  {mcqSession.questions.map((question, index) => {
                    const rating = cardRatings[question.id];
                    return (
                      <button
                        key={question.id}
                        type="button"
                        className={`flashcard-viewer__rail-item${currentIndex === index ? " is-active" : ""}${rating === "knew" ? " is-knew" : ""}${rating === "didnt-know" ? " is-didnt-know" : ""}`}
                        onClick={() => goToFlashcard(index)}
                      >
                        {index + 1}
                      </button>
                    );
                  })}
                </div>
              </aside>

              <section className="flashcard-viewer__main">
                <div className="flashcard-viewer__card-wrap">
                  <button
                    type="button"
                    className={`flashcard-viewer__card${isFlipped ? " is-flipped" : ""}`}
                    onClick={() => setIsFlipped((current) => !current)}
                    onTouchStart={(event) => setTouchStartX(event.changedTouches[0]?.clientX ?? null)}
                    onTouchEnd={(event) => {
                      const endX = event.changedTouches[0]?.clientX ?? null;
                      if (touchStartX === null || endX === null) {
                        return;
                      }

                      const deltaX = endX - touchStartX;
                      if (deltaX <= -40) {
                        goToFlashcard(currentIndex + 1);
                      } else if (deltaX >= 40) {
                        goToFlashcard(currentIndex - 1);
                      }
                      setTouchStartX(null);
                    }}
                  >
                    <div className="flashcard-viewer__face flashcard-viewer__face--front">
                      <span className="flashcard-viewer__face-tag">Front</span>
                      <div className="flashcard-viewer__content">
                        <div className="flashcard-viewer__question-copy">
                          {renderMarkdownBlocks(currentQuestion.description || currentQuestion.text || "")}
                        </div>
                        {currentQuestion.options?.length ? (
                          <div className="flashcard-viewer__options">
                            {renderFlashcardOptions(currentQuestion.options)}
                          </div>
                        ) : null}
                      </div>
                      <span className="flashcard-viewer__hint">Click to flip the card</span>
                    </div>

                    <div className="flashcard-viewer__face flashcard-viewer__face--back">
                      <span className="flashcard-viewer__face-tag">Back</span>
                      <div className="flashcard-viewer__answer">
                        <strong>Correct Answer</strong>
                        <div className="flashcard-viewer__answer-pill">{answerText}</div>
                      </div>
                      <div className="flashcard-viewer__content">
                        <h3>Explanation</h3>
                        <p>{currentQuestion.explanation}</p>
                      </div>
                    </div>
                  </button>
                </div>

                <div className="flashcard-viewer__actions">
                  <div className="flashcard-viewer__actions-group">
                    <Button variant="ghost" disabled={currentIndex === 0} onClick={() => goToFlashcard(currentIndex - 1)}>
                      Previous
                    </Button>
                    <Button variant="ghost" disabled={currentIndex === mcqSession.questions.length - 1} onClick={() => goToFlashcard(currentIndex + 1)}>
                      Next
                    </Button>
                  </div>

                  <div className="flashcard-viewer__actions-group">
                    <button
                      type="button"
                      className={`flashcard-viewer__rating flashcard-viewer__rating--good${currentRating === "knew" ? " is-active" : ""}`}
                      onClick={() => markFlashcard("knew")}
                    >
                      Knew It
                    </button>
                    <button
                      type="button"
                      className={`flashcard-viewer__rating flashcard-viewer__rating--soft${currentRating === "didnt-know" ? " is-active" : ""}`}
                      onClick={() => markFlashcard("didnt-know")}
                    >
                      Didn&apos;t Know
                    </button>
                  </div>
                </div>
              </section>
            </div>
          </section>
        </div>
      </section>
    </StudentScaffold>
  );
}

