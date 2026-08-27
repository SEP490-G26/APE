import { useEffect, useMemo, useState } from "react";
import { Button } from "../../../components/common";
import { DashboardIcon } from "../../../components/dashboard/DashboardIcon";
import { StudentScaffold } from "../../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../../lib/routes";
import { useInFlightGuard, useLockedFormSnapshot } from "../../../shared/guards";
import { InFlightNotice } from "../../../shared/ui";
import { getStudentCourses } from "../../student-documents/services/studentDocumentService";
import {
  buildDefaultDifficultyProfile,
  DifficultyProfileEditor,
  getDifficultyProfileTotal,
  validateDifficultyProfile
} from "../../question-generation-shared/components/DifficultyProfileEditor";
import { GenerationResultPanel } from "../../question-generation-shared/components/GenerationResultPanel";
import {
  deleteGeneratedQuestion,
  publishGeneratedQuestion
} from "../../question-generation-shared/services/generatedQuestionReviewService";
import {
  generateReviewFromSystem,
  getSystemCourseDocuments,
  getSystemDocumentChapters,
  getSystemDocumentMultiChapterTopics
} from "../services/systemGenerationService";

const MAX_CHAPTERS = 3;
const MAX_TOPICS = 5;
const QUESTION_TYPES = ["FE", "PE"];

