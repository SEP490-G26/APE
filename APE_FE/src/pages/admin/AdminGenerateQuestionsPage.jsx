import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { ROUTES } from "../../lib/routes";
import { http } from "../../services/http";
import { useInFlightGuard, useLockedFormSnapshot } from "../../shared/guards";
import { InFlightNotice } from "../../shared/ui";
import {
  buildDefaultDifficultyProfile,
  DifficultyProfileEditor,
  getDifficultyProfileTotal,
  validateDifficultyProfile
} from "../../modules/question-generation-shared/components/DifficultyProfileEditor";
import { GenerationResultPanel } from "../../modules/question-generation-shared/components/GenerationResultPanel";
import {
  deleteGeneratedQuestion,
  publishGeneratedQuestion
} from "../../modules/question-generation-shared/services/generatedQuestionReviewService";
import {
  generateReviewFromAdminSystem,
  getSystemCourseDocuments,
  getSystemDocumentChapters,
  getSystemDocumentMultiChapterTopics
} from "../../modules/question-generation-system/services/systemGenerationService";

const QUESTION_TYPES = ["FE", "PE"];

function buildFriendlyShortfallMessage(shortfallReport) {
  if (!shortfallReport) {
    return "";
  }

  const requested = Number(shortfallReport.requestedCount ?? 0);
  const generated = Number(shortfallReport.generatedCount ?? 0);
  const stopReason = String(shortfallReport.stopReason || "").toLowerCase();

  if (requested > generated && stopReason === "insufficient_grounded_chunks") {
    return `Only ${generated}/${requested} questions could be generated because the current chapter/topic scope is too narrow for PE. Expand the scope, add more related topics, or reduce the requested count.`;
  }

  if (requested > generated && stopReason === "requested_count_exceeds_grounded_capacity") {
    return `Only ${generated}/${requested} questions could be generated from the selected scope. Expand the chapter/topic scope or reduce the requested count.`;
  }

  if (requested > generated) {
    return `The system generated ${generated}/${requested} questions. See the Shortfall section below for details.`;
  }

  return "";
}

async function getAdminCourses() {
  const payload = await http("/api/admin/courses?page=1&limit=50");
  const data = payload?.data || payload;
  const items = data?.items || data?.Items || [];

  return items.map((course) => ({
    id: course?.id ?? course?.Id ?? "",
    code: course?.code ?? course?.Code ?? "",
    name: course?.name ?? course?.Name ?? "Untitled course"
  }));
}

