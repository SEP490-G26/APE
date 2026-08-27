import { http } from "./http";

const PE_SESSION_KEY = "ape.student.pe.session";
const PE_RESULT_KEY = "ape.student.pe.result";
const PE_SUBMISSION_HISTORY_KEY = "ape.student.pe.submissions";
const BYOS_DOCS_KEY = "ape.student.byos.documents";
const ADMIN_DOCS_KEY = "ape.admin.documents";
const MCQ_SESSION_KEY = "ape.student.mcq.session";
const PRACTICE_SESSION_HISTORY_KEY = "ape.student.practice.sessions";
const FE_EXAM_SESSION_KEY = "ape.student.fe.exam.session";

const PRACTICE_BASE = "/api/student/practice";
const STUDENT_EXAMS_BASE = "/api/student/exams";
const STUDENT_SUBMISSIONS_BASE = "/api/student/submissions";
const STUDENT_EXAM_CONFIG_BASE = "/api/student/exam-config";

const fallbackPeProblem = {
  id: "PE-001",
  examId: "",
  sessionId: "",
  questionId: "PE-001",
  selectedQuestionId: "PE-001",
  questions: [
    {
      id: "PE-001",
      title: "Reverse a Singly Linked List",
      description: [
        "Given the `head` of a singly linked list, reverse the list and return the new head.",
        "",
        "## Requirements",
        "- Implement the reversal in `Solution.java`.",
        "- Keep helper structures unchanged if they are part of the provided runtime skeleton.",
        "- Your solution should handle an empty list and a single-node list."
      ].join("\n"),
      difficulty: "Medium",
      topicTags: ["Linked List", "Pointers", "Traversal"],
      constraints: [],
      samples: [
        { input: "[1,2,3,4,5]", output: "[5,4,3,2,1]", explanation: "Reverse the next pointer of every node." }
      ],
      discussionHints: [],
      skeletonFiles: [
        {
          name: "Solution.java",
          content: "class Solution {\n  public ListNode solve(ListNode head) {\n    return head;\n  }\n}\n",
          isReadonly: false
        }
      ]
    }
  ],
  title: "Reverse a Singly Linked List",
  description: [
    "Given the `head` of a singly linked list, reverse the list and return the new head.",
    "",
    "## Requirements",
    "- Implement the reversal in `Solution.java`.",
    "- Keep helper structures unchanged if they are part of the provided runtime skeleton.",
    "- Your solution should handle an empty list and a single-node list."
  ].join("\n"),
  difficulty: "Medium",
  language: "Java",
  topicTags: ["Linked List", "Pointers", "Traversal"],
  constraints: [],
  samples: [
    { input: "[1,2,3,4,5]", output: "[5,4,3,2,1]", explanation: "Reverse the next pointer of every node." }
  ],
  discussionHints: [],
  skeletonFiles: [
    {
      name: "Solution.java",
      content: "class Solution {\n  public ListNode solve(ListNode head) {\n    return head;\n  }\n}\n",
      isReadonly: false
    }
  ]
};

const fallbackResult = {
  score: 0,
  status: "Pending",
  mentorHintsUsed: 0,
  testCases: [],
  codeReview: {
    errorAnalysis: "Submit a solution to receive server-side grading feedback.",
    qualityScore: "Pending",
    suggestions: [
      "Review the problem statement and edge cases.",
      "Run sample input before submitting.",
      "Check naming and control flow in your implementation."
    ],
    relatedConcepts: ["#ProblemSolving", "#Debugging", "#TestCases"]
  }
};

function readJson(key, fallback) {
  const raw = window.localStorage.getItem(key);
  if (!raw) {
    return fallback;
  }

  try {
    return JSON.parse(raw);
  } catch {
    return fallback;
  }
}

function writeJson(key, value) {
  window.localStorage.setItem(key, JSON.stringify(value));
}

function hasMeaningfulFileContent(files = []) {
  return Array.isArray(files) && files.some((file) => String(file?.content || "").trim().length > 0);
}

function mergeDraftFiles(preferredFiles = [], fallbackFiles = []) {
  if (hasMeaningfulFileContent(preferredFiles)) {
    return preferredFiles;
  }

  return Array.isArray(fallbackFiles) ? fallbackFiles : [];
}

function normalizePracticeSessionPayload(session) {
  return {
    sessionId: session?.id ?? session?.Id ?? session?.sessionId ?? session?.SessionId ?? "",
    examId: session?.examId ?? session?.ExamId ?? "",
    startTime: session?.startTime ?? session?.StartTime ?? new Date().toISOString(),
    endTime: session?.endTime ?? session?.EndTime ?? null,
    activeDurationSeconds: Number(session?.activeDurationSeconds ?? session?.ActiveDurationSeconds ?? 0),
    lastResumedAt: session?.lastResumedAt ?? session?.LastResumedAt ?? null,
    lastPausedAt: session?.lastPausedAt ?? session?.LastPausedAt ?? null,
    isPaused: Boolean(session?.isPaused ?? session?.IsPaused ?? false),
    selectedQuestionId: session?.selectedQuestionId ?? session?.SelectedQuestionId ?? "",
    draftCodes: Array.isArray(session?.draftCodes ?? session?.DraftCodes)
      ? (session?.draftCodes ?? session?.DraftCodes).map((item) => ({
          questionId: item?.questionId ?? item?.QuestionId ?? "",
          files: Array.isArray(item?.files ?? item?.Files)
            ? (item?.files ?? item?.Files).map((file) => ({
                name: file?.filename ?? file?.Filename ?? file?.name ?? "Main.java",
                content: file?.content ?? file?.Content ?? "",
                isReadonly: Boolean(file?.isReadonly ?? file?.IsReadonly ?? false)
              }))
            : []
        }))
      : []
  };
}

function buildTimedSessionFields(source = {}, fallbackStartTime = null) {
  const startTime = source?.startTime ?? source?.StartTime ?? fallbackStartTime ?? new Date().toISOString();
  return {
    startTime,
    startedAt: source?.startedAt ?? source?.StartedAt ?? startTime,
    endTime: source?.endTime ?? source?.EndTime ?? null,
    activeDurationSeconds: Number(source?.activeDurationSeconds ?? source?.ActiveDurationSeconds ?? 0),
    isPaused: Boolean(source?.isPaused ?? source?.IsPaused ?? false),
    lastResumedAt: source?.lastResumedAt ?? source?.LastResumedAt ?? startTime,
    lastPausedAt: source?.lastPausedAt ?? source?.LastPausedAt ?? null,
    status: source?.status ?? source?.Status ?? ""
  };
}

function applyPeDraftCodes(questions = [], draftCodes = []) {
  return questions.map((question) => {
    const matchedDraft = draftCodes.find((item) => item?.questionId === question.id);
    return {
      ...question,
      skeletonFiles: mergeDraftFiles(matchedDraft?.files, question.skeletonFiles)
    };
  });
}

