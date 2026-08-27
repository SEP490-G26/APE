import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { finishFeExamSession, getFeExamSession, getSessionSubmissions, pauseMcqPracticeSession, saveFeExamSessionSnapshot, submitFeAnswer } from "../../services/studentWorkspaceService";

function formatTime(totalSeconds) {
  const minutes = String(Math.floor(totalSeconds / 60)).padStart(2, "0");
  const seconds = String(totalSeconds % 60).padStart(2, "0");
  return `${minutes}:${seconds}`;
}

function getElapsedSeconds(startTime, endTime) {
  if (!startTime) {
    return 0;
  }

  const start = new Date(startTime).getTime();
  const end = endTime ? new Date(endTime).getTime() : Date.now();
  if (!Number.isFinite(start) || !Number.isFinite(end)) {
    return 0;
  }

  return Math.max(0, Math.floor((end - start) / 1000));
}

function getActiveElapsedSeconds(session) {
  if (!session) {
    return 0;
  }

  const baseSeconds = Math.max(0, Number(session?.activeDurationSeconds ?? 0));
  if (session?.endTime) {
    return baseSeconds;
  }

  if (session?.isPaused) {
    return baseSeconds;
  }

  const resumedAt = new Date(session?.lastResumedAt ?? session?.startTime ?? session?.startedAt ?? 0).getTime();
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

function buildQuestionContent(question) {
  const title = String(question?.title || "").trim();
  const description = String(question?.description || question?.text || "").trim();

  if (title && description) {
    return `## ${title}\n${description}`;
  }

  if (title) {
    return `## ${title}`;
  }

  return description;
}

function normalizeOptionText(option, index) {
  const raw = String(option ?? "");
  const trimmed = raw.trim();
  if (!trimmed) {
    return "";
  }

  const expectedLabel = String.fromCharCode(65 + index);
  const match = trimmed.match(/^([A-D])\s*[\.\)]\s*(.+)$/i);
  if (match && match[1].toUpperCase() === expectedLabel) {
    return match[2].trim();
  }

  return trimmed;
}

function findOptionIndexByAnswer(question, rawAnswer) {
  if (typeof rawAnswer === "number") {
    return rawAnswer;
  }

  const options = Array.isArray(question?.options) ? question.options : [];
  const answerList = Array.isArray(rawAnswer)
    ? rawAnswer
    : (rawAnswer !== null && rawAnswer !== undefined ? [rawAnswer] : []);

  for (const answer of answerList) {
    if (typeof answer !== "string") {
      continue;
    }

    const trimmedAnswer = answer.trim();
    if (!trimmedAnswer) {
      continue;
    }

    if (/^[A-Z]$/i.test(trimmedAnswer)) {
      const labelIndex = trimmedAnswer.toUpperCase().charCodeAt(0) - 65;
      if (labelIndex >= 0 && labelIndex < options.length) {
        return labelIndex;
      }
    }

    const exactIndex = options.findIndex((option) => String(option).trim() === trimmedAnswer);
    if (exactIndex >= 0) {
      return exactIndex;
    }

    const normalizedAnswer = trimmedAnswer.toLowerCase();
    const fuzzyIndex = options.findIndex((option) => String(option).trim().toLowerCase() === normalizedAnswer);
    if (fuzzyIndex >= 0) {
      return fuzzyIndex;
    }
  }

  return -1;
}

function getCorrectOptionIndex(question) {
  const rawAnswer = question?.answer ?? question?.correctAnswer ?? question?.Answer ?? question?.CorrectAnswer;
  return findOptionIndexByAnswer(question, rawAnswer);
}

