import { useEffect, useMemo, useState } from "react";
import { Button } from "../../components/common";
import { AdminGlyph } from "../../components/admin/AdminGlyph";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import {
  activateCourse,
  createCourse,
  deactivateCourse,
  deleteCourse,
  getCourseById,
  getCourses,
  updateCourse
} from "../../services/courseService";
import { getAuthSession } from "../../lib/storage";
import { ROUTES } from "../../lib/routes";
import { ConfirmModal, ToastNotification } from "../../shared/ui";

export function CourseManagementPage({ isAuthenticated = false }) {
  const session = getAuthSession();
  const isAdmin = session?.user?.role?.toLowerCase() === "admin";
  const [courses, setCourses] = useState([]);
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [isLoading, setIsLoading] = useState(false);
  const [pagination, setPagination] = useState({ page: 1, limit: 5, total: 0, totalPages: 1 });
  const [toast, setToast] = useState(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [coursePendingDelete, setCoursePendingDelete] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [editingCourseId, setEditingCourseId] = useState(null);
  const [form, setForm] = useState({
    code: "",
    name: "",
    description: ""
  });
  useEffect(() => {
    let ignore = false;

    async function loadCourses() {
      if (!isAuthenticated) {
        setCourses([]);
        setPagination({ page: 1, limit: 5, total: 0, totalPages: 1 });
        return;
      }

      setIsLoading(true);

      try {
        const response = await getCourses({
          page: pagination.page,
          limit: pagination.limit
        });
        const nextItems = Array.isArray(response?.items) ? response.items : [];

        if (!ignore) {
          setCourses(nextItems);
          setPagination((current) => ({
            ...current,
            total: response?.total ?? nextItems.length,
            totalPages: response?.totalPages ?? 1
          }));
        }
      } catch {
        if (!ignore) {
          setCourses([]);
          setPagination((current) => ({
            ...current,
            total: 0,
            totalPages: 1
          }));
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadCourses();

    return () => {
      ignore = true;
    };
  }, [isAuthenticated, pagination.page, pagination.limit, statusFilter]);

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  const filteredCourses = useMemo(() => {
    const keyword = searchTerm.trim().toLowerCase();

    return courses.filter((course) => {
      const matchesSearch =
        !keyword ||
        course.code.toLowerCase().includes(keyword) ||
        course.name.toLowerCase().includes(keyword);

      const matchesStatus =
        statusFilter === "all" ||
        (statusFilter === "active" && course.isActive) ||
        (statusFilter === "inactive" && !course.isActive);

      return matchesSearch && matchesStatus;
    });
  }, [courses, searchTerm, statusFilter]);

  const summaryCards = useMemo(() => {
    const activeCourses = courses.filter((course) => course.isActive).length;

    return [
      {
        label: "Total Courses",
        value: String(pagination.total),
        meta: `${activeCourses} active on this page`
      },
      {
        label: "Current Page",
        value: String(pagination.page),
        meta: `of ${pagination.totalPages || 1} pages`
      },
      {
        label: "Visible Rows",
        value: String(filteredCourses.length),
        meta: isLoading ? "Syncing with server" : "After current filters"
      }
    ];
  }, [courses, filteredCourses.length, isLoading, pagination.page, pagination.total, pagination.totalPages]);

  function resetForm(nextForm) {
    setForm(
      nextForm || {
        code: "",
        name: "",
        description: ""
      }
    );
  }

  function showToast(type, message) {
    setToast({ type, message });
  }

  async function refreshCourses() {
    const response = await getCourses({
      page: pagination.page,
      limit: pagination.limit
    });

    const nextItems = response?.items?.length ? response.items : [];
    setCourses(nextItems);
    setPagination((current) => ({
      ...current,
      total: response?.total ?? nextItems.length,
      totalPages: response?.totalPages ?? 1
    }));
  }

  function openCreateModal(prefill = null) {
    setEditingCourseId(null);
    resetForm(
      prefill || {
        code: "",
        name: "",
        description: ""
      }
    );
    setIsModalOpen(true);
  }

  async function openEditModal(courseId) {
    try {
      const course = await getCourseById(courseId);
      setEditingCourseId(courseId);
      resetForm({
        code: course.code || "",
        name: course.name || "",
        description: course.description || ""
      });
      setIsModalOpen(true);
    } catch (error) {
      showToast("error", error.message || "Unable to load course details.");
    }
  }

  async function handleSubmit(event) {
    event.preventDefault();

    const payload = {
      code: form.code.trim(),
      name: form.name.trim(),
      description: form.description.trim()
    };

    if (!payload.name || !payload.code) {
      showToast("error", "Course code and name are required.");
      return;
    }

    setIsSubmitting(true);

    try {
      if (editingCourseId) {
        await updateCourse(editingCourseId, {
          name: payload.name,
          description: payload.description
        });
        showToast("success", "Course updated successfully.");
      } else {
        await createCourse(payload);
        showToast("success", "Course created successfully.");
      }

      setIsModalOpen(false);
      resetForm();
      await refreshCourses();
    } catch (error) {
      showToast("error", error.message || "Unable to save course right now.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleToggleStatus(course) {
    try {
      if (course.isActive) {
        await deactivateCourse(course.id);
        showToast("error", "Inactive");
      } else {
        await activateCourse(course.id);
        showToast("success", "Active");
      }

      await refreshCourses();
    } catch (error) {
      showToast("error", error.message || "Unable to update course status.");
    }
  }

  async function handleDelete() {
    if (!coursePendingDelete) {
      return;
    }

    try {
      await deleteCourse(coursePendingDelete.id);
      showToast("success", `${coursePendingDelete.code} deleted.`);
      setCoursePendingDelete(null);

      if (filteredCourses.length === 1 && pagination.page > 1) {
        setPagination((current) => ({ ...current, page: current.page - 1 }));
        return;
      }

      await refreshCourses();
    } catch (error) {
      showToast("error", error.message || "Unable to delete course.");
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.courseManagement}
      heroIcon="cap"
      heroTitle="Management"
      heroSubtitle="Course operations"
      title="Course Management"
      subtitle="Oversee and manage institutional course offerings and configurations."
    >
      <section className="admin-page-actions">
        <Button
          variant="primary"
          className="admin-add-button"
          disabled={!isAdmin}
          onClick={() => openCreateModal()}
        >
          <span className="admin-add-button__icon">
            <AdminGlyph kind="plus" />
          </span>
          Add Course
        </Button>
      </section>

          {!isAdmin ? (
            <div className="admin-role-banner">
              You can view courses here, but only admin accounts can create, edit, activate or delete them.
            </div>
          ) : null}

          <section className="admin-filter-panel">
            <label className="admin-search-box">
              <span className="admin-search-box__icon">
                <AdminGlyph kind="search" />
              </span>
              <input
                type="text"
                value={searchTerm}
                onChange={(event) => setSearchTerm(event.target.value)}
                placeholder="Search by course name or code..."
              />
            </label>

            <div className="admin-filter-group">
              <label className="admin-select-field">
                <span>Status:</span>
                <select
                  value={statusFilter}
                  onChange={(event) => {
                    setStatusFilter(event.target.value);
                    setPagination((current) => ({ ...current, page: 1 }));
                  }}
                >
                  <option value="all">All</option>
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </select>
              </label>
            </div>
          </section>

          <section className="admin-table-panel">
            <div className="admin-table admin-table--native-layout admin-table--native-courses">
              <table className="admin-table__native">
                <colgroup>
                  <col className="admin-table__col admin-table__col--course-code" />
                  <col className="admin-table__col admin-table__col--course-name" />
                  <col className="admin-table__col admin-table__col--course-description" />
                  <col className="admin-table__col admin-table__col--course-status" />
                  <col className="admin-table__col admin-table__col--course-actions" />
                </colgroup>
                <thead>
                  <tr className="admin-table__head">
                    <th scope="col">Code</th>
                    <th scope="col">Name</th>
                    <th scope="col">Description</th>
                    <th scope="col">Status</th>
                    <th scope="col">Actions</th>
                  </tr>
                </thead>
                <tbody className="admin-table__body">
                {filteredCourses.map((course) => (
                  <tr className="admin-course-row" key={course.id}>
                    <td><strong className="admin-course-row__code">{course.code}</strong></td>
                    <td><div className="admin-course-row__name">{course.name}</div></td>
                    <td><p className="admin-course-row__description">{course.description}</p></td>
                    <td><div className="admin-course-row__status">
                      <button
                        type="button"
                        className={`admin-status-switch${course.isActive ? " is-active" : ""}`}
                        aria-label={`Toggle ${course.name}`}
                        disabled={!isAdmin}
                        onClick={() => handleToggleStatus(course)}
                      >
                        <span />
                      </button>
                    </div></td>
                    <td><div className="admin-course-row__actions">
                      <button
                        type="button"
                        aria-label="Edit course"
                        disabled={!isAdmin}
                        onClick={() => openEditModal(course.id)}
                      >
                        <AdminGlyph kind="edit" />
                      </button>
                      <button
                        type="button"
                        aria-label="Delete course"
                        disabled={!isAdmin}
                        onClick={() => setCoursePendingDelete(course)}
                      >
                        <AdminGlyph kind="trash" />
                      </button>
                    </div></td>
                  </tr>
                ))}
                </tbody>
              </table>
              {!filteredCourses.length ? (
                <div className="admin-table__body">
                  <div className="admin-empty-state">No courses match the current filters.</div>
                </div>
              ) : null}
            </div>

            <div className="admin-table-panel__footer">
              <p>
                {isLoading
                  ? "Loading courses..."
                  : `Showing ${filteredCourses.length} of ${pagination.total} courses`}
              </p>

              <div className="admin-pagination">
                <button
                  type="button"
                  className={pagination.page <= 1 ? "is-disabled" : ""}
                  aria-label="Previous page"
                  disabled={pagination.page <= 1}
                  onClick={() =>
                    setPagination((current) => ({ ...current, page: Math.max(1, current.page - 1) }))
                  }
                >
                  &#8249;
                </button>
                {Array.from({ length: Math.min(pagination.totalPages || 1, 5) }, (_, index) => {
                  const pageNumber = index + 1;

                  return (
                    <button
                      key={pageNumber}
                      type="button"
                      className={pagination.page === pageNumber ? "is-active" : ""}
                      onClick={() => setPagination((current) => ({ ...current, page: pageNumber }))}
                    >
                      {pageNumber}
                    </button>
                  );
                })}
                {pagination.totalPages > 5 ? <span>...</span> : null}
                {pagination.totalPages > 5 ? (
                  <button
                    type="button"
                    onClick={() =>
                      setPagination((current) => ({ ...current, page: pagination.totalPages }))
                    }
                  >
                    {pagination.totalPages}
                  </button>
                ) : null}
                <button
                  type="button"
                  className={pagination.page >= pagination.totalPages ? "is-disabled" : ""}
                  aria-label="Next page"
                  disabled={pagination.page >= pagination.totalPages}
                  onClick={() =>
                    setPagination((current) => ({
                      ...current,
                      page: Math.min(pagination.totalPages, current.page + 1)
                    }))
                  }
                >
                  &#8250;
                </button>
              </div>
            </div>
          </section>

      <section className="admin-summary-grid">
        {summaryCards.map((card) => (
          <article key={card.label} className="admin-summary-card">
            <p>{card.label}</p>
            <div className="admin-summary-card__value">
              <strong>{card.value}</strong>
              <span>{card.meta}</span>
            </div>
          </article>
        ))}
      </section>

      {isModalOpen ? (
        <div className="admin-modal-backdrop" role="presentation" onClick={() => !isSubmitting && setIsModalOpen(false)}>
          <div
            className="admin-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="course-modal-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="admin-modal__header">
              <div>
                <h2 id="course-modal-title">
                  {editingCourseId ? "Edit Course" : "Add Course"}
                </h2>
                <p>Configure the academic course information and activation readiness.</p>
              </div>
              <button
                type="button"
                className="admin-modal__close"
                onClick={() => !isSubmitting && setIsModalOpen(false)}
                aria-label="Close modal"
              >
                ×
              </button>
            </div>

            <form className="admin-modal__form" onSubmit={handleSubmit}>
              <label className="admin-modal__field">
                <span>Course Code</span>
                <input
                  type="text"
                  value={form.code}
                  disabled={Boolean(editingCourseId)}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, code: event.target.value.toUpperCase() }))
                  }
                  placeholder="PRJ301"
                />
              </label>

              <label className="admin-modal__field">
                <span>Course Name</span>
                <input
                  type="text"
                  value={form.name}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, name: event.target.value }))
                  }
                  placeholder="Software Architecture"
                />
              </label>

              <label className="admin-modal__field admin-modal__field--full">
                <span>Description</span>
                <textarea
                  value={form.description}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, description: event.target.value }))
                  }
                  placeholder="Briefly describe the course outcomes and scope."
                />
              </label>
              <div className="admin-modal__actions">
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => !isSubmitting && setIsModalOpen(false)}
                >
                  Cancel
                </Button>
                <Button type="submit" variant="primary" disabled={isSubmitting}>
                  {isSubmitting ? "Saving..." : editingCourseId ? "Save Changes" : "Create Course"}
                </Button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      <ConfirmModal
        open={Boolean(coursePendingDelete)}
        title="Delete course"
        message={coursePendingDelete ? `Delete ${coursePendingDelete.code} - ${coursePendingDelete.name}?` : ""}
        confirmLabel="Delete"
        onCancel={() => setCoursePendingDelete(null)}
        onConfirm={handleDelete}
      />

      <ToastNotification toast={toast} />
    </AdminScaffold>
  );
}