async function getPracticeSession(sessionId) {
  if (!sessionId) {
    return null;
  }

  const payload = await http(`${PRACTICE_BASE}/sessions/${sessionId}`);
  return normalizePracticeSessionPayload(unwrapPayload(payload));
}

function wait(ms = 180) {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

function unwrapPayload(payload) {
  return payload?.data || payload;
}

function inferLanguageFromFiles(files = []) {
  const filename = files.find((file) => !file.isReadonly)?.name || files[0]?.name || "";
  const ext = filename.split(".").pop()?.toLowerCase();

  switch (ext) {
    case "c":
      return "C";
    case "cpp":
    case "cc":
    case "cxx":
      return "C++";
    case "py":
      return "Python";
    case "java":
    default:
      return "Java";
  }
}

function mapSkeletonFiles(files = []) {
  return files.map((file) => ({
    name: file?.filename ?? file?.Filename ?? "Main.java",
    content: file?.content ?? file?.Content ?? "",
    isReadonly: Boolean(file?.isReadOnly ?? file?.IsReadOnly ?? false)
  }));
}

function mapSamples(testCases = []) {
  return testCases.map((item) => ({
    input: item?.input ?? item?.Input ?? "",
    output: item?.expectedOutput ?? item?.ExpectedOutput ?? "",
    explanation: item?.isHidden || item?.IsHidden ? "Hidden test case." : "Sample test case from the exam."
  }));
}

function mapExecutionDiagnostics(items) {
  if (!Array.isArray(items)) {
    return [];
  }

  return items.map((item) => ({
    category: item?.category ?? item?.Category ?? "",
    code: item?.code ?? item?.Code ?? "",
    severity: item?.severity ?? item?.Severity ?? "",
    title: item?.title ?? item?.Title ?? "",
    message: item?.message ?? item?.Message ?? "",
    filename: item?.filename ?? item?.Filename ?? "",
    line: item?.line ?? item?.Line ?? null,
    column: item?.column ?? item?.Column ?? null
  }));
}

function mapExecutionCase(item, fallback = {}) {
  if (!item) {
    return null;
  }

  return {
    index: Number(item?.index ?? item?.Index ?? fallback.index ?? 0),
    number: Number(item?.number ?? item?.Number ?? fallback.number ?? item?.index ?? item?.Index ?? 0),
    label: item?.label ?? item?.Label ?? fallback.label ?? "",
    status: item?.status ?? item?.Status ?? fallback.status ?? "Unknown",
    passed: Boolean(item?.passed ?? item?.Passed ?? fallback.passed ?? false),
    executionStatus: item?.executionStatus ?? item?.ExecutionStatus ?? fallback.executionStatus ?? "",
    isJudged: Boolean(item?.isJudged ?? item?.IsJudged ?? fallback.isJudged ?? true),
    isHidden: Boolean(item?.isHidden ?? item?.IsHidden ?? fallback.isHidden ?? false),
    input: item?.input ?? item?.Input ?? fallback.input ?? "",
    expectedOutput: item?.expectedOutput ?? item?.ExpectedOutput ?? fallback.expectedOutput ?? "",
    actualOutput: item?.actualOutput ?? item?.ActualOutput ?? fallback.actualOutput ?? "",
    stderr: item?.stderr ?? item?.Stderr ?? fallback.stderr ?? "",
    compileOutput: item?.compileOutput ?? item?.CompileOutput ?? fallback.compileOutput ?? "",
    runtimeMs: Number(item?.runtimeMs ?? item?.RuntimeMs ?? fallback.runtimeMs ?? 0),
    memoryKb: Number(item?.memoryKb ?? item?.MemoryKb ?? fallback.memoryKb ?? 0),
    diagnostics: mapExecutionDiagnostics(item?.diagnostics ?? item?.Diagnostics)
  };
}

function mapPeQuestion(question, fallbackDifficulty, fallbackTags) {
  const skeletonFiles = mapSkeletonFiles(question?.skeletonCode || question?.SkeletonCode || question?.skeletonFiles || question?.SkeletonFiles || []);
  return {
    id: question?.id ?? question?.Id ?? "",
    title: question?.title ?? question?.Title ?? "Untitled question",
    description: question?.description ?? question?.Description ?? "",
    difficulty: question?.difficulty ?? question?.Difficulty ?? fallbackDifficulty ?? "Medium",
    topicTags: Array.isArray(question?.topicTags ?? question?.TopicTags)
      ? (question?.topicTags ?? question?.TopicTags)
      : fallbackTags,
    constraints: [],
    samples: mapSamples(question?.testCases || question?.TestCases || question?.samples || question?.Samples || []),
    discussionHints: [],
    skeletonFiles: skeletonFiles.length ? skeletonFiles : fallbackPeProblem.skeletonFiles
  };
}

function mapSubmissionResult(submission, fallbackFiles = []) {
  const rawStatus = submission?.status ?? submission?.Status ?? "Pending";
  const total = submission?.totalTestCase ?? submission?.TotalTestCase ?? submission?.totalTestCases ?? submission?.TotalTestCases ?? 0;
  const passed = submission?.testCasesPassed ?? submission?.TestCasesPassed ?? 0;
  const scoreRaw = submission?.questionScore ?? submission?.QuestionScore ?? 0;
  const runtime = submission?.runtimeMs ?? submission?.RuntimeMs ?? 0;
  const memory = submission?.memoryKb ?? submission?.MemoryKb ?? 0;
  const rawItems = submission?.testResultItems ?? submission?.TestResultItems ?? submission?.testResults ?? submission?.TestResults ?? [];

  return {
    submissionId: submission?.id ?? submission?.Id ?? "",
    submittedAt: submission?.submitTime ?? submission?.SubmitTime ?? submission?.submittedAt ?? submission?.SubmittedAt ?? new Date().toISOString(),
    score: total > 0 ? Number(((passed / total) * 100).toFixed(1)) : Number((scoreRaw * 10).toFixed(1)),
    status: submission?.finalVerdict ?? submission?.FinalVerdict ?? rawStatus,
    mentorHintsUsed: 0,
    files: fallbackFiles,
    testCases: rawItems.map((item, index) => ({
      index: (item?.testCaseIndex ?? item?.TestCaseIndex ?? index) + 1,
      verdict: item?.verdict ?? item?.Verdict ?? item?.status ?? item?.Status ?? "Pending",
      runtime: item?.runtimeMs ?? item?.RuntimeMs ?? runtime,
      memory: item?.memoryKb ?? item?.MemoryKb ?? memory,
      hidden: Boolean(item?.isHidden ?? item?.IsHidden ?? false)
    })),
    codeReview: {
      errorAnalysis:
        (submission?.finalVerdict ?? submission?.FinalVerdict ?? rawStatus) === "Accepted"
          ? "The solution passed all graded test cases."
          : "Review failed test cases and compare your output against the expected behavior.",
      qualityScore: total > 0 ? `${passed}/${total} tests passed` : "Pending",
      suggestions: [
        "Inspect sample and hidden edge cases carefully.",
        "Use the runtime and memory numbers to spot inefficiencies.",
        "Resubmit after fixing the first failing verdict."
      ],
      relatedConcepts: ["#Judge0", "#EdgeCases", "#Refactor"]
    }
  };
}

async function pollPeSubmissionResult(submissionId, files) {
  const maxAttempts = 20;

  for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
    const payload = await http(`${STUDENT_SUBMISSIONS_BASE}/pe/${submissionId}`);
    const data = unwrapPayload(payload);
    const status = String(data?.status ?? data?.Status ?? "Pending");

    if (status !== "Pending" && status !== "Processing" && status !== "Grading") {
      return mapSubmissionResult(data, files);
    }

    await wait(1500);
  }

  const payload = await http(`${STUDENT_SUBMISSIONS_BASE}/pe/${submissionId}`);
  return mapSubmissionResult(unwrapPayload(payload), files);
}

