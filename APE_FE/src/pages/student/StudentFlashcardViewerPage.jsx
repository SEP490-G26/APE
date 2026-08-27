import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES } from "../../lib/routes";
import { getMcqSession, pauseMcqPracticeSession } from "../../services/studentWorkspaceService";

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
      return <div key={`space-${index}`} className="flashcard-viewer__spacer" />;
    }

    if (trimmed.startsWith("## ")) {
      return <h3 key={index}>{trimmed.slice(3)}</h3>;
    }

    if (trimmed.startsWith("- ")) {
      return (
        <div key={index} className="flashcard-viewer__bullet">
          <span />
          <p>{trimmed.slice(2)}</p>
        </div>
      );
    }

    return <p key={index}>{trimmed}</p>;
  });
}

function renderOptionList(options = []) {
  return options.map((option, index) => (
    <div key={`${index}-${option}`} className="flashcard-viewer__option">
      <strong>{String.fromCharCode(65 + index)}</strong>
      <p>{option}</p>
    </div>
  ));
}

function resolveAnswerText(card) {
  if (!card) {
    return "No answer";
  }

  if (typeof card.answer === "number" && Array.isArray(card.options) && card.answer >= 0 && card.answer < card.options.length) {
    return card.options[card.answer];
  }

  if (Array.isArray(card.correctAnswer) && card.correctAnswer.length > 0) {
    return card.correctAnswer.join(", ");
  }

  return "No answer";
}

export function StudentFlashcardViewerPage() {
  const [session, setSession] = useState(null);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isFlipped, setIsFlipped] = useState(false);
  const [cardRatings, setCardRatings] = useState({});
  const [touchStartX, setTouchStartX] = useState(null);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);

  useEffect(() => {
    let ignore = false;

    getMcqSession("Flashcard").then((data) => {
      if (!ignore) {
        setSession(data);
      }
    });

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    if (!session?.sessionId) {
      setElapsedSeconds(0);
      return undefined;
    }

    const syncElapsed = () => setElapsedSeconds(getActiveElapsedSeconds(session));
    syncElapsed();

    const intervalId = window.setInterval(syncElapsed, 1000);
    return () => window.clearInterval(intervalId);
  }, [session]);

  useEffect(() => {
    if (!session?.sessionId || String(session?.status) === "Submitted") {
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
  }, [session?.sessionId, session?.status]);

  const cards = session?.questions || [];
  const currentCard = cards[currentIndex] || null;

  const counters = useMemo(() => {
    const values = Object.values(cardRatings);
    return {
      knew: values.filter((value) => value === "knew").length,
      didntKnow: values.filter((value) => value === "didnt-know").length
    };
  }, [cardRatings]);

  function goToCard(nextIndex) {
    if (!cards.length) {
      return;
    }

    const boundedIndex = Math.max(0, Math.min(cards.length - 1, nextIndex));
    setCurrentIndex(boundedIndex);
    setIsFlipped(false);
  }

  function markCard(status) {
    if (!currentCard) {
      return;
    }

    setCardRatings((current) => ({
      ...current,
      [currentCard.id]: status
    }));
  }

  if (!currentCard) {
    return null;
  }

  const currentRating = cardRatings[currentCard.id];
  const progressPercent = Math.round(((currentIndex + 1) / cards.length) * 100);
  const answerText = resolveAnswerText(currentCard);

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentFlashcards}
      title="Flashcard Viewer"
      subtitle="Flip each card, review the answer, and keep track of what you already know."
      topbarActionLabel="Back To Exams"
      topbarActionHref={ROUTES.studentExams}
      showPageHeader={false}
    >
      <section className="flashcard-viewer">
        <header className="flashcard-viewer__header">
          <div>
            <span className="flashcard-viewer__eyebrow">Active Recall</span>
            <h1>Flashcard Viewer</h1>
            <p>Card {currentIndex + 1} of {cards.length}</p>
          </div>

          <div className="flashcard-viewer__stats">
            <strong>Knew: {counters.knew}</strong>
            <span>Didn&apos;t Know: {counters.didntKnow}</span>
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
              {cards.map((card, index) => {
                const rating = cardRatings[card.id];
                return (
                  <button
                    key={card.id}
                    type="button"
                    className={`flashcard-viewer__rail-item${currentIndex === index ? " is-active" : ""}${rating === "knew" ? " is-knew" : ""}${rating === "didnt-know" ? " is-didnt-know" : ""}`}
                    onClick={() => goToCard(index)}
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
                    goToCard(currentIndex + 1);
                  } else if (deltaX >= 40) {
                    goToCard(currentIndex - 1);
                  }
                  setTouchStartX(null);
                }}
              >
                <div className="flashcard-viewer__face flashcard-viewer__face--front">
                  <span className="flashcard-viewer__face-tag">Front</span>
                  <div className="flashcard-viewer__content">
                    <div className="flashcard-viewer__question-copy">
                      {renderMarkdownBlocks(currentCard.description || currentCard.text || "")}
                    </div>
                    {currentCard.options?.length ? (
                      <div className="flashcard-viewer__options">
                        {renderOptionList(currentCard.options)}
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
                    <p>{currentCard.explanation}</p>
                  </div>
                </div>
              </button>
            </div>

            <div className="flashcard-viewer__actions">
              <div className="flashcard-viewer__actions-group">
                <Button variant="ghost" disabled={currentIndex === 0} onClick={() => goToCard(currentIndex - 1)}>
                  Previous
                </Button>
                <Button variant="ghost" disabled={currentIndex === cards.length - 1} onClick={() => goToCard(currentIndex + 1)}>
                  Next
                </Button>
              </div>

              <div className="flashcard-viewer__actions-group">
                <button
                  type="button"
                  className={`flashcard-viewer__rating flashcard-viewer__rating--good${currentRating === "knew" ? " is-active" : ""}`}
                  onClick={() => markCard("knew")}
                >
                  Knew It
                </button>
                <button
                  type="button"
                  className={`flashcard-viewer__rating flashcard-viewer__rating--soft${currentRating === "didnt-know" ? " is-active" : ""}`}
                  onClick={() => markCard("didnt-know")}
                >
                  Didn&apos;t Know
                </button>
              </div>
            </div>
          </section>
        </div>
      </section>
    </StudentScaffold>
  );
}
