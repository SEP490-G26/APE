import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES, navigateTo } from "../../lib/routes";
import { getAuthSession } from "../../lib/storage";
import { getStudentCourseById, getStudentCourses, getStudentExams } from "../../services/studentExamService";
import { createPePracticeSession } from "../../services/studentWorkspaceService";

const DIFFICULTY_OPTIONS = ["Easy", "Medium", "Hard"];
const LANGUAGES = ["C", "C++", "Java", "Python"];
const MIN_BALANCE_VND = 1000;

function buildProfile() {
  const user = getAuthSession()?.user;
  return {
    aiWalletBalanceVnd: Number(user?.aiWalletBalanceVnd ?? 0),
    byokActive: Boolean(user?.byokActive ?? user?.hasByok ?? false)
  };
}

function formatVnd(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} VND`;
}

function fallbackTags(course) {
  const text = `${course?.code || ""} ${course?.name || ""}`.toLowerCase();
  if (text.includes("data")) {
    return ["Graphs", "Trees", "Sorting", "Searching"];
  }
  return ["OOP", "Recursion", "Validation", "Collections"];
}

export function StudentPeExamConfigPage() {
  const [profile] = useState(() => buildProfile());
  const [courses, setCourses] = useState([]);
  const [exams, setExams] = useState([]);
  const [selectedCourseId, setSelectedCourseId] = useState("");
  const [selectedExamId, setSelectedExamId] = useState("");
  const [availableTags, setAvailableTags] = useState([]);
  const [selectedTags, setSelectedTags] = useState([]);
  const [difficulty, setDifficulty] = useState("Medium");
  const [language, setLanguage] = useState("Java");
  const [isGenerating, setIsGenerating] = useState(false);

  useEffect(() => {
    getStudentCourses().then((items) => {
      setCourses(items);
      setSelectedCourseId(items[0]?.id || "");
    });
  }, []);

  useEffect(() => {
    if (!selectedCourseId) {
      return;
    }

    getStudentExams({ courseId: selectedCourseId, page: 1, limit: 50 })
      .then((response) => {
        const peExams = (response?.items || []).filter((item) => Number(item.peQuestionCount || 0) > 0);
        setExams(peExams);
        setSelectedExamId((current) =>
          peExams.some((item) => item.id === current) ? current : peExams[0]?.id || ""
        );
      })
      .catch(() => {
        setExams([]);
        setSelectedExamId("");
      });

    getStudentCourseById(selectedCourseId)
      .then((course) => setAvailableTags(course.topicTags?.length ? course.topicTags : fallbackTags(course)))
      .catch(() => {
        const course = courses.find((item) => item.id === selectedCourseId);
        setAvailableTags(fallbackTags(course));
      });
  }, [courses, selectedCourseId]);

  const hasEnoughBalance = profile.byokActive || profile.aiWalletBalanceVnd >= MIN_BALANCE_VND;
  const selectedCourse = useMemo(() => courses.find((item) => item.id === selectedCourseId) || null, [courses, selectedCourseId]);

  function toggleTag(tag) {
    setSelectedTags((current) => current.includes(tag) ? current.filter((item) => item !== tag) : [...current, tag]);
  }

  async function handleGenerate() {
    if (!selectedCourseId || !selectedExamId) {
      return;
    }

    if (!hasEnoughBalance) {
      navigateTo(ROUTES.profile);
      return;
    }

    setIsGenerating(true);
    await createPePracticeSession({
      examId: selectedExamId,
      title: `${selectedCourse?.name || "Practice"} Coding Challenge`,
      difficulty,
      language,
      topicTags: selectedTags.length ? selectedTags : availableTags.slice(0, 3)
    });
    setIsGenerating(false);
    navigateTo(ROUTES.studentCodingPractice);
  }

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentExams}
      title="PE Exam Configuration"
      subtitle="Configure a coding problem by course, topic, difficulty, and language before entering the workspace."
      actions={
        <div className="ai-inline-actions">
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentJourney)}>Open Setup</Button>
          <Button variant="ghost" onClick={() => navigateTo(ROUTES.studentPracticeHistory)}>Recent Results</Button>
        </div>
      }
    >
      <section className="student-exam-hero-strip">
        <div className="student-exam-hero-strip__card"><span>Course</span><strong>{selectedCourse?.name || "Loading"}</strong></div>
        <div className="student-exam-hero-strip__card"><span>AI Wallet</span><strong>{profile.byokActive ? "BYOK Enabled" : formatVnd(profile.aiWalletBalanceVnd)}</strong></div>
        <div className="student-exam-hero-strip__card"><span>Language</span><strong>{language}</strong></div>
      </section>

      <section className="student-exam-config">
        <div className="student-exam-config__primary">
          <label className="student-exam-field">
            <span>Select course</span>
            <select value={selectedCourseId} onChange={(event) => setSelectedCourseId(event.target.value)}>
              {courses.map((course) => <option key={course.id} value={course.id}>{course.code}: {course.name}</option>)}
            </select>
          </label>
          <label className="student-exam-field">
            <span>Select exam</span>
            <select value={selectedExamId} onChange={(event) => setSelectedExamId(event.target.value)} disabled={!exams.length}>
              {!exams.length ? <option value="">No PE exam available</option> : null}
              {exams.map((exam) => <option key={exam.id} value={exam.id}>{exam.title}</option>)}
            </select>
          </label>
          <div className="student-exam-field">
            <span>Topic Tags</span>
            <div className="student-tag-grid">
              {availableTags.map((tag) => (
                <button key={tag} type="button" className={`student-tag-chip${selectedTags.includes(tag) ? " is-selected" : ""}`} onClick={() => toggleTag(tag)}>
                  #{tag}
                </button>
              ))}
            </div>
          </div>
        </div>

        <aside className="student-exam-config__secondary">
          <div className="student-exam-field">
            <span>Difficulty</span>
            <div className="student-radio-group">
              {DIFFICULTY_OPTIONS.map((option) => (
                <label key={option} className="student-radio-option">
                  <input type="radio" checked={difficulty === option} onChange={() => setDifficulty(option)} />
                  <span>{option}</span>
                </label>
              ))}
            </div>
          </div>
          <label className="student-exam-field">
            <span>Language</span>
            <select value={language} onChange={(event) => setLanguage(event.target.value)}>
              {LANGUAGES.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </label>
          <div className="student-credit-panel">
            <strong>{profile.byokActive ? "BYOK Active - no wallet charge" : `AI wallet required: at least ${formatVnd(MIN_BALANCE_VND)}`}</strong>
            <p>Generate one coding problem, solve it in the workspace, then submit for grading.</p>
          </div>
          <Button variant="primary" className="student-generate-button" onClick={handleGenerate} disabled={isGenerating || !selectedExamId || !hasEnoughBalance}>
            {isGenerating ? "Generating..." : "Generate Problem"}
          </Button>
        </aside>
      </section>
    </StudentScaffold>
  );
}