async function createPeSubmission({ sessionId, questionId, files }) {
  const payload = await http(`${STUDENT_SUBMISSIONS_BASE}/pe`, {
    method: "POST",
    body: JSON.stringify({
      sessionId,
      questionId,
      files: (files || []).map((file) => ({
        filename: file.name,
        content: file.content
      }))
    })
  });

  return unwrapPayload(payload);
}

export async function createPePracticeSession(payload) {
  if (!payload?.examId) {
    throw new Error("examId is required.");
  }

  const startPayload = await http(`${PRACTICE_BASE}/start`, {
    method: "POST",
    body: JSON.stringify({ examId: payload.examId, forceNew: true })
  });
  const startData = unwrapPayload(startPayload);

  const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${payload.examId}`);
  const detail = unwrapPayload(detailPayload);
  const rawPeQuestions = Array.isArray(detail?.peQuestions) ? detail.peQuestions : [];
  const questions = rawPeQuestions.map((question) =>
    mapPeQuestion(question, payload?.difficulty, payload?.topicTags?.length ? payload.topicTags : fallbackPeProblem.topicTags)
  );
  const peQuestion = questions[0] || null;

  if (!peQuestion) {
    throw new Error("This exam does not contain any PE question.");
  }

  const session = {
    id: peQuestion.id,
    examId: detail?.id ?? payload.examId,
    sessionId: startData?.sessionId ?? "",
    ...buildTimedSessionFields(startData, startData?.startTime ?? new Date().toISOString()),
    questionId: peQuestion.id,
    selectedQuestionId: peQuestion.id,
    questions,
    title: peQuestion.title || payload.title || fallbackPeProblem.title,
    description: peQuestion.description || fallbackPeProblem.description,
    difficulty: payload?.difficulty || fallbackPeProblem.difficulty,
    language: payload?.language || inferLanguageFromFiles(peQuestion.skeletonFiles),
    topicTags: payload?.topicTags?.length ? payload.topicTags : fallbackPeProblem.topicTags,
    constraints: payload?.constraints || [],
    samples: peQuestion.samples || [],
    discussionHints: [],
    skeletonFiles: peQuestion.skeletonFiles?.length ? peQuestion.skeletonFiles : fallbackPeProblem.skeletonFiles
  };

  writeJson(PE_SESSION_KEY, session);
  return session;
}

export async function getPePracticeSession() {
  const stored = readJson(PE_SESSION_KEY, null);
  if (!stored?.examId || !stored?.sessionId) {
    return fallbackPeProblem;
  }

  let liveSession = await getPracticeSession(stored.sessionId).catch(() => null);
  if (liveSession?.isPaused) {
    const resumedPayload = await http(`${PRACTICE_BASE}/sessions/${stored.sessionId}/resume`, {
      method: "POST"
    }).catch(() => null);

    if (resumedPayload) {
      liveSession = normalizePracticeSessionPayload(unwrapPayload(resumedPayload));
    }
  }

  const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${stored.examId}`);
  const detail = unwrapPayload(detailPayload);
  const source = { ...stored, ...liveSession };
  const hydrated = buildPeSessionFromExamDetail(detail, stored.sessionId, source);
  const selectedQuestionId = source.selectedQuestionId || hydrated.selectedQuestionId;
  const questions = hydrated.questions.map((question) => {
    const storedQuestion = Array.isArray(source.questions)
      ? source.questions.find((item) => item?.id === question.id)
      : null;

    return {
      ...question,
      skeletonFiles: mergeDraftFiles(storedQuestion?.skeletonFiles, question.skeletonFiles)
    };
  });

  const activeQuestion = questions.find((question) => question.id === selectedQuestionId) || questions[0] || null;
  const session = {
    ...hydrated,
    selectedQuestionId: activeQuestion?.id || hydrated.selectedQuestionId,
    questions,
    skeletonFiles: mergeDraftFiles(source.skeletonFiles, activeQuestion?.skeletonFiles || hydrated.skeletonFiles)
  };

  writeJson(PE_SESSION_KEY, session);
  return session;
}

export function savePePracticeSessionSnapshot(session) {
  if (!session) {
    return null;
  }

  writeJson(PE_SESSION_KEY, session);
  return session;
}

export function saveFeExamSessionSnapshot(session) {
  if (!session) {
    return null;
  }

  writeJson(FE_EXAM_SESSION_KEY, session);
  return session;
}

export async function savePeDraftCode({ sessionId, questionId, selectedQuestionId, files }) {
  if (!sessionId || !questionId) {
    throw new Error("sessionId and questionId are required.");
  }

  const payload = await http(`${PRACTICE_BASE}/sessions/${sessionId}/draft-code`, {
    method: "PUT",
    body: JSON.stringify({
      questionId,
      selectedQuestionId: selectedQuestionId || questionId,
      files: (files || []).map((file) => ({
        filename: file.name,
        content: file.content
      }))
    })
  });

  return unwrapPayload(payload);
}

export async function pausePePracticeSession(sessionId, options = {}) {
  if (!sessionId) {
    return false;
  }

  const payload = await http(`${PRACTICE_BASE}/sessions/${sessionId}/pause`, {
    method: "POST",
    ...(options.keepalive ? { keepalive: true } : {})
  });

  return Boolean(unwrapPayload(payload));
}

export async function pauseMcqPracticeSession(sessionId, options = {}) {
  if (!sessionId) {
    return false;
  }

  const payload = await http(`${PRACTICE_BASE}/sessions/${sessionId}/pause`, {
    method: "POST",
    ...(options.keepalive ? { keepalive: true } : {})
  });

  return Boolean(unwrapPayload(payload));
}

