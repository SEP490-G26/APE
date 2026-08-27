import { ROUTES, navigateTo } from "../../lib/routes";

const ITEMS = [
  { key: "my-exams", label: "My Exams", href: ROUTES.studentPractice },
  { key: "system-exams", label: "System Exams", href: ROUTES.studentSystemExamLibrary },
  { key: "question-bank", label: "Question Bank", href: ROUTES.studentQuestionBank },
  { key: "practice-history", label: "Practice History", href: ROUTES.studentPracticeHistory }
];

export function PracticeSidebar({ activeTab = "my-exams" }) {
  return (
    <aside className="practice-sidebar">
      <nav className="practice-sidebar__nav">
        {ITEMS.map((item) => (
          <button
            key={item.key}
            type="button"
            className={`practice-sidebar__link${activeTab === item.key ? " is-active" : ""}`}
            onClick={() => activeTab !== item.key && navigateTo(item.href)}
          >
            {item.label}
          </button>
        ))}
      </nav>
    </aside>
  );
}