export function GenerateFromSystemPage() {
  const [courses, setCourses] = useState([]);
  const [documents, setDocuments] = useState([]);
  const [chapters, setChapters] = useState([]);
  const [topicResult, setTopicResult] = useState(null);
  const [result, setResult] = useState(null);
  const [error, setError] = useState("");
  const [isLoadingScope, setIsLoadingScope] = useState(true);
  const [isLoadingTopics, setIsLoadingTopics] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);
  const [isReviewMutating, setIsReviewMutating] = useState(false);
  const [selectedCourseId, setSelectedCourseId] = useState("");
  const [selectedDocumentId, setSelectedDocumentId] = useState("");
  const [selectedChapterKeys, setSelectedChapterKeys] = useState([]);
  const [selectedTopics, setSelectedTopics] = useState([]);
  const [subject, setSubject] = useState("");
  const [questionType, setQuestionType] = useState("FE");
  const [difficultyProfile, setDifficultyProfile] = useState(() => buildDefaultDifficultyProfile("FE", "Medium"));
  const requestScopeSnapshot = useLockedFormSnapshot(
    {
      selectedCourseId,
      selectedDocumentId,
      selectedChapterKeys,
      subject,
      questionType,
      difficultyProfile,
      selectedTopics
    },
    isGenerating
  );

  useInFlightGuard(isGenerating);

  useEffect(() => {
    let mounted = true;

    async function loadCourses() {
      setIsLoadingScope(true);
      setError("");

      try {
        const payload = await getStudentCourses({ page: 1, limit: 50 });
        if (!mounted) {
          return;
        }

        const nextCourses = payload?.items || [];
        setCourses(nextCourses);
        setSelectedCourseId(nextCourses[0]?.id || nextCourses[0]?.code || "");
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load courses.");
        }
      } finally {
        if (mounted) {
          setIsLoadingScope(false);
        }
      }
    }

    loadCourses();

    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    let mounted = true;

    async function loadDocuments() {
      if (!selectedCourseId) {
        setDocuments([]);
        setSelectedDocumentId("");
        setSelectedChapterKeys([]);
        return;
      }

      setIsLoadingScope(true);
      setError("");
      setDocuments([]);
      setSelectedDocumentId("");
      setChapters([]);
      setSelectedChapterKeys([]);
      setTopicResult(null);
      setSelectedTopics([]);

      try {
        const payload = await getSystemCourseDocuments(selectedCourseId);
        if (!mounted) {
          return;
        }

        const nextDocuments = payload || [];
        setDocuments(nextDocuments);
        setSelectedDocumentId(nextDocuments[0]?.documentId || "");
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load source documents.");
        }
      } finally {
        if (mounted) {
          setIsLoadingScope(false);
        }
      }
    }

    loadDocuments();

    return () => {
      mounted = false;
    };
  }, [selectedCourseId]);

  useEffect(() => {
    let mounted = true;

    async function loadChapters() {
      if (!selectedCourseId || !selectedDocumentId) {
        setChapters([]);
        setSelectedChapterKeys([]);
        return;
      }

      setIsLoadingScope(true);
      setError("");
      setChapters([]);
      setSelectedChapterKeys([]);
      setTopicResult(null);
      setSelectedTopics([]);

      try {
        const payload = await getSystemDocumentChapters(selectedCourseId, selectedDocumentId);
        if (!mounted) {
          return;
        }

        const nextChapters = payload?.chapters || [];
        setChapters(nextChapters);
        setSelectedChapterKeys(nextChapters[0]?.chapterKey ? [nextChapters[0].chapterKey] : []);
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load chapters.");
        }
      } finally {
        if (mounted) {
          setIsLoadingScope(false);
        }
      }
    }

    loadChapters();

    return () => {
      mounted = false;
    };
  }, [selectedCourseId, selectedDocumentId]);

  useEffect(() => {
    let mounted = true;

    async function loadTopics() {
      if (!selectedCourseId || !selectedDocumentId || !selectedChapterKeys.length) {
        setTopicResult(null);
        setSelectedTopics([]);
        return;
      }

      setIsLoadingTopics(true);
      setError("");
      setTopicResult(null);

      try {
        const payload = await getSystemDocumentMultiChapterTopics(selectedCourseId, selectedDocumentId, selectedChapterKeys);
        if (!mounted) {
          return;
        }

        setTopicResult(payload);
        setSubject(payload?.subject || "");
        setSelectedTopics((current) => {
          const available = new Set((payload?.topics || []).map((item) => item.tag));
          return current.filter((item) => available.has(item));
        });
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load chapter topics.");
        }
      } finally {
        if (mounted) {
          setIsLoadingTopics(false);
        }
      }
    }

    loadTopics();

    return () => {
      mounted = false;
    };
  }, [selectedCourseId, selectedDocumentId, selectedChapterKeys]);

  const selectedCourse = useMemo(
    () => courses.find((item) => (item.id || item.code) === selectedCourseId) || null,
    [courses, selectedCourseId]
  );
  const selectedDocument = useMemo(
    () => documents.find((item) => item.documentId === selectedDocumentId) || null,
    [documents, selectedDocumentId]
  );
  const selectedChapterList = useMemo(
    () => chapters.filter((item) => selectedChapterKeys.includes(item.chapterKey)),
    [chapters, selectedChapterKeys]
  );
  const topicOptions = topicResult?.topics || [];
  const selectedTopicDetails = useMemo(
    () => topicOptions.filter((item) => selectedTopics.includes(item.tag)),
    [topicOptions, selectedTopics]
  );
  const totalRequestedCount = useMemo(() => getDifficultyProfileTotal(difficultyProfile), [difficultyProfile]);
  const readinessText = !selectedCourseId || !selectedDocumentId || !selectedChapterKeys.length
    ? "Complete source selection"
    : !selectedTopics.length
      ? "Pick topic tags"
      : "Ready to generate";

  useEffect(() => {
    setDifficultyProfile((current) => {
      if (!current.length) {
        return buildDefaultDifficultyProfile(questionType, "Medium");
      }

      if (questionType === "FE" && current.length === 1 && Number(current[0].count) === 1) {
        return buildDefaultDifficultyProfile("FE", current[0].difficulty);
      }

      if (questionType === "PE" && current.length === 1 && Number(current[0].count) === 10) {
        return buildDefaultDifficultyProfile("PE", current[0].difficulty);
      }

      return current;
    });
  }, [questionType]);

  function toggleChapter(chapterKey) {
    setSelectedChapterKeys((current) => {
      if (current.includes(chapterKey)) {
        return current.filter((item) => item !== chapterKey);
      }

      if (current.length >= MAX_CHAPTERS) {
        return current;
      }

      return [...current, chapterKey];
    });
  }

  function toggleTopic(tag) {
    setSelectedTopics((current) => {
      if (current.includes(tag)) {
        return current.filter((item) => item !== tag);
      }

      if (current.length >= MAX_TOPICS) {
        return current;
      }

      return [...current, tag];
    });
  }

  async function handleGenerate() {
    if (!selectedCourseId || !selectedDocumentId || !selectedChapterKeys.length || !subject) {
      setError("Please complete course, document, chapter, and subject selection before generating.");
      return;
    }

    if (selectedChapterKeys.length > MAX_CHAPTERS) {
      setError(`Please choose at most ${MAX_CHAPTERS} chapters.`);
      return;
    }

    if (selectedTopics.length < 1 || selectedTopics.length > MAX_TOPICS) {
      setError(`Please choose from 1 to ${MAX_TOPICS} topic tags.`);
      return;
    }

    const difficultyProfileError = validateDifficultyProfile(difficultyProfile, questionType);
    if (difficultyProfileError) {
      setError(difficultyProfileError);
      return;
    }

    setIsGenerating(true);
    setError("");

    try {
      const nextResult = await generateReviewFromSystem({
        courseId: selectedCourseId,
        documentId: selectedDocumentId,
        chapterKey: selectedChapterKeys[0],
        chapterKeys: selectedChapterKeys,
        subject,
        questionType,
        difficultyProfile: difficultyProfile.map((item) => ({
          difficulty: item.difficulty,
          count: Number(item.count)
        })),
        count: totalRequestedCount,
        targetTopics: selectedTopics,
        mode: "SameModelDualRole",
        maxAttempts: 2,
        persistQuestions: true,
        isPublic: false
      });

      setResult(nextResult);
    } catch (generationError) {
      setError(generationError.message || "Question generation failed.");
    } finally {
      setIsGenerating(false);
    }
  }

  async function handleDeleteDraft(question) {
    if (!question?.persistedQuestionId) {
      return;
    }

    setIsReviewMutating(true);
    setError("");

    try {
      await deleteGeneratedQuestion(question.persistedQuestionId);
      setResult((current) => {
        if (!current) {
          return current;
        }

        const nextQuestions = (current.questions || []).filter((item) => item.persistedQuestionId !== question.persistedQuestionId);
        const nextIds = (current.persistence?.persistedQuestionIds || []).filter((id) => id !== question.persistedQuestionId);
        const nextFeIds = (current.persistence?.feQuestionIds || []).filter((id) => id !== question.persistedQuestionId);
        const nextPeIds = (current.persistence?.peQuestionIds || []).filter((id) => id !== question.persistedQuestionId);

        return {
          ...current,
          questions: nextQuestions,
          persistence: {
            ...current.persistence,
            feQuestionIds: nextFeIds,
            peQuestionIds: nextPeIds,
            persistedQuestionIds: nextIds,
            persistedCount: nextIds.length,
            persisted: nextIds.length > 0
          }
        };
      });
    } catch (mutationError) {
      setError(mutationError.message || "Unable to remove this draft question.");
    } finally {
      setIsReviewMutating(false);
    }
  }

  async function handleConfirmRemaining() {
    const draftIds = (result?.questions || [])
      .map((question) => question.persistedQuestionId)
      .filter(Boolean);

    if (!draftIds.length) {
      return;
    }

    setIsReviewMutating(true);
    setError("");

    try {
      for (const questionId of draftIds) {
        await publishGeneratedQuestion(questionId);
      }

      setResult((current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          questions: (current.questions || []).map((question) => ({
            ...question,
            persistedStatus: question.persistedQuestionId ? "Active" : question.persistedStatus
          }))
        };
      });
    } catch (mutationError) {
      setError(mutationError.message || "Unable to confirm generated questions.");
    } finally {
      setIsReviewMutating(false);
    }
  }

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentGenerateSystem}
      title="Generate From System Pool"
      subtitle="Build a scoped FE or PE run from course documents, chapters, and canonical topic tags."
      actions={
        <div className="ai-inline-actions">
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentJourney)}>Open Setup</Button>
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentByosDocuments)}>Open BYOS</Button>
        </div>
      }
    >
      <section className="system-generation-shell">
        <article className="system-generation-hero">
          <div className="system-generation-hero__copy">
            <p className="ai-student-kicker">System Flow</p>
            <h2>Scope first. Generate once.</h2>
            <p>
              Select the course document, narrow it to the right chapters and topic tags, then generate a cleaner FE or PE set without jumping between pages.
            </p>
          </div>

          <div className="system-generation-summary-grid">
            <article className="system-generation-summary-card">
              <span>Course</span>
              <strong>{selectedCourse ? selectedCourse.code || selectedCourse.id : "-"}</strong>
              <small>{selectedCourse?.name || "Select course"}</small>
            </article>
            <article className="system-generation-summary-card">
              <span>Source</span>
              <strong>{selectedDocument?.fileName || "-"}</strong>
              <small>{selectedChapterKeys.length}/{MAX_CHAPTERS} chapters</small>
            </article>
            <article className="system-generation-summary-card">
              <span>Run</span>
              <strong>{questionType} - {totalRequestedCount}</strong>
              <small>{difficultyProfile.map((item) => `${item.difficulty} ${item.count}`).join(" - ")}</small>
            </article>
          </div>
        </article>

        <section className="ai-student-shell ai-student-shell--split">
          <article className="ai-student-panel system-generation-panel">
            <div className="ai-student-panel__header">
              <div>
                <p className="ai-student-kicker">Input</p>
                <h2>Generation scope</h2>
              </div>
            </div>

            {error ? <div className="student-status-banner">{error}</div> : null}
            {isGenerating ? (
              <InFlightNotice
                title="Generating questions..."
                description={`Locked scope: course ${requestScopeSnapshot.selectedCourseId || "-"}, document ${requestScopeSnapshot.selectedDocumentId || "-"}, chapters ${(requestScopeSnapshot.selectedChapterKeys || []).join(", ") || "-"}, topics ${(requestScopeSnapshot.selectedTopics || []).join(", ") || "-"}.`}
              />
            ) : null}

            <section className="system-generation-section">
              <div className="system-generation-section__header">
                <div>
                  <p className="system-generation-section__eyebrow">Step 1</p>
                  <h3>Choose source</h3>
                </div>
                <span>{selectedDocument ? "Document ready" : "Select course + document"}</span>
              </div>

              <div className="system-generation-form-grid">
                <label className="student-filter-field">
                  <span>Course</span>
                  <select value={selectedCourseId} onChange={(event) => setSelectedCourseId(event.target.value)} disabled={isGenerating || isLoadingScope}>
                    <option value="">Select course</option>
                    {courses.map((item) => (
                      <option key={item.id || item.code} value={item.id || item.code}>{item.code || item.id} - {item.name}</option>
                    ))}
                  </select>
                </label>

                <label className="student-filter-field">
                  <span>Document</span>
                  <select value={selectedDocumentId} onChange={(event) => setSelectedDocumentId(event.target.value)} disabled={isGenerating || isLoadingScope || !documents.length}>
                    <option value="">Select document</option>
                    {documents.map((item) => (
                      <option key={item.documentId} value={item.documentId}>{item.fileName}</option>
                    ))}
                  </select>
                </label>
              </div>
              {!isLoadingScope && !documents.length ? (
                <div className="system-generation-inline-note">
                  No system documents are available for this course in the current DB snapshot.
                </div>
              ) : null}
            </section>

            <section className="system-generation-section">
              <div className="system-generation-section__header">
                <div>
                  <p className="system-generation-section__eyebrow">Step 2</p>
                  <h3>Set learning scope</h3>
                </div>
                <span>{selectedTopics.length}/{MAX_TOPICS} topics selected</span>
              </div>

              <label className="student-filter-field">
                <span>Subject</span>
                <input value={subject} readOnly disabled />
              </label>

              <div className="student-exam-field">
                <span>Chapters</span>
              </div>
              <div className="ai-topic-list">
                {chapters.map((item) => (
                  <button
                    key={item.chapterKey}
                    type="button"
                    className={`ai-chip ai-chip--toggle ${selectedChapterKeys.includes(item.chapterKey) ? "is-active" : ""}`}
                    onClick={() => toggleChapter(item.chapterKey)}
                    disabled={isGenerating || isLoadingScope}
                  >
                    {item.chapterTitle}
                  </button>
                ))}
              </div>

              {!isLoadingScope && chapters.length ? (
                <div className="system-generation-inline-note">
                  Choose up to {MAX_CHAPTERS} chapters. Current scope: {selectedChapterList.map((item) => item.chapterTitle).join(", ") || "No chapter selected."}
                </div>
              ) : null}
              {!isLoadingScope && !chapters.length && selectedDocumentId ? (
                <div className="system-generation-inline-note">
                  This system document has no chapter summary available yet.
                </div>
              ) : null}

              <div className="student-exam-field">
                <span>Topic Tags</span>
                <div className="ai-topic-list">
                  {topicOptions.map((topic) => (
                    <button
                      key={topic.tag}
                      type="button"
                      className={`ai-chip ai-chip--toggle ${selectedTopics.includes(topic.tag) ? "is-active" : ""}`}
                      onClick={() => toggleTopic(topic.tag)}
                      disabled={isGenerating || isLoadingTopics}
                    >
                      {topic.tag}
                    </button>
                  ))}
                </div>
              </div>

              {isLoadingTopics ? <div className="system-generation-inline-note">Loading topics...</div> : null}
              {!isLoadingTopics && !topicOptions.length && selectedChapterKeys.length ? (
                <div className="system-generation-inline-note">
                  No topic tags were returned for the current chapter scope.
                </div>
              ) : null}
              {selectedTopicDetails.length ? (
                <div className="system-generation-inline-note">
                  Evidence: {selectedTopicDetails.map((item) => `${item.tag} (${item.chunkCount || 0} chunks)`).join(", ")}
                </div>
              ) : null}
            </section>

            <section className="system-generation-section">
              <div className="system-generation-section__header">
                <div>
                  <p className="system-generation-section__eyebrow">Step 3</p>
                  <h3>Configure output</h3>
                </div>
                <span>{questionType} {difficultyProfile.length > 1 ? "mixed" : "single"} profile</span>
              </div>

              <div className="student-segmented-control student-segmented-control--wide">
                {QUESTION_TYPES.map((type) => (
                  <button
                    key={type}
                    type="button"
                    className={questionType === type ? "is-active" : ""}
                    onClick={() => setQuestionType(type)}
                    disabled={isGenerating}
                  >
                    {type}
                  </button>
                ))}
              </div>

              <DifficultyProfileEditor
                questionType={questionType}
                rows={difficultyProfile}
                onChange={setDifficultyProfile}
                disabled={isGenerating}
              />

              <div className="system-generation-inline-note">
                {readinessText}. Choose 1 to {MAX_TOPICS} topic tags before generating. Total requested: {totalRequestedCount}.
              </div>
            </section>

            <div className="system-generation-sticky-actions">
              <div className="system-generation-readiness">
                <span>Readiness</span>
                <strong>{readinessText}</strong>
              </div>
              <Button variant="primary" onClick={handleGenerate} disabled={isGenerating || isLoadingScope || isLoadingTopics}>
                {isGenerating ? "Generating questions..." : "Generate Questions"}
              </Button>
            </div>
          </article>

          <article className="ai-student-panel system-generation-panel">
            <div className="ai-student-panel__header">
              <div>
                <p className="ai-student-kicker">Output</p>
                <h2>Generation result</h2>
              </div>
            </div>

            <div className="system-generation-result-head">
              <div className="system-generation-result-head__badge">
                <DashboardIcon kind="spark" />
              </div>
              <div>
                <strong>{result ? "Latest parsed review" : "Waiting for your first run"}</strong>
                <p>{result ? "Review, inspect, then continue into practice." : "Generated questions and review details will appear here."}</p>
              </div>
            </div>

            <GenerationResultPanel
              result={result}
              showDraftActions
              hideCorrectAnswer
              onDeleteQuestion={handleDeleteDraft}
              onConfirmAll={handleConfirmRemaining}
              isMutating={isReviewMutating}
            />
            {result ? (
              <div className="ai-inline-actions ai-inline-actions--footer">
                <Button variant="secondary" onClick={() => navigateTo(ROUTES.studentExams)}>
                  Open Practice Config
                </Button>
                <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentPracticeHistory)}>
                  Open Practice History
                </Button>
              </div>
            ) : null}
          </article>
        </section>
      </section>
    </StudentScaffold>
  );
}