export async function resumePePracticeSession({ examId, sessionId, examTitle, courseId }) {
  if (!examId || !sessionId) {
    throw new Error("examId and sessionId are required.");
  }

  const resumedPayload = await http(`${PRACTICE_BASE}/sessions/${sessionId}/resume`, {
    method: "POST"
  });
  const resumedSession = normalizePracticeSessionPayload(unwrapPayload(resumedPayload));
  const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${examId}`);
  const detail = unwrapPayload(detailPayload);
  const session = buildPeSessionFromExamDetail(detail, sessionId, {
    ...resumedSession,
    examId,
    title: examTitle,
    courseId
  });

  writeJson(PE_SESSION_KEY, session);
  return session;
}

export async function getActivePracticeSession(examId) {
  if (!examId) {
    return null;
  }

  const payload = await http(`${PRACTICE_BASE}/exams/${examId}/active-session`);
  return unwrapPayload(payload);
}

export async function runPeCode({ questionId, files, stdin }) {
  const payload = await http(`${STUDENT_SUBMISSIONS_BASE}/pe/run`, {
    method: "POST",
    body: JSON.stringify({
      questionId,
      stdin,
      files: (files || []).map((file) => ({
        filename: file.name,
        content: file.content
      }))
    })
  });

  const data = unwrapPayload(payload);
  const rawCustomRunResult = data?.customRunResult ?? data?.CustomRunResult ?? null;
  const fallbackStatus = data?.status || "Unknown";
  const rawSampleResults = data?.sampleResults ?? data?.SampleResults ?? [];

  return {
    status: fallbackStatus,
    stdout: data?.stdout || "",
    stderr: data?.stderr || "",
    compileOutput: data?.compileOutput || "",
    runtimeMs: data?.runtimeMs || 0,
    memoryKb: data?.memoryKb || 0,
    executionStatus: data?.executionStatus ?? data?.ExecutionStatus ?? "",
    isJudged: Boolean(data?.isJudged ?? data?.IsJudged ?? true),
    diagnostics: mapExecutionDiagnostics(data?.diagnostics ?? data?.Diagnostics),
    passedSampleCases: Number(data?.passedSampleCases ?? data?.PassedSampleCases ?? 0),
    totalSampleCases: Number(data?.totalSampleCases ?? data?.TotalSampleCases ?? 0),
    sampleResults: Array.isArray(rawSampleResults)
      ? rawSampleResults.map((item, index) => mapExecutionCase(item, {
          index: index + 1,
          number: index + 1,
          label: `Sample ${index + 1}`,
          status: fallbackStatus
        })).filter(Boolean)
      : [],
    customRunResult: rawCustomRunResult
      ? mapExecutionCase(rawCustomRunResult, {
          label: "Custom input",
          status: fallbackStatus,
          input: stdin ?? "",
          runtimeMs: data?.runtimeMs ?? 0,
          memoryKb: data?.memoryKb ?? 0,
          isJudged: false
        })
      : null,
    files
  };
}

function buildPeSessionFromExamDetail(detail, sessionId, payload = {}) {
  const rawPeQuestions = Array.isArray(detail?.peQuestions ?? detail?.PEQuestions) ? (detail?.peQuestions ?? detail?.PEQuestions) : [];
  const questions = rawPeQuestions.map((question) =>
    mapPeQuestion(question, payload?.difficulty, payload?.topicTags?.length ? payload.topicTags : fallbackPeProblem.topicTags)
  );
  const peQuestion = questions[0] || null;

  if (!peQuestion) {
    throw new Error("This exam does not contain any PE question.");
  }

  return {
    id: peQuestion.id,
    examId: detail?.id ?? detail?.Id ?? "",
    sessionId,
    startTime: payload?.startTime ?? payload?.startedAt ?? new Date().toISOString(),
    activeDurationSeconds: Number(payload?.activeDurationSeconds ?? 0),
    isPaused: Boolean(payload?.isPaused ?? false),
    lastResumedAt: payload?.lastResumedAt ?? null,
    lastPausedAt: payload?.lastPausedAt ?? null,
    questionId: peQuestion.id,
    selectedQuestionId: payload?.selectedQuestionId || peQuestion.id,
    questions: applyPeDraftCodes(questions, payload?.draftCodes || []),
    title: peQuestion.title || payload.title || fallbackPeProblem.title,
    description: peQuestion.description || fallbackPeProblem.description,
    difficulty: payload?.difficulty || peQuestion.difficulty || fallbackPeProblem.difficulty,
    language: payload?.language || inferLanguageFromFiles(peQuestion.skeletonFiles),
    topicTags: payload?.topicTags?.length ? payload.topicTags : (peQuestion.topicTags || fallbackPeProblem.topicTags),
    constraints: payload?.constraints || [],
    samples: peQuestion.samples || [],
    discussionHints: [],
    skeletonFiles: peQuestion.skeletonFiles?.length ? peQuestion.skeletonFiles : fallbackPeProblem.skeletonFiles
  };
}

export async function submitPeCode({ sessionId, questions, draftFilesByQuestion = {} }) {
  const normalizedQuestions = Array.isArray(questions) ? questions.filter((item) => item?.id) : [];
  if (!sessionId || !normalizedQuestions.length) {
    throw new Error("sessionId and at least one PE question are required.");
  }

  const acceptedList = [];
  for (const question of normalizedQuestions) {
    const files = draftFilesByQuestion[question.id] || question.skeletonFiles || [];
    const accepted = await createPeSubmission({
      sessionId,
      questionId: question.id,
      files
    });

    acceptedList.push({
      questionId: question.id,
      files,
      accepted
    });
  }

  const detailResults = await Promise.all(acceptedList.map(async (item) => {
    const polled = await pollPeSubmissionResult(item.accepted?.submissionId, item.files);
    return {
      ...polled,
      questionId: item.questionId
    };
  }));

  const currentHistory = readJson(PE_SUBMISSION_HISTORY_KEY, []);

  if (sessionId) {
    try {
      await http(`${PRACTICE_BASE}/sessions/${sessionId}/end`, {
        method: "POST"
      });
    } catch {
      // keep grading result even if end-session sync fails
    }
  }

  const sessionPayload = await http(`${STUDENT_SUBMISSIONS_BASE}/session/${sessionId}`);
  const sessionData = unwrapPayload(sessionPayload);
  const result = await buildPeSessionViewerResult(sessionId, sessionData, normalizedQuestions);
  if (!result.questionResults?.length) {
    result.questionResults = detailResults;
  }

  writeJson(PE_RESULT_KEY, result);
  writeJson(PE_SUBMISSION_HISTORY_KEY, [result, ...currentHistory]);
  return result;
}

export async function getPeSubmissionResult() {
  return readJson(PE_RESULT_KEY, fallbackResult);
}

export async function openPeHistoryResult(sessionId, submissionId = "") {
  if (!sessionId && !submissionId) {
    throw new Error("sessionId or submissionId is required.");
  }

  if (sessionId) {
    const sessionPayload = await http(`${STUDENT_SUBMISSIONS_BASE}/session/${sessionId}`);
    const sessionData = unwrapPayload(sessionPayload);
    const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${sessionData?.examId ?? sessionData?.ExamId}`);
    const examDetail = unwrapPayload(detailPayload);
    const questionMeta = Array.isArray(examDetail?.peQuestions ?? examDetail?.PEQuestions)
      ? (examDetail?.peQuestions ?? examDetail?.PEQuestions).map((question) => mapPeQuestion(question, question?.difficulty, question?.topicTags || []))
      : [];
    const result = await buildPeSessionViewerResult(sessionId, sessionData, questionMeta);
    writeJson(PE_RESULT_KEY, result);
    return result;
  }

  const resolvedSubmissionId = String(submissionId || "").trim();
  if (!resolvedSubmissionId) {
    throw new Error("Cannot determine which PE submission should be opened.");
  }

  const detailPayload = await http(`${STUDENT_SUBMISSIONS_BASE}/pe/${resolvedSubmissionId}`);
  const detail = unwrapPayload(detailPayload);
  const result = mapPeDetailToViewerResult(detail);
  writeJson(PE_RESULT_KEY, result);
  return result;
}