export function AdminGenerateQuestionsPage({ documentId }) {
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
  const [selectedDocumentId, setSelectedDocumentId] = useState(documentId || "");
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

  useInFlightGuard(isGenerating || isReviewMutating);

  useEffect(() => {
    let mounted = true;

    async function loadCourses() {
      setIsLoadingScope(true);
      setError("");

      try {
        const nextCourses = await getAdminCourses();
        if (!mounted) {
          return;
        }

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
        setSubject("");
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
      setSubject("");

      try {
        const payload = await getSystemCourseDocuments(selectedCourseId);
        if (!mounted) {
          return;
        }

        const nextDocuments = payload || [];
        setDocuments(nextDocuments);
        setSelectedDocumentId((current) => {
          const preferredDocumentId = current || documentId || "";
          const existsInCourse = nextDocuments.some((item) => item.documentId === preferredDocumentId);
          if (existsInCourse) {
            return preferredDocumentId;
          }

          return nextDocuments[0]?.documentId || "";
        });
      } catch (loadError) {
        if (mounted) {
          setError(loadError.message || "Unable to load documents for this course.");
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
  }, [documentId, selectedCourseId]);

  useEffect(() => {
    let mounted = true;

    async function loadChapters() {
      if (!selectedCourseId || !selectedDocumentId) {
        setChapters([]);
        setSelectedChapterKeys([]);
        setTopicResult(null);
        setSelectedTopics([]);
        setSubject("");
        return;
      }

      setIsLoadingScope(true);
      setError("");
      setChapters([]);
      setSelectedChapterKeys([]);
      setTopicResult(null);
      setSelectedTopics([]);
      setSubject("");

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
          setError(loadError.message || "Unable to load chapters for this document.");
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
        setSubject("");
        return;
      }

      setIsLoadingTopics(true);
      setError("");
      setTopicResult(null);
      setSubject("");

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
          setError(loadError.message || "Unable to load topic tags for the selected chapters.");
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

  function clearGenerationResult() {
    setResult(null);
    setError("");
  }

  function toggleChapter(chapterKey) {
    clearGenerationResult();
    setSelectedChapterKeys((current) => {
      if (current.includes(chapterKey)) {
        return current.filter((item) => item !== chapterKey);
      }

      return [...current, chapterKey];
    });
  }

  function toggleTopic(tag) {
    clearGenerationResult();
    setSelectedTopics((current) => {
      if (current.includes(tag)) {
        return current.filter((item) => item !== tag);
      }

      return [...current, tag];
    });
  }

  async function handleGenerate() {
    if (!selectedCourseId || !selectedDocumentId || !selectedChapterKeys.length || !subject) {
      setError("Please select the course, document, chapter, and subject before generating questions.");
      return;
    }

    if (selectedTopics.length < 1) {
      setError("Please choose at least one topic tag. If you just changed chapters, reselect the matching topic tags before generating.");
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
      const nextResult = await generateReviewFromAdminSystem({
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
        isPublic: true
      });

      setResult(nextResult);
      const shortfallMessage = buildFriendlyShortfallMessage(nextResult?.shortfallReport);
      if (shortfallMessage) {
        setError(shortfallMessage);
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
            persistedStatus: question.persistedQuestionId ? "Active" : question.persistedStatus,
            persistedIsPublic: question.persistedQuestionId ? true : question.persistedIsPublic
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
    <AdminScaffold
      activeRoute={ROUTES.adminGenerateQuestions}
      heroIcon="doc"
      heroTitle="Management"
      heroSubtitle="Question generation"
      title="Generate Questions"
      subtitle="Generate from real system documents, save as draft first, remove unwanted questions, then confirm the remaining set."
    >
      <section className="system-generation-shell">
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
              </div>

              <div className="system-generation-form-grid">
                <label className="student-filter-field">
                  <span>Course</span>
                  <select
                    value={selectedCourseId}
                    onChange={(event) => {
                      clearGenerationResult();
                      setSelectedCourseId(event.target.value);
                    }}
                    disabled={isGenerating || isLoadingScope}
                  >
                    <option value="">Select course</option>
                    {courses.map((item) => (
                      <option key={item.id || item.code} value={item.id || item.code}>{item.code || item.id} - {item.name}</option>
                    ))}
                  </select>
                </label>

                <label className="student-filter-field">
                  <span>Document</span>
                  <select
                    value={selectedDocumentId}
                    onChange={(event) => {
                      clearGenerationResult();
                      setSelectedDocumentId(event.target.value);
                    }}
                    disabled={isGenerating || isLoadingScope || !documents.length}
                  >
                    <option value="">Select document</option>
                    {documents.map((item) => (
                      <option key={item.documentId} value={item.documentId}>{item.fileName}</option>
                    ))}
                  </select>
                </label>
              </div>

              {selectedDocument ? (
                <div className="ai-student-callout">
                  <strong>{selectedDocument.fileName}</strong>
                  <div>{selectedCourse?.code || selectedCourse?.id || "-"} | {selectedDocument.subjectCode || "Unknown subject"} | {selectedDocument.chunkCount || 0} chunks</div>
                </div>
              ) : null}
            </section>

            <section className="system-generation-section">
              <div className="system-generation-section__header">
                <div>
                  <p className="system-generation-section__eyebrow">Step 2</p>
                  <h3>Set learning scope</h3>
                </div>
                <span>{selectedTopics.length} topics selected</span>
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

              {selectedChapterList.length ? (
                <div className="ai-student-callout">
                  {selectedChapterList.map((item) => item.chapterTitle).join(", ")}
                </div>
              ) : null}

              <div className="student-exam-field">
                <span>Topic Tags</span>
              </div>
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

              {selectedTopicDetails.length ? (
                <div className="ai-student-callout">
                  {selectedTopicDetails.map((item) => `${item.tag} (${item.chunkCount || 0} chunks)`).join(", ")}
                </div>
              ) : null}
            </section>

            <section className="system-generation-section">
              <div className="system-generation-section__header">
                <div>
                  <p className="system-generation-section__eyebrow">Step 3</p>
                  <h3>Configure output</h3>
                </div>
                <span>{totalRequestedCount} questions</span>
              </div>

              <div className="student-segmented-control student-segmented-control--wide">
                {QUESTION_TYPES.map((type) => (
                  <button
                    key={type}
                    type="button"
                    className={questionType === type ? "is-active" : ""}
                    onClick={() => {
                      clearGenerationResult();
                      setQuestionType(type);
                    }}
                    disabled={isGenerating}
                  >
                    {type}
                  </button>
                ))}
              </div>

              <DifficultyProfileEditor
                questionType={questionType}
                rows={difficultyProfile}
                onChange={(nextProfile) => {
                  clearGenerationResult();
                  setDifficultyProfile(nextProfile);
                }}
                disabled={isGenerating}
              />
            </section>

            <div className="system-generation-sticky-actions">
              <Button variant="primary" onClick={handleGenerate} disabled={isGenerating || isLoadingScope || isLoadingTopics}>
                {isGenerating ? "Generating questions..." : "Generate Questions"}
              </Button>
            </div>
          </article>

          <article className="ai-student-panel system-generation-panel">
            <div className="ai-student-panel__header">
              <div>
                <p className="ai-student-kicker">Output</p>
                <h2>Review drafts before publish</h2>
              </div>
            </div>

            <GenerationResultPanel
              result={result}
              showDraftActions
              onDeleteQuestion={handleDeleteDraft}
              onConfirmAll={handleConfirmRemaining}
              isMutating={isReviewMutating}
            />
          </article>
        </section>
      </section>
    </AdminScaffold>
  );
}
