import { useEffect, useMemo, useState } from "react";
import { StudentScaffold } from "../../components/student/StudentScaffold";
import { ROUTES } from "../../lib/routes";
import {
  getStudentAnalytics,
  getStudentInsightCourses,
  getStudentPracticeHistory,
  getStudentStreak
} from "../../services/studentInsightService";

const PERIOD_OPTIONS = [
  { value: "day", label: "Day" },
  { value: "week", label: "Week" },
  { value: "month", label: "Month" }
];

const COURSE_ORDER = ["All", "PRF192", "PRO192", "CSD201"];

function formatDateOnly(value) {
  return new Date(value).toLocaleDateString("en-GB");
}

function formatShortDayMonth(value) {
  const date = new Date(value);
  const day = `${date.getDate()}`.padStart(2, "0");
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  return `${day}/${month}`;
}

function formatWeekTickLabel(value) {
  const date = new Date(value);
  const weekday = date.toLocaleDateString("en-GB", { weekday: "short" });
  return `${weekday} ${formatShortDayMonth(date)}`;
}

function formatMonthTickLabel(value) {
  return `${new Date(value).getMonth() + 1}th`;
}

function formatDateTimeLabel(value) {
  return new Date(value).toLocaleString("en-GB", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function formatNumber(value, options) {
  return Number(value || 0).toLocaleString("en-GB", options);
}

function formatPercent(value) {
  return `${formatNumber(value, { maximumFractionDigits: 1 })}%`;
}

function formatDurationMinutes(value) {
  const minutes = Math.max(0, Number(value || 0));
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = Math.round(minutes % 60);

  if (hours <= 0) {
    return `${remainingMinutes} min`;
  }

  return `${hours}h ${remainingMinutes}m`;
}

function formatWalletBalance(value) {
  return `${formatNumber(value)} VND`;
}

function isMonthStyleLabel(label) {
  return /^\d{1,2}th$/i.test(String(label || ""));
}

function formatChartTooltipValue(key, value) {
  if (key === "studyMinutes") {
    return formatDurationMinutes(value);
  }

  return formatNumber(value, { maximumFractionDigits: 2 });
}

function getSeriesBarHeight(value, maxValue) {
  const numericValue = Number(value || 0);
  if (numericValue <= 0 || maxValue <= 0) {
    return "0%";
  }

  return `${Math.max(4, Math.round((numericValue / maxValue) * 100))}%`;
}

function isClosedSession(item) {
  const normalizedStatus = String(item?.status || "").toLowerCase();
  return normalizedStatus.includes("complete") || normalizedStatus.includes("abandon");
}

function startOfLocalDay(date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function diffInDays(left, right) {
  const milliseconds = startOfLocalDay(left).getTime() - startOfLocalDay(right).getTime();
  return Math.round(milliseconds / 86400000);
}

function getRangeStart(period) {
  const now = new Date();
  const today = startOfLocalDay(now);

  switch (period) {
    case "day":
      return today;
    case "week":
      return new Date(today.getFullYear(), today.getMonth(), today.getDate() - 6);
    default:
      return new Date(today.getFullYear(), 0, 1);
  }
}

function getRangeLabel(period, startDate) {
  const now = new Date();

  if (period === "day") {
    return `${now.toLocaleDateString("en-GB", { weekday: "long" })}, ${formatDateOnly(now)}`;
  }

  return `${formatDateOnly(startDate)} - ${formatDateOnly(now)}`;
}

function buildTrendSeries(period, history) {
  const today = startOfLocalDay(new Date());
  const startDate = getRangeStart(period);

  if (period === "day") {
    return Array.from({ length: 24 }, (_, hour) => {
      const bucket = history.filter((item) => {
        const itemDate = new Date(item.date);
        return diffInDays(itemDate, today) === 0 && itemDate.getHours() === hour;
      });

      return {
        id: `hour-${hour}`,
        label: `${`${hour}`.padStart(2, "0")}:00`,
        fullLabel: `${formatDateOnly(today)} ${`${hour}`.padStart(2, "0")}:00`,
        sessions: bucket.length,
        studyMinutes: bucket
          .filter(isClosedSession)
          .reduce((total, item) => total + Number(item.durationMinutes || 0), 0),
        score: bucket.length ? bucket.reduce((total, item) => total + Number(item.numericScore || 0), 0) / bucket.length : 0
      };
    });
  }

  if (period === "month") {
    return Array.from({ length: 12 }, (_, monthIndex) => {
      const monthDate = new Date(today.getFullYear(), monthIndex, 1);
      const bucket = history.filter((item) => {
        const itemDate = new Date(item.date);
        return itemDate.getFullYear() === monthDate.getFullYear() && itemDate.getMonth() === monthIndex;
      });

      return {
        id: `month-${monthIndex}`,
        label: formatMonthTickLabel(monthDate),
        fullLabel: `Month ${monthIndex + 1}/${monthDate.getFullYear()}`,
        sessions: bucket.length,
        studyMinutes: bucket
          .filter(isClosedSession)
          .reduce((total, item) => total + Number(item.durationMinutes || 0), 0),
        score: bucket.length ? bucket.reduce((total, item) => total + Number(item.numericScore || 0), 0) / bucket.length : 0
      };
    });
  }

  const dayCount = Math.max(1, diffInDays(today, startDate) + 1);
  return Array.from({ length: dayCount }, (_, offset) => {
    const day = new Date(startDate.getFullYear(), startDate.getMonth(), startDate.getDate() + offset);
    const bucket = history.filter((item) => diffInDays(new Date(item.date), day) === 0);

    return {
      id: `day-${offset}`,
      label: formatWeekTickLabel(day),
      fullLabel: formatDateTimeLabel(day),
      sessions: bucket.length,
      studyMinutes: bucket
        .filter(isClosedSession)
        .reduce((total, item) => total + Number(item.durationMinutes || 0), 0),
      score: bucket.length ? bucket.reduce((total, item) => total + Number(item.numericScore || 0), 0) / bucket.length : 0
    };
  });
}

function buildHeatmap(history) {
  const today = startOfLocalDay(new Date());
  return Array.from({ length: 28 }, (_, index) => {
    const day = new Date(today.getFullYear(), today.getMonth(), today.getDate() - (27 - index));
    const sessions = history.filter((item) => diffInDays(new Date(item.date), day) === 0).length;
    return {
      id: day.toISOString(),
      label: formatDateOnly(day),
      sessions
    };
  });
}

function buildCourseRows(courses, history) {
  return courses
    .filter((course) => COURSE_ORDER.includes(course.code))
    .map((course) => {
      const rows = history.filter((item) => item.courseId === course.id || item.courseId === course.code);
      const scores = rows.map((item) => Number(item.numericScore || item.score || 0));
      const totalMinutes = rows
        .filter(isClosedSession)
        .reduce((total, item) => total + Number(item.durationMinutes || 0), 0);
      return {
        id: course.id,
        code: course.code,
        name: course.name,
        attempts: rows.length,
        totalMinutes,
        averageScore: scores.length ? scores.reduce((total, value) => total + value, 0) / scores.length : 0
      };
    });
}

function getCompletionMeta(history) {
  const completed = history.filter((item) => {
    const normalizedStatus = String(item.status || "").toLowerCase();
    return normalizedStatus.includes("submit") || normalizedStatus.includes("complete");
  }).length;
  const abandoned = history.filter((item) => String(item.status || "").toLowerCase().includes("abandon")).length;
  return { completed, abandoned };
}

function buildDifficultyRows(history) {
  const feRows = history.filter((item) => item.type === "FE");
  const bands = [
    { label: "0%-49%", min: 0, max: 49.999 },
    { label: "50%-79%", min: 50, max: 79.999 },
    { label: "80%-100%", min: 80, max: 100 }
  ];

  return bands.map((band) => {
    const matchedRows = feRows.filter((item) => {
      const score = Number(item.numericScore || 0);
      return score >= band.min && score <= band.max;
    });

    return {
      id: band.label,
      label: band.label,
      count: matchedRows.length,
      rate: feRows.length > 0 ? (matchedRows.length / feRows.length) * 100 : 0
    };
  });
}

function MiniProgressBar({ value, tone = "blue" }) {
  return (
    <div className="student-analytics-progress">
      <span className={`student-analytics-progress__fill is-${tone}`} style={{ width: `${Math.max(4, Math.min(100, value || 0))}%` }} />
    </div>
  );
}

function SummaryCard({ label, value, note }) {
  return (
    <article className="student-panel student-analytics-card">
      <span>{label}</span>
      <strong>{value}</strong>
      <p>{note}</p>
    </article>
  );
}

function CompactSeriesChart({ title, note, rows, valueKey, tone = "blue" }) {
  const maxValue = Math.max(...rows.map((item) => Number(item[valueKey] || 0)), 1);
  const useScrollableLayout = rows.length > 8;
  const hasMonthLabels = rows.some((item) => isMonthStyleLabel(item?.label));
  const columnWidth = useScrollableLayout ? (hasMonthLabels ? 52 : 38) : null;
  const columnGap = useScrollableLayout ? 10 : 0;
  const chartMinWidth = useScrollableLayout ? (rows.length * columnWidth) + ((rows.length - 1) * columnGap) + 12 : null;

  return (
    <article className={`student-panel student-analytics-panel${hasMonthLabels ? " is-monthly-chart" : ""}`}>
      <div className="student-panel__header">
        <div>
          <h2>{title}</h2>
          <p>{note}</p>
        </div>
      </div>
      <div className={`student-analytics-bars__scroll${useScrollableLayout ? " is-scrollable" : ""}`}>
        <div
          className="student-analytics-bars"
          style={useScrollableLayout
            ? {
                minWidth: `${chartMinWidth}px`,
                gridTemplateColumns: `repeat(${rows.length}, ${columnWidth}px)`
              }
            : {
                gridTemplateColumns: `repeat(${rows.length}, minmax(0, 1fr))`
              }}
        >
          {rows.map((item) => (
            <div key={item.id} className="student-analytics-bars__item">
              <div className="student-analytics-bars__column">
                <span
                  className={`student-analytics-bars__bar is-${tone}`}
                  style={{
                    height: getSeriesBarHeight(item[valueKey], maxValue)
                  }}
                  title={`${item.fullLabel || item.label}: ${formatChartTooltipValue(valueKey, item[valueKey])}`}
                />
              </div>
              <strong>{item.label}</strong>
            </div>
          ))}
        </div>
      </div>
    </article>
  );
}

export function KnowledgeAnalyticsPage() {
  const [analytics, setAnalytics] = useState(null);
  const [streak, setStreak] = useState(null);
  const [history, setHistory] = useState([]);
  const [courses, setCourses] = useState([]);
  const [period, setPeriod] = useState("week");
  const [courseFilter, setCourseFilter] = useState("All");
  const [errorMessage, setErrorMessage] = useState("");

  useEffect(() => {
    let isMounted = true;

    Promise.all([
      getStudentAnalytics(),
      getStudentStreak(),
      getStudentPracticeHistory({ limit: 100, courseId: "all", type: "All", fetchAll: true }),
      getStudentInsightCourses()
    ])
      .then(([analyticsResult, streakResult, historyResult, courseResult]) => {
        if (!isMounted) {
          return;
        }

        setAnalytics(analyticsResult);
        setStreak(streakResult);
        setHistory(Array.isArray(historyResult?.items) ? historyResult.items : []);
        setCourses(Array.isArray(courseResult) ? courseResult : []);
      })
      .catch((error) => {
        if (!isMounted) {
          return;
        }

        setErrorMessage(error.message || "Unable to load learning analytics.");
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const filteredHistory = useMemo(() => {
    const startDate = getRangeStart(period);
    return history.filter((item) => {
      const itemDate = new Date(item.date);
      const withinPeriod = itemDate >= startDate;
      const withinCourse = courseFilter === "All"
        ? true
        : item.courseId === courseFilter || item.courseName === courseFilter || courses.find((course) => course.code === courseFilter && (course.id === item.courseId || course.name === item.courseName));

      return withinPeriod && withinCourse;
    });
  }, [courseFilter, courses, history, period]);

  const trendRows = useMemo(() => buildTrendSeries(period, filteredHistory), [filteredHistory, period]);
  const heatmapRows = useMemo(() => buildHeatmap(history), [history]);
  const completionMeta = useMemo(() => getCompletionMeta(filteredHistory), [filteredHistory]);
  const courseRows = useMemo(() => buildCourseRows(courses, filteredHistory), [courses, filteredHistory]);

  const overview = useMemo(() => {
    if (!analytics || !streak) {
      return [];
    }

    const totalMinutes = filteredHistory
      .filter(isClosedSession)
      .reduce((total, item) => total + Number(item.durationMinutes || 0), 0);
    const lastPracticeDate = filteredHistory.length
      ? [...filteredHistory].sort((left, right) => new Date(right.date).getTime() - new Date(left.date).getTime())[0].date
      : null;

    return [
      { label: "Current Streak", value: `${streak.currentStreak} days`, note: "Current learning streak" },
      { label: "Best Streak", value: `${streak.highestStreak} days`, note: "Highest streak achieved" },
      { label: "AI Wallet Balance", value: formatWalletBalance(analytics.subscription?.balance || 0), note: "Current AI wallet balance" },
      { label: "Total Practice Sessions", value: formatNumber(filteredHistory.length), note: "Sessions in selected period" },
      { label: "Completed Sessions", value: formatNumber(completionMeta.completed), note: "Sessions submitted successfully" },
      { label: "Abandoned Sessions", value: formatNumber(completionMeta.abandoned), note: "Sessions left unfinished" },
      { label: "Total Study Time", value: formatDurationMinutes(totalMinutes), note: "Accumulated study time" },
      { label: "Last Practice Date", value: lastPracticeDate ? formatDateOnly(lastPracticeDate) : "-", note: "Most recent activity" }
    ];
  }, [analytics, completionMeta, filteredHistory, streak]);

  const feAnalytics = useMemo(() => {
    const feRows = filteredHistory.filter((item) => item.type === "FE");
    const totalCorrect = feRows.reduce((total, item) => total + Number(item.correctCount || 0), 0);
    const totalQuestions = feRows.reduce((total, item) => total + Number(item.totalQuestions || 0), 0);
    const difficultyRows = buildDifficultyRows(filteredHistory);

    return {
      totalSubmissions: feRows.length,
      totalCorrect,
      correctRate: totalQuestions ? (totalCorrect / totalQuestions) * 100 : 0,
      difficultyRows
    };
  }, [filteredHistory]);

  const peAnalytics = useMemo(() => {
    const peRows = filteredHistory.filter((item) => item.type === "PE");
    const verdictCounts = peRows.reduce((accumulator, item) => {
      const verdict = String(item.latestPeVerdict || "").toLowerCase();

      if (verdict === "accepted") {
        accumulator.accepted += 1;
      } else if (verdict === "compilationerror") {
        accumulator.compilationError += 1;
      } else if (verdict === "systemerror") {
        accumulator.systemError += 1;
      } else if (verdict) {
        accumulator.wrongAnswer += 1;
      } else if (Number(item.numericScore || 0) >= 70) {
        accumulator.accepted += 1;
      } else {
        accumulator.wrongAnswer += 1;
      }

      return accumulator;
    }, {
      accepted: 0,
      wrongAnswer: 0,
      compilationError: 0,
      systemError: 0
    });

    const verdictRows = [
      { label: "Accepted", value: verdictCounts.accepted, tone: "green" },
      { label: "Wrong Answer", value: verdictCounts.wrongAnswer, tone: "orange" },
      { label: "Compilation Error", value: verdictCounts.compilationError, tone: "slate" },
      { label: "System Error", value: verdictCounts.systemError, tone: "red" }
    ];

    return {
      totalSubmissions: peRows.length,
      totalPassed: verdictCounts.accepted,
      passRate: peRows.length ? (verdictCounts.accepted / peRows.length) * 100 : 0,
      verdictRows
    };
  }, [filteredHistory]);

  if (!analytics || !streak) {
    return null;
  }

  return (
    <StudentScaffold
      activeRoute={ROUTES.studentAnalytics}
      title="Learning Analytics"
      subtitle="Review consistency, FE and PE progress, and course-level learning activity."
      showPageHeader={false}
    >
      {errorMessage ? <section className="student-status-banner">{errorMessage}</section> : null}

      <section className="student-panel student-analytics-hero">
        <div>
          <span className="student-panel__eyebrow">Analytics Overview</span>
          <h2>Study consistency, performance, and weak areas in one workspace</h2>
          <p>{getRangeLabel(period, getRangeStart(period))}</p>
        </div>
        <div className="student-analytics-toolbar">
          <div className="student-analytics-segmented" role="tablist" aria-label="Analytics period">
            {PERIOD_OPTIONS.map((option) => (
              <button
                key={option.value}
                type="button"
                className={period === option.value ? "is-active" : ""}
                onClick={() => setPeriod(option.value)}
              >
                {option.label}
              </button>
            ))}
          </div>
          <label className="student-filter-field student-analytics-filter">
            <span>Course</span>
            <select value={courseFilter} onChange={(event) => setCourseFilter(event.target.value)}>
              {COURSE_ORDER.map((code) => (
                <option key={code} value={code}>{code}</option>
              ))}
            </select>
          </label>
        </div>
      </section>

      <section className="student-metric-grid student-metric-grid--analytics-extended">
        {overview.map((item) => (
          <SummaryCard key={item.label} label={item.label} value={item.value} note={item.note} />
        ))}
      </section>

      <section className="student-page-grid student-page-grid--analytics">
        <CompactSeriesChart
          title="Sessions in Selected Period"
          note="Track how often you practiced over time."
          rows={trendRows}
          valueKey="sessions"
          tone="blue"
        />
        <CompactSeriesChart
          title="Study Time Trend"
          note="Minutes spent across the selected period."
          rows={trendRows}
          valueKey="studyMinutes"
          tone="orange"
        />
      </section>

      <section className="student-page-grid student-page-grid--analytics">
        <article className="student-panel student-analytics-panel">
          <div className="student-panel__header">
            <div>
              <h2>FE Analytics</h2>
              <p>Multiple choice performance by difficulty and recurring weak areas.</p>
            </div>
          </div>
          <div className="student-analytics-kpi-row">
            <div><strong>{formatNumber(feAnalytics.totalSubmissions)}</strong><span>Total FE submissions</span></div>
          </div>
          <div className="student-analytics-difficulty">
            <div className="student-analytics-difficulty__head">
              <span>Range</span>
              <span>Sessions</span>
              <span>Share</span>
            </div>
            {feAnalytics.difficultyRows.map((item) => (
              <div key={item.id} className="student-analytics-difficulty__row">
                <strong>{item.label}</strong>
                <span>{formatNumber(item.count)}</span>
                <div className="student-analytics-difficulty__metric">
                  <MiniProgressBar value={item.rate} tone="orange" />
                  <small>{formatPercent(item.rate)}</small>
                </div>
              </div>
            ))}
          </div>
        </article>

        <article className="student-panel student-analytics-panel">
          <div className="student-panel__header">
            <div>
              <h2>PE Analytics</h2>
              <p>Coding performance, pass rate, verdict mix, and runtime quality.</p>
            </div>
          </div>
          <div className="student-analytics-kpi-row">
            <div><strong>{formatNumber(peAnalytics.totalSubmissions)}</strong><span>Total PE submissions</span></div>
            <div><strong>{formatNumber(peAnalytics.totalPassed)}</strong><span>Total PE passed</span></div>
            <div><strong>{formatPercent(peAnalytics.passRate)}</strong><span>PE pass rate</span></div>
          </div>
          <div className="student-analytics-verdicts">
            {peAnalytics.verdictRows.map((item) => (
              <div key={item.label} className="student-analytics-verdicts__item">
                <div className="student-analytics-verdicts__head">
                  <span>{item.label}</span>
                  <strong>{formatNumber(item.value)}</strong>
                </div>
                <MiniProgressBar value={peAnalytics.totalSubmissions ? (item.value / peAnalytics.totalSubmissions) * 100 : 0} tone={item.tone} />
              </div>
            ))}
          </div>
        </article>
      </section>

      <section className="student-page-grid student-page-grid--analytics">
        <article className="student-panel student-analytics-panel">
          <div className="student-panel__header">
            <div>
              <h2>Practice Heatmap</h2>
              <p>Spot active study days and consistency gaps at a glance.</p>
            </div>
          </div>
          <div className="student-analytics-heatmap">
            {heatmapRows.map((item) => (
              <div
                key={item.id}
                className={`student-analytics-heatmap__cell level-${Math.min(item.sessions, 4)}`}
                title={`${item.label}: ${item.sessions} session${item.sessions === 1 ? "" : "s"}`}
              />
            ))}
          </div>
          <div className="student-analytics-inline-stats">
            <div><strong>{formatNumber(heatmapRows.filter((item) => item.sessions > 0).length)}</strong><span>active days</span></div>
            <div><strong>{streak.currentStreak}</strong><span>current streak</span></div>
            <div><strong>{heatmapRows[heatmapRows.length - 1]?.label || "-"}</strong><span>latest day</span></div>
          </div>
        </article>

        <article className="student-panel student-analytics-panel">
          <div className="student-panel__header">
            <div>
              <h2>Course Analytics</h2>
              <p>See where you spend the most time and where you perform best across courses.</p>
            </div>
          </div>
          <div className="student-analytics-course-list">
            {courseRows.map((item) => (
              <div key={item.id} className="student-analytics-course-list__item">
                <div>
                  <strong>{item.code}</strong>
                  <span>{item.name}</span>
                </div>
                <div>
                  <label>Time spent</label>
                  <strong>{formatDurationMinutes(item.totalMinutes)}</strong>
                </div>
                <div>
                  <label>Accuracy</label>
                  <strong>{formatPercent(item.averageScore)}</strong>
                </div>
              </div>
            ))}
          </div>
        </article>
      </section>
    </StudentScaffold>
  );
}