export async function getMcqSession(mode = "Practice") {
  await wait();
  const fallback = {
    mode,
    questions: [
      {
        id: "Q1",
        description: "## LIFO Principle\nWhich data structure best supports **Last-In-First-Out (LIFO)** behavior?",
        options: ["Queue", "Stack", "Tree", "Graph"],
        answer: 1,
        explanation: "A stack follows Last-In, First-Out semantics: the last item pushed is the first one popped."
      },
      {
        id: "Q2",
        description: "## React State\nWhich React hook is used for local component state?",
        options: ["useMemo", "useRef", "useState", "useId"],
        answer: 2,
        explanation: "useState is the standard hook for local state in function components."
      },
      {
        id: "Q3",
        description: "## Time Complexity\nWhich notation is commonly used to describe upper-bound time complexity?",
        options: ["Big O", "Big Theta", "Big Omega", "Little o"],
        answer: 0,
        explanation: "Big O notation expresses the upper bound of an algorithm's growth rate."
      },
      {
        id: "Q4",
        description: "## HTTP Methods\nWhich HTTP method is typically used to update an existing resource?",
        options: ["GET", "POST", "PUT", "TRACE"],
        answer: 2,
        explanation: "PUT is commonly used to replace or update an existing resource."
      },
      {
        id: "Q5",
        description: "## Database Keys\nWhich key uniquely identifies each row in a relational table?",
        options: ["Foreign key", "Primary key", "Composite key", "Candidate key"],
        answer: 1,
        explanation: "A primary key uniquely identifies each row in a table."
      }
    ]
  };
  const stored = readJson(MCQ_SESSION_KEY, null);
  if (!stored?.examId || !stored?.sessionId) {
    return fallback;
  }

  try {
    const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${stored.examId}`);
    const detail = unwrapPayload(detailPayload);
    const questions = Array.isArray(detail?.feQuestions ?? detail?.FEQuestions)
      ? (detail?.feQuestions ?? detail?.FEQuestions).map(mapFeQuestion)
      : [];

    if (!questions.length) {
      return fallback;
    }

    const session = {
      ...stored,
      mode: stored.mode || mode,
      title: detail?.title ?? detail?.Title ?? stored.title ?? "Flashcard Session",
      courseId: detail?.courseId ?? detail?.CourseId ?? stored.courseId ?? "",
      questions
    };

    writeJson(MCQ_SESSION_KEY, session);
    return session;
  } catch {
    return stored || fallback;
  }
}

function mapFeQuestion(question, index) {
  const options = Array.isArray(question?.options ?? question?.Options)
    ? (question?.options ?? question?.Options)
    : [];
  const rawAnswer = question?.answer ?? question?.Answer ?? question?.correctAnswer ?? question?.CorrectAnswer;
  const answerList = Array.isArray(rawAnswer)
    ? rawAnswer
    : (rawAnswer !== null && rawAnswer !== undefined ? [rawAnswer] : []);
  const normalizedAnswer = typeof rawAnswer === "number"
    ? rawAnswer
    : answerList.reduce((foundIndex, entry) => {
      if (foundIndex >= 0) {
        return foundIndex;
      }

      if (typeof entry !== "string" || entry.trim() === "") {
        return -1;
      }

      const trimmedEntry = entry.trim();
      const labelMatch = /^[A-Z]$/i.test(trimmedEntry)
        ? trimmedEntry.toUpperCase().charCodeAt(0) - 65
        : -1;

      if (labelMatch >= 0 && labelMatch < options.length) {
        return labelMatch;
      }

      return options.findIndex((option) => option === trimmedEntry);
    }, -1);

  return {
    id: question?.id ?? question?.Id ?? `Q${index + 1}`,
    title: question?.title ?? question?.Title ?? `Question ${index + 1}`,
    description: question?.description ?? question?.Description ?? "",
    options,
    answer: normalizedAnswer >= 0 ? normalizedAnswer : null,
    correctAnswer: answerList
      .map((entry) => {
        if (typeof entry !== "string") {
          return null;
        }

        const trimmedEntry = entry.trim();
        const labelMatch = /^[A-Z]$/i.test(trimmedEntry)
          ? trimmedEntry.toUpperCase().charCodeAt(0) - 65
          : -1;

        if (labelMatch >= 0 && labelMatch < options.length) {
          return options[labelMatch];
        }

        return trimmedEntry || null;
      })
      .filter(Boolean),
    explanation: question?.explanation ?? question?.Explanation ?? "",
    difficulty: question?.difficulty ?? question?.Difficulty ?? "Medium",
    topicTags: Array.isArray(question?.topicTags ?? question?.TopicTags)
      ? (question?.topicTags ?? question?.TopicTags)
      : []
  };
}

function mapPeDetailToViewerResult(detail) {
  const testResults = Array.isArray(detail?.testResults ?? detail?.TestResults)
    ? (detail?.testResults ?? detail?.TestResults)
    : [];
  const total = Number(detail?.totalTestCases ?? detail?.TotalTestCases ?? testResults.length ?? 0);
  const passed = Number(detail?.testCasesPassed ?? detail?.TestCasesPassed ?? 0);
  const maxScore = Number(detail?.maxScore ?? detail?.MaxScore ?? 0);
  const earnedScore = Number(detail?.questionScore ?? detail?.QuestionScore ?? 0);
  const percent = maxScore > 0
    ? Number(((earnedScore / maxScore) * 100).toFixed(1))
    : (total > 0 ? Number(((passed / total) * 100).toFixed(1)) : 0);

  return {
    submissionId: detail?.id ?? detail?.Id ?? "",
    questionId: detail?.questionId ?? detail?.QuestionId ?? "",
    submittedAt: detail?.submittedAt ?? detail?.SubmittedAt ?? new Date().toISOString(),
    score: percent,
    earnedScore,
    maxScore,
    status: detail?.finalVerdict ?? detail?.FinalVerdict ?? detail?.status ?? detail?.Status ?? "Pending",
    executionStatus: detail?.executionStatus ?? detail?.ExecutionStatus ?? "",
    diagnostics: mapExecutionDiagnostics(detail?.diagnostics ?? detail?.Diagnostics),
    mentorHintsUsed: 0,
    files: Array.isArray(detail?.submittedCode ?? detail?.SubmittedCode)
      ? (detail?.submittedCode ?? detail?.SubmittedCode).map((file) => ({
        name: file?.filename ?? file?.Filename ?? "Main.java",
        content: file?.content ?? file?.Content ?? ""
      }))
      : [],
    testCases: testResults.map((item, index) => ({
      index: Number(item?.testCaseIndex ?? item?.TestCaseIndex ?? index) + 1,
      number: Number(item?.number ?? item?.Number ?? item?.testCaseIndex ?? item?.TestCaseIndex ?? index) + 1,
      label: item?.label ?? item?.Label ?? `Test ${index + 1}`,
      verdict: item?.verdict ?? item?.Verdict ?? (item?.passed ?? item?.Passed ? "Accepted" : "WrongAnswer"),
      executionStatus: item?.executionStatus ?? item?.ExecutionStatus ?? "",
      isJudged: Boolean(item?.isJudged ?? item?.IsJudged ?? true),
      runtime: Number(item?.runtimeMs ?? item?.RuntimeMs ?? item?.timeMs ?? item?.TimeMs ?? 0),
      memory: Number(item?.memoryKb ?? item?.MemoryKb ?? 0),
      hidden: Boolean(item?.isHidden ?? item?.IsHidden ?? false),
      input: item?.input ?? item?.Input ?? "",
      expectedOutput: item?.expectedOutput ?? item?.ExpectedOutput ?? "",
      actualOutput: item?.actualOutput ?? item?.ActualOutput ?? "",
      stderr: item?.standardError ?? item?.StandardError ?? "",
      compileOutput: item?.compileOutput ?? item?.CompileOutput ?? "",
      diagnostics: mapExecutionDiagnostics(item?.diagnostics ?? item?.Diagnostics)
    })),
    codeReview: {
      errorAnalysis:
        detail?.processingError ?? detail?.ProcessingError ?? (
          passed === total && total > 0
            ? "The solution passed all graded test cases."
            : "Review failed test cases and compare your output against the expected behavior."
        ),
      qualityScore: total > 0 ? `${passed}/${total} tests passed` : "Pending",
      suggestions: [
        "Inspect the first failing test case before resubmitting.",
        "Check edge cases and output formatting carefully.",
        "Optimize runtime and memory after correctness is stable."
      ],
      relatedConcepts: ["#Judge0", "#TestCases", "#Debugging"]
    }
  };
}

export async function createFeExamSession({ examId, examTitle, courseId, forceNew = false }) {
  if (!examId) {
    throw new Error("examId is required.");
  }

  const startPayload = await http(`${PRACTICE_BASE}/start`, {
    method: "POST",
    body: JSON.stringify({ examId, forceNew: Boolean(forceNew) })
  });
  const startData = unwrapPayload(startPayload);

  const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${examId}`);
  const detail = unwrapPayload(detailPayload);
  const questions = Array.isArray(detail?.feQuestions ?? detail?.FEQuestions)
    ? (detail?.feQuestions ?? detail?.FEQuestions).map(mapFeQuestion)
    : [];

  if (!questions.length) {
    throw new Error("This exam does not contain any FE question.");
  }

  const session = {
    examId,
    sessionId: startData?.sessionId ?? "",
    ...buildTimedSessionFields(startData, startData?.startTime ?? new Date().toISOString()),
    title: detail?.title ?? detail?.Title ?? examTitle ?? "FE Exam",
    courseId: detail?.courseId ?? detail?.CourseId ?? courseId ?? "",
    questions,
    currentIndex: 0,
    selectedAnswers: {}
  };

  writeJson(FE_EXAM_SESSION_KEY, session);
  return session;
}

