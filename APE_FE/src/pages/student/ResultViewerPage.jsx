import { useEffect, useState } from "react";
import { Button } from "../../components/common";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { useInFlightGuard } from "../../shared/guards";
import { InFlightNotice } from "../../shared/ui";
import { getLatestMentorFeedback, requestMentorFeedback } from "../../services/submissionService";
import { getPeSubmissionResult, openPeHistoryResult } from "../../services/studentWorkspaceService";

function wait(ms = 900) {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

function formatVnd(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} VND`;
}

function formatUsd(value) {
  return Number(value || 0).toFixed(6);
}

function resolveVerdictTone(verdict) {
  const normalized = String(verdict || "").toLowerCase();
  if (normalized === "accepted" || normalized === "ac") {
    return "green";
  }

  if (normalized.includes("pending") || normalized.includes("processing")) {
    return "blue";
  }

  if (normalized.includes("partial")) {
    return "orange";
  }

  return "red";
}

function resolveExecutionTone(status) {
  const normalized = String(status || "").toLowerCase();
  if (normalized === "passed" || normalized === "completed") {
    return "green";
  }

  if (normalized.includes("pending") || normalized.includes("processing")) {
    return "blue";
  }

  if (normalized === "notexecuted" || normalized === "not executed") {
    return "orange";
  }

  return "red";
}

function formatExecutionStatus(status = "", fallback = "") {
  const normalized = String(status || "").trim().toLowerCase();
  switch (normalized) {
    case "passed":
      return "Passed";
    case "failed":
      return "Failed";
    case "completed":
      return "Completed";
    case "notexecuted":
    case "not executed":
      return "Not executed";
    default:
      return fallback || status || "Unknown";
  }
}

function formatDiagnosticText(diagnostic) {
  if (!diagnostic) {
    return "";
  }

  const location = diagnostic.filename
    ? [diagnostic.filename, diagnostic.line, diagnostic.column].filter((value) => value !== null && value !== undefined && value !== "").join(":")
    : [diagnostic.line, diagnostic.column].filter((value) => value !== null && value !== undefined && value !== "").join(":");

  return [
    diagnostic.title || diagnostic.code || diagnostic.category || "Diagnostic",
    diagnostic.message || "",
    location ? `Location: ${location}` : ""
  ].filter(Boolean).join("\n");
}

function formatQuestionScore(item) {
  return `${Number(item?.earnedScore ?? 0).toFixed(2)}/${Number(item?.maxScore ?? 0).toFixed(2)}`;
}

function mapFallbackMentorFeedback(result) {
  return {
    feedback: {
      errorAnalysis: result.codeReview?.errorAnalysis ? [{ title: "Code Review", detail: result.codeReview.errorAnalysis }] : [],
      qualityScore: { overall: result.codeReview?.qualityScore || "-" },
      improvementSuggestions: (result.codeReview?.suggestions || []).map((item) => ({ title: "Suggestion", detail: item })),
      issueCategories: result.codeReview?.relatedConcepts || [],
      feedbackText: result.codeReview?.errorAnalysis || "No mentor feedback available.",
      verdict: result.status === "Partial" ? "needs_fix" : "accepted"
    },
    fallbackUsed: false,
    creditsDeducted: 0,
    chargedVnd: 0,
    actualDeductedVnd: 0,
    remainingBalanceVnd: 0
  };
}

function normalizeStoredMentorFeedback(feedback) {
  if (!feedback) {
    return null;
  }

  return {
    feedback,
    fallbackUsed: Boolean(feedback?.fallbackUsed ?? false),
    chargedVnd: Number(feedback?.chargedVnd ?? 0),
    actualDeductedVnd: Number(feedback?.actualDeductedVnd ?? 0),
    absorbedVnd: Number(feedback?.absorbedVnd ?? 0),
    remainingBalanceVnd: Number(feedback?.remainingBalanceVnd ?? 0),
    reportedCostUsd: Number(feedback?.reportedCostUsd ?? 0),
    usdToVndRate: Number(feedback?.usdToVndRate ?? 0),
    billingStatus: feedback?.billingStatus ?? ""
  };
}

function getMentorSuggestions(mentorResult, fallbackResult) {
  if (mentorResult?.feedback?.improvementSuggestions?.length) {
    return mentorResult.feedback.improvementSuggestions.map((item) => item.detail || item.title).filter(Boolean);
  }

  return fallbackResult.codeReview?.suggestions || [];
}

function getMentorIssueCategories(mentorResult, fallbackResult) {
  if (mentorResult?.feedback?.issueCategories?.length) {
    return mentorResult.feedback.issueCategories;
  }

  return fallbackResult.codeReview?.relatedConcepts || [];
}

export function ResultViewerPage() {
  const [result, setResult] = useState(null);
  const [selectedQuestionId, setSelectedQuestionId] = useState("");
  const [showReview, setShowReview] = useState(false);
  const [isRequestingMentor, setIsRequestingMentor] = useState(false);
  const [mentorError, setMentorError] = useState("");
  const [mentorResult, setMentorResult] = useState(null);

  useInFlightGuard(isRequestingMentor);

  const questionResults = Array.isArray(result?.questionResults) ? result.questionResults : [];
  const resolvedSelectedQuestionId = selectedQuestionId || questionResults[0]?.questionId || "";
  const activeQuestion = questionResults.find((item) => item.questionId === resolvedSelectedQuestionId) || questionResults[0] || null;
  const displaySubmissionId = activeQuestion?.submissionId || result?.submissionId || "";
  const displayTestCases = activeQuestion?.testCases || result?.testCases || [];
  const displayStatus = activeQuestion?.status || result?.status;
  const displayExecutionStatus = activeQuestion?.executionStatus || result?.executionStatus || "";
  const displayDiagnostics = activeQuestion?.diagnostics || result?.diagnostics || [];
  const displayScore = questionResults.length
    ? `${Number(result?.earnedScore ?? 0).toFixed(2)}/${Number(result?.maxScore ?? 0).toFixed(2)}`
    : `${result?.score}%`;
  const visibleCases = displayTestCases.filter((item) => !item.hidden).length;
  const lockedCases = displayTestCases.length - visibleCases;
  const acceptedCases = displayTestCases.filter((item) => String(item.verdict || "").toLowerCase() === "accepted" || item.verdict === "AC").length;

  useEffect(() => {
    let isMounted = true;

    async function loadResult() {
      const current = await getPeSubmissionResult();
      if (!isMounted) {
        return;
      }

      let resolvedResult = current;
      setResult(current);
      if (current?.selectedQuestionId) {
        setSelectedQuestionId(current.selectedQuestionId);
      }

      if (current?.submissionId && (current?.status === "Pending" || current?.status === "Processing")) {
        let sessionId = null;
        try {
          const raw = window.localStorage.getItem("ape.student.pe.session");
          sessionId = raw ? JSON.parse(raw)?.sessionId : null;
        } catch {
          sessionId = null;
        }

        if (!sessionId) {
          return;
        }

        try {
          const refreshed = await openPeHistoryResult(sessionId);
          if (isMounted) {
            resolvedResult = refreshed;
            setResult(refreshed);
            if (refreshed?.selectedQuestionId) {
              setSelectedQuestionId(refreshed.selectedQuestionId);
            }
          }
        } catch {
          // keep current local state; user can retry or reopen history
        }
      }
    }

    loadResult();

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    let ignore = false;

    async function loadMentorFeedback() {
      if (!displaySubmissionId) {
        setMentorResult(null);
        setShowReview(false);
        return;
      }

      setMentorError("");
      try {
        const latestFeedback = await getLatestMentorFeedback(displaySubmissionId);
        if (ignore || !latestFeedback) {
          return;
        }

        setMentorResult(normalizeStoredMentorFeedback(latestFeedback));
        setShowReview(true);
      } catch (error) {
        const message = String(error?.message || "");
        if (message.toLowerCase().includes("not found")) {
          if (!ignore) {
            setMentorResult(null);
            setShowReview(false);
          }
          return;
        }
      }
    }

    loadMentorFeedback();

    return () => {
      ignore = true;
    };
  }, [displaySubmissionId]);

  if (!result) {
    return null;
  }

  async function handleRequestMentor() {
    if (isRequestingMentor) {
      return;
    }

    setIsRequestingMentor(true);
    setMentorError("");

    try {
      if (displaySubmissionId) {
        const response = await requestMentorFeedback(displaySubmissionId);
        setMentorResult(response);
        setShowReview(true);
        return;
      }

      await wait();
      setMentorResult(mapFallbackMentorFeedback(result));
      setShowReview(true);
    } catch (error) {
      await wait(300);
      setMentorResult(mapFallbackMentorFeedback(result));
      setMentorError(error.message || "Mentor service is unavailable. Showing local review fallback.");
      setShowReview(true);
    } finally {
      setIsRequestingMentor(false);
    }
  }

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentExams}
      title="Result Viewer"
      subtitle="Inspect grading outcome, per-test verdicts, and open an AI review summary without exposing full solutions."
      actions={
        <div className="ai-inline-actions">
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentJourney)}>Open Setup</Button>
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentPracticeHistory)}>Open History</Button>
        </div>
      }
    >
      <section className="student-panel student-result-hero">
        <div className="student-result-hero__copy">
          <span className="student-panel__eyebrow">Practice Result</span>
          <h2>{result.examTitle || "Programming Result"}</h2>
          <p>Review each programming question, inspect execution evidence, and reopen AI mentor guidance without leaving the result screen.</p>
        </div>
        <div className="student-result-hero__stats">
          <article className="student-result-stat">
            <span>Overall Score</span>
            <strong>{displayScore}</strong>
          </article>
          <article className="student-result-stat">
            <span>Status</span>
            <strong>{displayStatus}</strong>
          </article>
          <article className="student-result-stat">
            <span>Execution</span>
            <strong>{formatExecutionStatus(displayExecutionStatus, displayStatus)}</strong>
          </article>
        </div>
      </section>

      <section className="student-result-layout">
        <div className="student-result-layout__main">
          <article className="student-panel student-result-overview">
            {activeQuestion ? (
              <>
                <div className="student-panel__header">
                  <div><h2>{activeQuestion.title || "Selected Question"}</h2><p>Problem brief and execution breakdown for the selected programming question.</p></div>
                  <span className={`student-pill is-${resolveVerdictTone(activeQuestion.status)}`}>{activeQuestion.status}</span>
                </div>
                {questionResults.length > 1 ? (
                  <div style={{ marginTop: 12 }}>
                    <div className="student-segmented-control student-segmented-control--wide">
                      {questionResults.map((item, index) => {
                        const isActive = item.questionId === resolvedSelectedQuestionId;
                        const label = `Q${index + 1}`;
                        return (
                          <button
                            key={item.questionId || String(index)}
                            type="button"
                            className={isActive ? "is-active" : ""}
                            onClick={() => setSelectedQuestionId(item.questionId)}
                            title={item.title || label}
                          >
                            {label}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                ) : null}
                <div className="student-result-question">
                  {activeQuestion.description ? (
                    <div className="student-result-question__statement">
                      <strong>Problem</strong>
                      <p>{activeQuestion.description}</p>
                    </div>
                  ) : null}
                </div>
                <div className="student-result-summary-grid">
                  <article className="student-result-summary-card">
                    <span>Question Score</span>
                    <strong>{formatQuestionScore(activeQuestion)}</strong>
                  </article>
                  <article className="student-result-summary-card">
                    <span>Accepted Cases</span>
                    <strong>{acceptedCases}/{displayTestCases.length}</strong>
                  </article>
                  <article className="student-result-summary-card">
                    <span>Visible Cases</span>
                    <strong>{visibleCases}</strong>
                  </article>
                  <article className="student-result-summary-card">
                    <span>Locked Cases</span>
                    <strong>{lockedCases}</strong>
                  </article>
                </div>
              </>
            ) : null}
          </article>

          <article className="student-panel student-result-tests">
            <div className="student-panel__header">
              <div><h2>Test Case Review</h2><p>Inspect input, expected output, actual output, and execution diagnostics for each graded case.</p></div>
            </div>
            <div className="student-result-testcase-list">
              {displayTestCases.map((item) => (
                <article key={item.index} className="student-result-testcase">
                  <div className="student-result-testcase__head">
                    <div className="student-result-testcase__title">
                      <strong>{item.label || `Test ${item.number || item.index}`}</strong>
                      <span>{item.hidden ? "Locked test case" : "Visible test case"}</span>
                    </div>
                    <div className="student-result-testcase__metrics">
                      <span className={`student-pill is-${item.isJudged ? resolveVerdictTone(item.verdict) : resolveExecutionTone(item.executionStatus)}`}>
                        {item.isJudged ? item.verdict : formatExecutionStatus(item.executionStatus, item.verdict)}
                      </span>
                      <span>{formatExecutionStatus(item.executionStatus, item.verdict)}</span>
                      <span>{item.runtime} ms</span>
                      <span>{item.memory} KB</span>
                    </div>
                  </div>
                  <div className="student-result-testcase__detail">
                    <div>
                      <strong>Input</strong>
                      <pre className="ai-code-block">{item.input || "(empty)"}</pre>
                    </div>
                    {!item.hidden ? (
                      <div>
                        <strong>Expected Output</strong>
                        <pre className="ai-code-block">{item.expectedOutput || "(empty)"}</pre>
                      </div>
                    ) : null}
                    <div>
                      <strong>Your Output</strong>
                      <pre className="ai-code-block">{item.actualOutput || "(empty)"}</pre>
                    </div>
                    {item.stderr ? (
                      <div>
                        <strong>Standard Error</strong>
                        <pre className="ai-code-block">{item.stderr}</pre>
                      </div>
                    ) : null}
                    {item.compileOutput ? (
                      <div>
                        <strong>Compile Output</strong>
                        <pre className="ai-code-block">{item.compileOutput}</pre>
                      </div>
                    ) : null}
                    {item.diagnostics?.length ? (
                      <div>
                        <strong>Diagnostics</strong>
                        <pre className="ai-code-block">{item.diagnostics.map(formatDiagnosticText).join("\n\n")}</pre>
                      </div>
                    ) : null}
                  </div>
                </article>
              ))}
            </div>
          </article>
        </div>

        <article className="student-panel student-result-mentor">
          <div className="student-panel__header">
            <div><h2>AI Code Review</h2><p>Structured mentor guidance and billing detail for the selected submission.</p></div>
          </div>
          {mentorError ? <div className="student-status-banner">{mentorError}</div> : null}
          {isRequestingMentor ? (
            <InFlightNotice
              title="Requesting mentor feedback..."
              description={`Mentor request is running for submission ${displaySubmissionId || "local-preview"}. Leaving this page may interrupt the current operation.`}
            />
          ) : null}
          {!showReview ? (
            <Button variant="primary" onClick={handleRequestMentor} disabled={isRequestingMentor}>
              {isRequestingMentor ? "Requesting mentor feedback..." : "Ask AI Mentor"}
            </Button>
          ) : (
            <div className="student-weak-list">
              <div className="student-weak-list__item">
                <strong>Verdict</strong>
                <span className={`student-result-mentor__verdict is-${resolveVerdictTone(mentorResult?.feedback?.verdict || displayStatus)}`}>
                  {mentorResult?.feedback?.verdict || (displayStatus === "Partial" ? "needs_fix" : "accepted")}
                </span>
                <em />
              </div>
              <div className="student-weak-list__item">
                <strong>Error Analysis</strong>
                <span>
                  {mentorResult?.feedback?.errorAnalysis?.[0]?.detail || result.codeReview.errorAnalysis}
                </span>
                <em />
              </div>
              <div className="student-weak-list__item">
                <strong>Quality Score</strong>
                <span>
                  {mentorResult?.feedback?.qualityScore?.overall || result.codeReview.qualityScore}
                </span>
                <em />
              </div>
              {mentorResult?.feedback?.feedbackText ? (
                <div className="student-weak-list__item">
                  <strong>Feedback Text</strong>
                  <span>{mentorResult.feedback.feedbackText}</span>
                  <em />
                </div>
              ) : null}
              {getMentorSuggestions(mentorResult, result).map((item) => (
                <div key={item} className="student-weak-list__item"><strong>Suggestion</strong><span>{item}</span><em /></div>
              ))}
              <div className="student-tag-grid">
                {getMentorIssueCategories(mentorResult, result).map((tag) => (
                  <button key={tag} type="button" className="student-tag-chip" onClick={() => navigateTo(ROUTES.studentExams)}>{tag}</button>
                ))}
              </div>
              {mentorResult ? (
                <div className="student-result-billing">
                  <strong>Mentor Runtime</strong>
                  <div className="student-result-billing__grid">
                    <div><span>Charged</span><strong>{formatVnd(mentorResult.chargedVnd ?? 0)}</strong></div>
                    <div><span>Deducted</span><strong>{formatVnd(mentorResult.actualDeductedVnd ?? 0)}</strong></div>
                    <div><span>Remaining Wallet</span><strong>{formatVnd(mentorResult.remainingBalanceVnd ?? 0)}</strong></div>
                  </div>
                </div>
              ) : null}
            </div>
          )}
        </article>
      </section>
    </StudentScaffold>
  );
}