export function StudentMultipleChoiceExamPage() {
  const [session, setSession] = useState(null);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [selectedOption, setSelectedOption] = useState(null);
  const [selectedAnswers, setSelectedAnswers] = useState({});
  const [gradedQuestions, setGradedQuestions] = useState({});
  const [collapsedExplanation, setCollapsedExplanation] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [summary, setSummary] = useState(null);
  const [isFinished, setIsFinished] = useState(false);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);

  useEffect(() => {
    let ignore = false;

    getFeExamSession()
      .then(async (data) => {
        if (ignore) {
          return;
        }

        if (!data?.questions?.length) {
          navigateTo(ROUTES.studentExams);
          return;
        }

        setSession(data);
        setCurrentIndex(Math.max(0, Number(data?.currentIndex ?? 0)));
        if (data?.selectedAnswers && typeof data.selectedAnswers === "object") {
          setSelectedAnswers(data.selectedAnswers);
        }

        if (String(data?.status) !== "Submitted") {
          return;
        }

        const submissionPayload = await getSessionSubmissions(data.sessionId);
        if (ignore) {
          return;
        }

        const feSubmissions = Array.isArray(submissionPayload?.FE)
          ? submissionPayload.FE
          : (Array.isArray(submissionPayload?.fe) ? submissionPayload.fe : []);
        const gradedMap = Object.fromEntries(
          feSubmissions.map((item) => {
            const answer = Array.isArray(item?.userAnswer ?? item?.UserAnswer)
              ? (item?.userAnswer ?? item?.UserAnswer)[0] ?? null
              : null;
            const correctAnswer = Array.isArray(item?.correctAnswer ?? item?.CorrectAnswer)
              ? (item?.correctAnswer ?? item?.CorrectAnswer)
              : [];
            const question = data.questions.find((entry) => entry.id === (item?.questionId ?? item?.QuestionId));
            const selectedIndex = question ? findOptionIndexByAnswer(question, answer) : -1;

            return [
              item?.questionId ?? item?.QuestionId,
              {
                correctAnswer,
                selectedOption: selectedIndex >= 0 ? selectedIndex : null,
                isCorrect: Boolean(item?.isCorrect ?? item?.IsCorrect)
              }
            ];
          })
        );

        const selectedMap = Object.fromEntries(
          Object.entries(gradedMap).map(([questionId, value]) => [questionId, { selectedOption: value.selectedOption }])
        );

        setSelectedAnswers(selectedMap);
        setGradedQuestions(gradedMap);
        setIsFinished(true);
        setSummary(buildSummary(data.questions, gradedMap));
      })
      .catch(() => {
        if (!ignore) {
          navigateTo(ROUTES.studentExams);
        }
      });

    return () => {
      ignore = true;
    };
  }, []);

  const questions = session?.questions || [];
  const currentQuestion = questions[currentIndex] || null;
  const currentAttempt = currentQuestion ? gradedQuestions[currentQuestion.id] : null;
  const correctOptionIndex = currentQuestion
    ? getCorrectOptionIndex({
      ...currentQuestion,
      correctAnswer: currentAttempt?.correctAnswer?.length ? currentAttempt.correctAnswer : currentQuestion.correctAnswer
    })
    : -1;
  const answeredCount = Object.keys(selectedAnswers).length;
  const progressText = `${answeredCount} / ${questions.length || 0}`;

  useEffect(() => {
    if (!currentQuestion) {
      return;
    }

    setSelectedOption(selectedAnswers[currentQuestion.id]?.selectedOption ?? null);
    setCollapsedExplanation(false);
  }, [currentQuestion, selectedAnswers]);

  useEffect(() => {
    if (!session?.sessionId) {
      setElapsedSeconds(0);
      return undefined;
    }

    const syncElapsed = () => {
      setElapsedSeconds(getActiveElapsedSeconds(session));
    };

    syncElapsed();
    if (isFinished || String(session?.status) === "Submitted") {
      return undefined;
    }

    const intervalId = window.setInterval(syncElapsed, 1000);
    return () => window.clearInterval(intervalId);
  }, [isFinished, session]);

  useEffect(() => {
    if (!session?.sessionId || isFinished || String(session?.status) === "Submitted") {
      return undefined;
    }

    const pauseSession = () => {
      pauseMcqPracticeSession(session.sessionId, { keepalive: true }).catch(() => {});
    };

    window.addEventListener("pagehide", pauseSession);
    return () => {
      window.removeEventListener("pagehide", pauseSession);
      pauseSession();
    };
  }, [isFinished, session?.sessionId, session?.status]);

  useEffect(() => {
    if (!session?.sessionId || isFinished) {
      return;
    }

    saveFeExamSessionSnapshot({
      ...session,
      currentIndex,
      selectedAnswers
    });
  }, [currentIndex, isFinished, selectedAnswers, session]);

  const scoreSummary = useMemo(() => {
    if (!questions.length || !isFinished) {
      return { correctCount: 0, total: 0, percent: 0 };
    }

    const correctCount = questions.filter((question) => gradedQuestions[question.id]?.isCorrect).length;
    return {
      correctCount,
      total: questions.length,
      percent: Math.round((correctCount / questions.length) * 100)
    };
  }, [gradedQuestions, isFinished, questions]);

  async function handleFinishAttempt() {
    if (!session?.sessionId || isSubmitting || isFinished) {
      return;
    }
    if (Object.keys(selectedAnswers).length !== questions.length) {
      return;
    }

    setIsSubmitting(true);
    setErrorMessage("");

    try {
      const results = [];

      for (const question of questions) {
        const selectedIndex = selectedAnswers[question.id]?.selectedOption;
        const selectedText = typeof selectedIndex === "number" && selectedIndex >= 0
          ? question.options[selectedIndex]
          : null;

        const result = await submitFeAnswer({
          sessionId: session.sessionId,
          questionId: question.id,
          answer: selectedText
        });

        results.push({
          questionId: result?.questionId ?? result?.QuestionId ?? question.id,
          correctAnswer: Array.isArray(result?.correctAnswer ?? result?.CorrectAnswer)
            ? (result?.correctAnswer ?? result?.CorrectAnswer)
            : [],
          isCorrect: Boolean(result?.isCorrect ?? result?.IsCorrect),
          selectedOption: selectedIndex
        });
      }

      const gradedMap = Object.fromEntries(
        results.map((item) => [
          item.questionId,
          {
            correctAnswer: item.correctAnswer,
            selectedOption: item.selectedOption,
            isCorrect: item.isCorrect
          }
        ])
      );

      await finishFeExamSession(session.sessionId);

      setGradedQuestions(gradedMap);
      setIsFinished(true);
      const payload = buildSummary(questions, gradedMap, session.title || "FE Exam");
      setSummary(payload);
      setSession((current) => current ? { ...current, status: "Submitted" } : current);
    } catch (error) {
      setErrorMessage(error.message || "Unable to submit this FE exam.");
    } finally {
      setIsSubmitting(false);
    }
  }

  function handleSelectOption(index) {
    if (!currentQuestion || isSubmitting || isFinished) {
      return;
    }

    setSelectedOption(index);
    setCollapsedExplanation(false);
    setErrorMessage("");
    setSelectedAnswers((current) => ({
      ...current,
      [currentQuestion.id]: {
        selectedOption: index
      }
    }));
  }

  if (!currentQuestion) {
    return null;
  }

  const allAnswered = answeredCount === questions.length;

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentFeExam}
      title="FE Multiple Choice Exam"
      subtitle="Answer each question, verify instantly, and finish with a summary."
      topbarActionLabel="Open Results"
      topbarActionHref={ROUTES.studentPracticeHistory}
      showPageHeader={false}
    >
      <section className="fe-exam-shell">
        <aside className="fe-exam-sidebar">
          <div className="fe-exam-sidebar__head">
              <h2>Exam Navigation</h2>
              <p>Elapsed {formatTime(elapsedSeconds)}</p>
            </div>

          <div className="fe-exam-sidebar__list">
            {questions.map((question, index) => {
              const attempt = gradedQuestions[question.id];
              const answered = selectedAnswers[question.id]?.selectedOption !== null && selectedAnswers[question.id]?.selectedOption !== undefined;
              return (
                <button
                  key={question.id}
                  type="button"
                  className={`fe-exam-nav-item${currentIndex === index ? " is-active" : ""}${answered ? " is-answered" : ""}${isFinished && attempt?.isCorrect ? " is-correct" : ""}${isFinished && attempt && !attempt.isCorrect ? " is-wrong" : ""}`}
                  onClick={() => setCurrentIndex(index)}
                >
                  <span className="fe-exam-nav-item__dot" aria-hidden="true">
                    {answered ? "✓" : ""}
                  </span>
                  <span>Question {index + 1}</span>
                </button>
              );
            })}
          </div>

          <div className="fe-exam-sidebar__footer">
            <button type="button" className="fe-exam-utility">Help Center</button>
            <button type="button" className="fe-exam-utility">Calculator</button>
            <Button variant="primary" className="fe-exam-finish" disabled={!allAnswered || isFinished || isSubmitting} onClick={handleFinishAttempt}>
              {isSubmitting ? "Submitting..." : isFinished ? "Completed" : "Finish Attempt"}
            </Button>
          </div>
        </aside>

        <section className="fe-exam-main">
          <header className="fe-exam-main__header">
            <div>
              <span className="fe-exam-badge">{currentQuestion.topicTags?.[0] || session?.title || "FE Exam"}</span>
              <h1>Question {currentIndex + 1}</h1>
            </div>
            <div className="fe-exam-progress">
              <span>Progress</span>
              <strong>{progressText}</strong>
            </div>
          </header>

          <article className="fe-exam-question-card">
            <div className="fe-exam-question-copy">
              {renderMarkdownBlocks(buildQuestionContent(currentQuestion))}
            </div>

            <div className="fe-exam-options">
              {currentQuestion.options.map((option, index) => {
                const optionText = normalizeOptionText(option, index);
                const isSelected = selectedOption === index;
                const isStudentChoice = isFinished
                  ? currentAttempt?.selectedOption === index
                  : isSelected;
                const isCorrectAnswer = isFinished && correctOptionIndex === index;
                const isWrongSelection = isFinished && isStudentChoice && !currentAttempt?.isCorrect;
                const optionStateClass = isFinished
                  ? `${isStudentChoice ? " is-selected" : ""}${isCorrectAnswer ? " is-correct" : ""}${isWrongSelection ? " is-wrong" : ""}`
                  : `${isSelected ? " is-selected" : ""}`;

                return (
                  <button
                    key={`${index}-${option}`}
                    type="button"
                    className={`fe-exam-option${optionStateClass}`}
                    onClick={() => handleSelectOption(index)}
                    disabled={isSubmitting || isFinished}
                  >
                    <span className="fe-exam-option__letter">{String.fromCharCode(65 + index)}</span>
                    <span className="fe-exam-option__body">
                      <span className="fe-exam-option__text">{optionText}</span>
                      {isFinished && (isStudentChoice || isCorrectAnswer) ? (
                        <span className="fe-exam-option__badges">
                          {isStudentChoice ? (
                            <span className={`fe-exam-option__badge${isWrongSelection ? " is-wrong" : ""}`}>
                              Your answer
                            </span>
                          ) : null}
                          {isCorrectAnswer ? (
                            <span className="fe-exam-option__badge is-correct">
                              Correct answer
                            </span>
                          ) : null}
                        </span>
                      ) : null}
                    </span>
                    {isCorrectAnswer ? <span className="fe-exam-option__check">OK</span> : null}
                  </button>
                );
              })}
            </div>
        </article>

          {errorMessage ? (
            <section className="student-status-banner">
              {errorMessage}
            </section>
          ) : null}

          {isFinished && currentAttempt ? (
            <section className="fe-exam-explanation">
              <button type="button" className="fe-exam-explanation__toggle" onClick={() => setCollapsedExplanation((current) => !current)}>
                <span>Explanation</span>
                <span>{collapsedExplanation ? "+" : "-"}</span>
              </button>
              {!collapsedExplanation ? <p>{currentQuestion.explanation}</p> : null}
            </section>
          ) : null}

          {summary ? (
            <section className="fe-exam-summary">
              <strong>Correct: {summary.correctCount}/{summary.totalQuestions} ({summary.scorePercent}%)</strong>
              <p>Your answers have been submitted and this FE exam session has been completed.</p>
            </section>
          ) : null}

          <footer className="fe-exam-actions">
            <Button variant="ghost" disabled={currentIndex === 0} onClick={() => setCurrentIndex((current) => Math.max(0, current - 1))}>
              Previous Question
            </Button>
            <div className="fe-exam-actions__right">
              <Button variant="primary" disabled={currentIndex === questions.length - 1} onClick={() => setCurrentIndex((current) => Math.min(questions.length - 1, current + 1))}>
                Next Question
              </Button>
            </div>
          </footer>
        </section>
      </section>
    </StudentScaffold>
  );
}

function buildSummary(questions, gradedMap, mode = "FE Exam") {
  const total = questions.length;
  const correctCount = questions.filter((question) => gradedMap[question.id]?.isCorrect).length;
  const percent = total ? Math.round((correctCount / total) * 100) : 0;

  return {
    mode,
    fe_answers: questions.map((question) => ({
      questionId: question.id,
      selectedOption: gradedMap[question.id]?.selectedOption ?? null,
      isCorrect: Boolean(gradedMap[question.id]?.isCorrect)
    })),
    correctCount,
    totalQuestions: total,
    scorePercent: percent
  };
}