export async function createFlashcardSession({ examId, examTitle, courseId, forceNew = false }) {
  if (!examId) {
    throw new Error("examId is required.");
  }

  const startPayload = await http(`${PRACTICE_BASE}/start`, {
    method: "POST",
    body: JSON.stringify({ examId, forceNew: Boolean(forceNew) })
  });
  const startData = unwrapPayload(startPayload);

  const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${examId}`);
  const detail = unwrapPayload(detailPayload);
  const questions = Array.isArray(detail?.feQuestions ?? detail?.FEQuestions)
    ? (detail?.feQuestions ?? detail?.FEQuestions).map(mapFeQuestion)
    : [];

  if (!questions.length) {
    throw new Error("This flashcard exam does not contain any FE question.");
  }

  const session = {
    mode: "Flashcard",
    examId,
    sessionId: startData?.sessionId ?? "",
    ...buildTimedSessionFields(startData, startData?.startTime ?? new Date().toISOString()),
    title: detail?.title ?? detail?.Title ?? examTitle ?? "Flashcard Session",
    courseId: detail?.courseId ?? detail?.CourseId ?? courseId ?? "",
    questions,
    currentIndex: 0
  };

  writeJson(MCQ_SESSION_KEY, session);
  return session;
}

function buildFeSessionFromExamDetail(detail, sessionId, fallback = {}) {
  const questions = Array.isArray(detail?.feQuestions ?? detail?.FEQuestions)
    ? (detail?.feQuestions ?? detail?.FEQuestions).map(mapFeQuestion)
    : [];

  if (!questions.length) {
    throw new Error("This exam does not contain any FE question.");
  }

  return {
    examId: detail?.id ?? detail?.Id ?? fallback.examId ?? "",
    sessionId,
    ...buildTimedSessionFields(fallback, fallback?.startTime ?? fallback?.startedAt ?? new Date().toISOString()),
    title: detail?.title ?? detail?.Title ?? fallback.examTitle ?? "FE Exam",
    courseId: detail?.courseId ?? detail?.CourseId ?? fallback.courseId ?? "",
    questions
  };
}

function buildAggregateCodeReview(questionResults = []) {
  const failedQuestions = questionResults.filter((item) => String(item?.status || "").toLowerCase() !== "accepted");
  const passedQuestions = questionResults.length - failedQuestions.length;

  return {
    errorAnalysis: failedQuestions.length
      ? `Completed ${passedQuestions}/${questionResults.length} programming question(s). Review the failed question(s) below before resubmitting.`
      : "All programming questions in this session passed their graded test cases.",
    qualityScore: `${passedQuestions}/${questionResults.length} question(s) accepted`,
    suggestions: failedQuestions.length
      ? [
          "Open each failed question and inspect its failing test cases.",
          "Compare your output with the expected behavior for every rejected question.",
          "Resubmit after fixing the first failing question before optimizing the rest."
        ]
      : [
          "All questions passed. You can still review runtime and memory for optimization.",
          "Keep this solution set as a reference for future practice."
        ],
    relatedConcepts: ["#Judge0", "#MultiQuestion", "#Debugging"]
  };
}

async function buildPeSessionViewerResult(sessionId, sessionPayload, questionMeta = []) {
  const peItems = Array.isArray(sessionPayload?.PE ?? sessionPayload?.pe) ? (sessionPayload?.PE ?? sessionPayload?.pe) : [];
  const questionMetaMap = new Map(questionMeta.map((item) => [item.id, item]));
  const detailResults = await Promise.all(peItems.map(async (item) => {
    const detailPayload = await http(`${STUDENT_SUBMISSIONS_BASE}/pe/${item?.id ?? item?.Id}`);
    const detail = unwrapPayload(detailPayload);
    const mapped = mapPeDetailToViewerResult(detail);
    const meta = questionMetaMap.get(mapped.questionId) || {};

    return {
      ...mapped,
      title: meta.title || `Question ${mapped.questionId || ""}`.trim(),
      difficulty: meta.difficulty || "Medium",
      topicTags: meta.topicTags || [],
      samples: meta.samples || [],
      description: meta.description || "",
      attemptCount: Number(item?.attemptCount ?? item?.AttemptCount ?? 0)
    };
  }));

  const totalEarned = detailResults.reduce((sum, item) => sum + Number(item?.earnedScore ?? 0), 0);
  const totalMax = peItems.reduce((sum, item) => sum + Number(item?.maxScore ?? item?.MaxScore ?? 0), 0);
  const percent = totalMax > 0 ? Number(((totalEarned / totalMax) * 100).toFixed(1)) : 0;
  const selectedQuestion = detailResults[0] || null;

  return {
    sessionId,
    examId: sessionPayload?.examId ?? sessionPayload?.ExamId ?? "",
    examTitle: sessionPayload?.examTitle ?? sessionPayload?.ExamTitle ?? "PE Practice Result",
    courseId: sessionPayload?.courseId ?? sessionPayload?.CourseId ?? "",
    submissionId: selectedQuestion?.submissionId ?? "",
    selectedQuestionId: selectedQuestion?.questionId ?? "",
    score: percent,
    earnedScore: totalEarned,
    maxScore: totalMax,
    status: detailResults.every((item) => String(item?.status || "").toLowerCase() === "accepted") ? "Accepted" : "Partial",
    mentorHintsUsed: 0,
    files: selectedQuestion?.files ?? [],
    testCases: selectedQuestion?.testCases ?? [],
    questionResults: detailResults,
    codeReview: buildAggregateCodeReview(detailResults)
  };
}

export async function startConfiguredExam(payload) {
  const response = await http(`${STUDENT_EXAM_CONFIG_BASE}/start`, {
    method: "POST",
    body: JSON.stringify(payload)
  });

  const data = unwrapPayload(response);
  const detail = data?.exam ?? data?.Exam;
  const sessionId = data?.sessionId ?? data?.SessionId ?? "";
  const startTime = data?.startTime ?? data?.StartTime ?? new Date().toISOString();
  const examType = data?.examType ?? data?.ExamType ?? payload?.examType ?? "FE";

  if (!detail || !sessionId) {
    throw new Error("Unable to create a session from the current configuration.");
  }

  if (String(examType).toUpperCase() === "PE") {
    const session = buildPeSessionFromExamDetail(detail, sessionId, { ...payload, startTime });
    writeJson(PE_SESSION_KEY, session);
    return { examType: "PE", session };
  }

  const session = buildFeSessionFromExamDetail(detail, sessionId, { ...payload, startTime });
  writeJson(FE_EXAM_SESSION_KEY, session);

  const normalizedMode = String(detail?.mode ?? detail?.Mode ?? payload?.mode ?? "Practice");
  if (normalizedMode.toLowerCase() === "flashcard") {
    writeJson(MCQ_SESSION_KEY, {
      mode: "Flashcard",
      sessionId,
      startTime,
      examId: detail?.id ?? detail?.Id ?? "",
      courseId: detail?.courseId ?? detail?.CourseId ?? "",
      title: detail?.title ?? detail?.Title ?? "Flashcard Session",
      questions: session.questions
    });
  }

  return { examType: "FE", mode: normalizedMode, session };
}

export async function getFeExamSession() {
  const stored = readJson(FE_EXAM_SESSION_KEY, null);
  if (!stored?.examId || !stored?.sessionId) {
    return null;
  }

  let liveSession = await getPracticeSession(stored.sessionId).catch(() => null);
  if (liveSession?.isPaused && String(stored?.status || "").toLowerCase() !== "submitted") {
    const resumedPayload = await http(`${PRACTICE_BASE}/sessions/${stored.sessionId}/resume`, {
      method: "POST"
    }).catch(() => null);

    if (resumedPayload) {
      liveSession = normalizePracticeSessionPayload(unwrapPayload(resumedPayload));
    }
  }

  const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${stored.examId}`);
  const detail = unwrapPayload(detailPayload);
  const source = { ...stored, ...liveSession };
  const questions = Array.isArray(detail?.feQuestions ?? detail?.FEQuestions)
    ? (detail?.feQuestions ?? detail?.FEQuestions).map(mapFeQuestion)
    : [];

  const session = {
    ...buildFeSessionFromExamDetail(detail, stored.sessionId, source),
    ...source,
    title: detail?.title ?? detail?.Title ?? source.title ?? "FE Exam",
    courseId: detail?.courseId ?? detail?.CourseId ?? source.courseId ?? "",
    questions
  };

  writeJson(FE_EXAM_SESSION_KEY, session);
  return session;
}

