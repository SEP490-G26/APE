import { useEffect, useMemo, useState } from "react";
import { Button } from "../../../components/common";
import { StudentScaffold } from "../../../components/student/StudentScaffold";
import { updateAuthSessionUser } from "../../../lib/storage";
import { ROUTES, navigateTo } from "../../../lib/routes";
import { useInFlightGuard, useLockedFormSnapshot } from "../../../shared/guards";
import { InFlightNotice } from "../../../shared/ui";
import { getProfile } from "../../../services/profileService";
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
  getStudentDocuments,
  getStudentDocumentChapters,
  getStudentDocumentMultiChapterTopics
} from "../../student-documents/services/studentDocumentService";
import { generateReviewFromByos } from "../services/byosGenerationService";

const MAX_CHAPTERS = 3;
const MAX_TOPICS = 5;

export function GenerateFromByosPage({ documentId }) {
  const [documents, setDocuments] = useState([]);
  const [chapters, setChapters] = useState([]);
  const [topicResult, setTopicResult] = useState(null);
  const [result, setResult] = useState(null);
  const [error, setError] = useState("");
  const [isLoadingScope, setIsLoadingScope] = useState(true);
  const [isLoadingTopics, setIsLoadingTopics] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);
  const [isReviewMutating, setIsReviewMutating] = useState(false);
  const [subject, setSubject] = useState("");
  const [questionType, setQuestionType] = useState("PE");
  const [difficultyProfile, setDifficultyProfile] = useState(() => buildDefaultDifficultyProfile("PE", "Easy"));
  const [selectedDocumentId, setSelectedDocumentId] = useState(documentId || "");
  const [selectedChapterKeys, setSelectedChapterKeys] = useState([]);
  const [selectedTopics, setSelectedTopics] = useState([]);
  const requestScopeSnapshot = useLockedFormSnapshot(
    {
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

    async function loadDocuments() {
      setIsLoadingScope(true);
      setError("");

      try {
        const documentPayload = await getStudentDocuments({ page: 1, limit: 50 });
        if (!mounted) {
          return;
        }

        const readyDocuments = (documentPayload?.items || []).filter((item) => item.isReadyForGeneration);
        setDocuments(readyDocuments);

        const nextDocumentId = documentId || readyDocuments[0]?.id || "";
        setSelectedDocumentId(nextDocumentId);
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load BYOS generation scope.");
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
  }, [documentId]);

  useEffect(() => {
    let mounted = true;

    async function loadChapters() {
      if (!selectedDocumentId) {
        setChapters([]);
        setSelectedChapterKeys([]);
        return;
      }

      setIsLoadingScope(true);
      setError("");
      setChapters([]);
      setTopicResult(null);
      setSelectedTopics([]);

      try {
        const chapterPayload = await getStudentDocumentChapters(selectedDocumentId);
        if (!mounted) {
          return;
        }

        const nextChapters = chapterPayload?.chapters || [];
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
  }, [selectedDocumentId]);

  useEffect(() => {
    let mounted = true;

    async function loadTopics() {
      if (!selectedDocumentId || !selectedChapterKeys.length) {
        setTopicResult(null);
        setSelectedTopics([]);
        return;
      }

      setIsLoadingTopics(true);
      setError("");
      setTopicResult(null);
      setSelectedTopics([]);

      try {
        const topicsPayload = await getStudentDocumentMultiChapterTopics(selectedDocumentId, selectedChapterKeys);
        if (!mounted) {
          return;
        }

        setTopicResult(topicsPayload);
        setSubject(topicsPayload?.subject || "");
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load topics.");
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
  }, [selectedDocumentId, selectedChapterKeys]);

  const selectedChapterList = useMemo(
    () => chapters.filter((item) => selectedChapterKeys.includes(item.chapterKey)),
    [chapters, selectedChapterKeys]
  );
  const selectedDocument = useMemo(
    () => documents.find((item) => item.id === selectedDocumentId) || null,
    [documents, selectedDocumentId]
  );
  const selectedTopicDetails = useMemo(
    () => (topicResult?.topics || []).filter((item) => selectedTopics.includes(item.tag)),
    [topicResult, selectedTopics]
  );
  const totalRequestedCount = useMemo(() => getDifficultyProfileTotal(difficultyProfile), [difficultyProfile]);
  const chapterScopeChunks = useMemo(
    () => selectedChapterList.reduce((sum, item) => sum + (item.chunkCount || 0), 0),
    [selectedChapterList]
  );
  const chapterScopeTokens = useMemo(
    () => selectedChapterList.reduce((sum, item) => sum + (item.estimatedTokens || 0), 0),
    [selectedChapterList]
  );
  const topicPoolCount = topicResult?.topics?.length || 0;

  useEffect(() => {
    setDifficultyProfile((current) => {
      if (!current.length) {
        return buildDefaultDifficultyProfile(questionType, "Easy");
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
    if (!selectedDocumentId || !selectedChapterKeys.length || !subject || !selectedTopics.length) {
      setError("Please complete document, chapter, subject, and topic selection before generating.");
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
      const nextResult = await generateReviewFromByos({
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

      try {
        const profile = await getProfile();
        const actualBalance = Number(profile?.aiWalletBalanceVnd ?? 0);
        updateAuthSessionUser({ aiWalletBalanceVnd: actualBalance });

        setResult({
          ...nextResult,
          metrics: {
            ...(nextResult?.metrics || {}),
            remainingBalanceVnd: actualBalance
          }
        });
      } catch {
        setResult(nextResult);
      }
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
      activeRoute={ROUTES.studentByosGenerateRoot}
      title="Generate Questions From Document"
      
      actions={
        <div className="ai-inline-actions">
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentJourney)}>Open Setup</Button>
        </div>
      }
    >
      <section className="ai-student-shell">
        <section className="ai-student-shell ai-student-shell--split">
          <article className="ai-student-panel">
            <div className="ai-student-panel__header">
              <div>
                <p className="ai-student-kicker">Setup</p>
                <h2>Build the generation scope</h2>
              </div>
            </div>
            {error ? <div className="student-status-banner">{error}</div> : null}
            {isGenerating ? (
              <InFlightNotice
                title="Generating questions..."
                description={`Locked scope: document ${requestScopeSnapshot.selectedDocumentId || "-"}, chapters ${(requestScopeSnapshot.selectedChapterKeys || []).join(", ") || "-"}, subject ${requestScopeSnapshot.subject || "-"}, topics ${(requestScopeSnapshot.selectedTopics || []).join(", ") || "-"}.`}
              />
            ) : null}

            <section className="byos-generation-section">
              <div className="byos-generation-section__header">
                <div>
                  <p className="ai-student-kicker">1. Source</p>
                  <h3>Choose the document and output type</h3>
                </div>
                <span>Start from a ready BYOS document</span>
              </div>
              <div className="ai-form-grid">
                <label className="student-filter-field">
                  <span>Document</span>
                  <select value={selectedDocumentId} onChange={(event) => setSelectedDocumentId(event.target.value)} disabled={isGenerating || isLoadingScope}>
                    <option value="">Select document</option>
                    {documents.map((item) => (
                      <option key={item.id} value={item.id}>{item.fileName}</option>
                    ))}
                  </select>
                </label>
                <label className="student-filter-field">
                  <span>Subject</span>
                  <input value={subject} readOnly disabled />
                </label>
                <label className="student-filter-field">
                  <span>Question Type</span>
                  <select value={questionType} onChange={(event) => setQuestionType(event.target.value)} disabled={isGenerating}>
                    <option value="PE">PE</option>
                    <option value="FE">FE</option>
                  </select>
                </label>
              </div>
              {selectedDocument ? (
                <div className="ai-student-callout">
                  <strong>{selectedDocument.fileName}</strong>
                  <div>{selectedDocument.subjectCode || "Unknown subject"} - {selectedDocument.chunkCount || 0} chunks - {selectedDocument.tagCount || 0} tags</div>
                </div>
              ) : null}
            </section>

            <section className="byos-generation-section">
              <div className="byos-generation-section__header">
                <div>
                  <p className="ai-student-kicker">2. Scope</p>
                  <h3>Select chapters and topic evidence</h3>
                </div>
                <span>Up to {MAX_CHAPTERS} chapters and {MAX_TOPICS} topic tags</span>
              </div>
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
                <div className="ai-student-callout">
                  <strong>Selected chapters: {selectedChapterKeys.length}/{MAX_CHAPTERS}</strong>
                  <div>{(selectedChapterList.map((item) => item.chapterTitle).join(", ")) || "No chapter selected."}</div>
                  <div>{(selectedChapterList[0]?.overviewShort) || "Choose up to 3 chapters to build the generation scope."}</div>
                  <div>Approx scope: {chapterScopeChunks} chunks - {chapterScopeTokens} tokens</div>
                </div>
              ) : null}
              {!isLoadingScope && !chapters.length ? (
                <div className="ai-student-callout">This document has no chapter summary yet. Re-check extraction or choose another BYOS document.</div>
              ) : null}

              <div className="student-exam-field">
                <span>Topic Tags</span>
              </div>
              <div className="ai-topic-list">
                {(topicResult?.topics || []).map((topic) => (
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
              {isLoadingTopics ? <div className="ai-student-callout">Loading topics...</div> : null}
              {!isLoadingTopics && topicResult?.topics?.length ? (
                <div className="ai-student-callout">
                  Choose from 1 to {MAX_TOPICS} topic tags. Current selection: {selectedTopics.length}/{MAX_TOPICS}. Total requested: {totalRequestedCount}.
                </div>
              ) : null}
              {!isLoadingTopics && !topicResult?.topics?.length && selectedChapterKeys.length ? (
                <div className="ai-student-callout">No topic tags were returned for the current chapter scope.</div>
              ) : null}
            </section>

            <section className="byos-generation-section">
              <div className="byos-generation-section__header">
                <div>
                  <p className="ai-student-kicker">3. Output</p>
                  <h3>Define the difficulty mix and optional hints</h3>
                </div>
                <span>{totalRequestedCount} total questions requested</span>
              </div>
              <DifficultyProfileEditor
                questionType={questionType}
                rows={difficultyProfile}
                onChange={setDifficultyProfile}
                disabled={isGenerating}
              />
            </section>

            <div className="ai-inline-actions ai-inline-actions--footer">
              <Button variant="primary" onClick={handleGenerate} disabled={isGenerating || isLoadingScope || isLoadingTopics}>
                {isGenerating ? "Generating questions..." : "Generate Questions"}
              </Button>
            </div>
          </article>

          <article className="ai-student-panel">
            <div className="ai-student-panel__header">
              <div>
                <p className="ai-student-kicker">Result</p>
                <h2>Generation output</h2>
              </div>
            </div>
            <div className="byos-generation-result-head">
              <div className="byos-generation-result-head__badge">AI</div>
              <div>
                <strong>{result ? "Result ready for review" : "No result yet"}</strong>
                <p>
                  {result
                    ? "Inspect the generated question set, review signals, persistence details, and follow-up actions."
                    : "After you generate, this panel will show the parsed result, review summary, question payload, and usage metrics."}
                </p>
              </div>
            </div>
            <GenerationResultPanel
              result={result}
              showDraftActions
              showConfirmAllAction={false}
              hideCorrectAnswer
              onDeleteQuestion={handleDeleteDraft}
              onConfirmAll={handleConfirmRemaining}
              isMutating={isReviewMutating}
            />
            {result ? (
              <div className="ai-inline-actions ai-inline-actions--footer">
                {Array.isArray(result?.questions) && result.questions.some((question) => question?.persistedQuestionId && String(question?.persistedStatus || "Draft").toLowerCase() === "draft") ? (
                  <Button variant="primary" onClick={handleConfirmRemaining} disabled={isReviewMutating}>
                    {isReviewMutating ? "Processing..." : "Confirm Remaining Questions"}
                  </Button>
                ) : null}
                <Button variant="secondary" onClick={() => navigateTo(ROUTES.studentExams)}>
                  Open Practice Config
                </Button>
              </div>
            ) : null}
          </article>
        </section>
      </section>
    </StudentScaffold>
  );
}