export async function openFeHistorySession({ examId, sessionId, examTitle, courseId }) {
  if (!examId || !sessionId) {
    throw new Error("examId and sessionId are required.");
  }

  const liveSession = await getPracticeSession(sessionId).catch(() => null);
  const session = {
    examId,
    sessionId,
    title: examTitle ?? "FE Exam",
    courseId: courseId ?? "",
    ...buildTimedSessionFields(liveSession || {}, liveSession?.startTime ?? new Date().toISOString()),
    status: "Submitted"
  };

  writeJson(FE_EXAM_SESSION_KEY, session);
  return getFeExamSession();
}

export async function resumeFeExamSession({ examId, sessionId, examTitle, courseId }) {
  if (!examId || !sessionId) {
    throw new Error("examId and sessionId are required.");
  }

  const resumedPayload = await http(`${PRACTICE_BASE}/sessions/${sessionId}/resume`, {
    method: "POST"
  });
  const resumedSession = normalizePracticeSessionPayload(unwrapPayload(resumedPayload));
  const detailPayload = await http(`${STUDENT_EXAMS_BASE}/${examId}`);
  const detail = unwrapPayload(detailPayload);
  const session = {
    ...buildFeSessionFromExamDetail(detail, sessionId, {
      ...resumedSession,
      examId,
      examTitle,
      courseId
    }),
    ...resumedSession,
    examId,
    title: detail?.title ?? detail?.Title ?? examTitle ?? "FE Exam",
    courseId: detail?.courseId ?? detail?.CourseId ?? courseId ?? "",
    status: ""
  };

  writeJson(FE_EXAM_SESSION_KEY, session);
  return session;
}

export async function submitFeAnswer({ sessionId, questionId, answer }) {
  const payload = await http(`${STUDENT_SUBMISSIONS_BASE}/fe`, {
    method: "POST",
    body: JSON.stringify({
      sessionId,
      questionId,
      userAnswer: answer ? [answer] : []
    })
  });

  return unwrapPayload(payload);
}

export async function finishFeExamSession(sessionId) {
  if (!sessionId) {
    throw new Error("sessionId is required.");
  }

  const payload = await http(`${PRACTICE_BASE}/sessions/${sessionId}/end`, {
    method: "POST"
  });

  return unwrapPayload(payload);
}

export async function getSessionSubmissions(sessionId) {
  if (!sessionId) {
    throw new Error("sessionId is required.");
  }

  const payload = await http(`${STUDENT_SUBMISSIONS_BASE}/session/${sessionId}`);
  return unwrapPayload(payload);
}

export async function saveMcqSession(session) {
  await wait();
  writeJson(MCQ_SESSION_KEY, session);
  return session;
}

export async function getByosDocuments() {
  await wait();
  return readJson(BYOS_DOCS_KEY, [
    {
      id: "BYOS-1",
      fileName: "recursion_notes.pdf",
      size: "1.8 MB",
      course: "PRO192",
      uploadedBy: "Current Student",
      status: "Vectorized",
      gatekeeper: "Supported",
      extractedText: "Recursion solves a problem by calling itself on smaller inputs...",
      tags: ["Recursion", "Call Stack", "Base Case"],
      chunks: [
        { order: 1, preview: "Recursion solves a problem by calling itself...", fullText: "Recursion solves a problem by calling itself on smaller inputs.", tags: ["Recursion"] },
        { order: 2, preview: "Every recursive solution needs a base case...", fullText: "Every recursive solution needs a base case to stop infinite calls.", tags: ["Base Case"] }
      ],
      autoConfirmed: true
    }
  ]);
}

export async function uploadByosDocuments(files, meta = {}) {
  await wait(700);
  const current = await getByosDocuments();
  const additions = files.map((file, index) => ({
    id: `BYOS-${Date.now()}-${index}`,
    fileName: file.name,
    size: `${(file.size / 1024 / 1024 || 0.4).toFixed(1)} MB`,
    course: meta.courseId || "General",
    uploadedBy: "Current Student",
    status: "Vectorized",
    gatekeeper: "Supported",
    extractedText: `Extracted preview content for ${file.name}.`,
    tags: ["Imported", "Personal Material"],
    chunks: [
      { order: 1, preview: `Preview from ${file.name}`, fullText: `Extracted preview content for ${file.name}.`, tags: ["Imported"] }
    ],
    autoConfirmed: true
  }));
  const next = [...additions, ...current].slice(0, 10);
  writeJson(BYOS_DOCS_KEY, next);
  return next;
}

export async function getAdminPipelineDocuments() {
  await wait();
  return readJson(ADMIN_DOCS_KEY, [
    {
      id: "ADM-1",
      fileName: "PRN211_syllabus.pdf",
      size: "2.4 MB",
      uploadDate: "2026-07-18T08:20:00",
      uploadedBy: "Admin Center",
      course: "PRN211",
      status: "Pending Approval",
      gatekeeper: "Supported",
      reason: "",
      extractedText: "This course covers responsive application development, state management, and API integration.",
      tags: ["React", "REST", "State"],
      chunks: [
        { order: 1, preview: "This course covers responsive application...", fullText: "This course covers responsive application development, state management, and API integration.", tags: ["React", "REST"] },
        { order: 2, preview: "Students will build web clients...", fullText: "Students will build web clients that communicate with backend services.", tags: ["API"] }
      ]
    }
  ]);
}

export async function uploadAdminPipelineDocuments(files, meta = {}) {
  await wait(700);
  const current = await getAdminPipelineDocuments();
  const additions = files.map((file, index) => ({
    id: `ADM-${Date.now()}-${index}`,
    fileName: file.name,
    size: `${(file.size / 1024 / 1024 || 0.5).toFixed(1)} MB`,
    uploadDate: new Date().toISOString(),
    uploadedBy: "Admin Center",
    course: meta.courseId || "General",
    status: "Pending Approval",
    gatekeeper: "Supported",
    reason: "",
    extractedText: `OCR preview for ${file.name}.`,
    tags: ["Imported", "Review"],
    chunks: [{ order: 1, preview: `OCR preview for ${file.name}`, fullText: `OCR preview for ${file.name}.`, tags: ["Imported"] }]
  }));
  const next = [...additions, ...current];
  writeJson(ADMIN_DOCS_KEY, next);
  return next;
}

export async function updatePipelineDocumentStatus(documentId, status, extractedText) {
  await wait(300);
  const current = await getAdminPipelineDocuments();
  const next = current.map((item) =>
    item.id === documentId
      ? { ...item, status, extractedText: extractedText ?? item.extractedText }
      : item
  );
  writeJson(ADMIN_DOCS_KEY, next);
  return next.find((item) => item.id === documentId);
}


export async function savePracticeSessionAnswers(payload) {
  await wait(260);
  const current = readJson(PRACTICE_SESSION_HISTORY_KEY, []);
  const record = {
    id: `PS-${Date.now()}`,
    createdAt: new Date().toISOString(),
    ...payload
  };
  writeJson(PRACTICE_SESSION_HISTORY_KEY, [record, ...current]);
  return record;
}
