import { useEffect, useState } from "react";
import { Avatar } from "../../components/common";
import { AdminGlyph } from "../../components/admin/AdminGlyph";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { ROUTES } from "../../lib/routes";
import {
  getAdminUserById,
  getAdminUsers,
  updateAdminUserStatus
} from "../../services/adminUserService";
import { ToastNotification } from "../../shared/ui";

function getUserStatusErrorMessage(error) {
  const message = error?.message || "";

  if (message.toLowerCase().includes("own admin account") || message.toLowerCase().includes("own status")) {
    return "You cannot change the status of your own admin account.";
  }

  return message || "Unable to update user status.";
}

function stringEquals(a, b) {
  return String(a || "").trim().toLowerCase() === String(b || "").trim().toLowerCase();
}

export function AdminUsersPage({ isAuthenticated = false }) {
  const [filters, setFilters] = useState({ search: "", role: "all", status: "all" });
  const [users, setUsers] = useState([]);
  const [selectedUserId, setSelectedUserId] = useState("");
  const [selectedUser, setSelectedUser] = useState(null);
  const [pagination, setPagination] = useState({ page: 1, limit: 8, total: 0, totalPages: 1 });
  const [isLoading, setIsLoading] = useState(false);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [toast, setToast] = useState(null);

  useEffect(() => {
    let ignore = false;

    async function loadUsers() {
      if (!isAuthenticated) {
        return;
      }

      setIsLoading(true);

      try {
        const response = await getAdminUsers({
          ...filters,
          page: pagination.page,
          limit: pagination.limit
        });

        if (!ignore) {
          const nextItems = response?.items || [];
          setUsers(nextItems);
          setPagination((current) => ({
            ...current,
            total: response?.total || 0,
            totalPages: response?.totalPages || 1
          }));

          if (!selectedUserId && nextItems.length) {
            setSelectedUserId(nextItems[0].id);
          }
        }
      } catch (error) {
        if (!ignore) {
          showToast("error", error.message || "Unable to load admin users.");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadUsers();

    return () => {
      ignore = true;
    };
  }, [filters, isAuthenticated, pagination.limit, pagination.page, selectedUserId]);

  useEffect(() => {
    let ignore = false;

    async function loadUserDetail() {
      if (!selectedUserId) {
        setSelectedUser(null);
        return;
      }

      setIsDetailLoading(true);

      try {
        const detail = await getAdminUserById(selectedUserId);

        if (!ignore) {
          setSelectedUser(detail);
        }
      } catch (error) {
        if (!ignore) {
          showToast("error", error.message || "Unable to load user detail.");
        }
      } finally {
        if (!ignore) {
          setIsDetailLoading(false);
        }
      }
    }

    loadUserDetail();

    return () => {
      ignore = true;
    };
  }, [selectedUserId]);

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  function showToast(type, message) {
    setToast({ type, message });
  }

  async function refreshUsers() {
    const response = await getAdminUsers({
      ...filters,
      page: pagination.page,
      limit: pagination.limit
    });

    const nextItems = response?.items || [];
    setUsers(nextItems);
    setPagination((current) => ({
      ...current,
      total: response?.total || 0,
      totalPages: response?.totalPages || 1
    }));
  }

  async function handleInlineStatusToggle(user, event) {
    event.stopPropagation();

    if (!stringEquals(user.role, "Student")) {
      showToast("error", "Only Student accounts can be banned/unbanned.");
      return;
    }

    setIsSubmitting(true);

    try {
      const nextStatus = user.status?.toLowerCase() === "banned" ? "Active" : "Banned";
      await updateAdminUserStatus(user.id, nextStatus);
      showToast(
        nextStatus === "Banned" ? "error" : "success",
        nextStatus
      );

      await refreshUsers();

      if (selectedUserId === user.id) {
        const detail = await getAdminUserById(user.id);
        setSelectedUser(detail);
      }
    } catch (error) {
      showToast("error", getUserStatusErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.userManagement}
      heroIcon="user"
      heroTitle="Management"
      heroSubtitle="User registry"
      title="User Registry"
      subtitle="Review user accounts and moderate access status from one admin workspace."
    >
      <section className="admin-filter-panel">
        <label className="admin-search-box">
          <span className="admin-search-box__icon">
            <AdminGlyph kind="search" />
          </span>
          <input
            type="text"
            value={filters.search}
            onChange={(event) => {
              setFilters((current) => ({ ...current, search: event.target.value }));
              setPagination((current) => ({ ...current, page: 1 }));
            }}
            placeholder="Search by email or user name..."
          />
        </label>

        <div className="admin-filter-group">
          <label className="admin-select-field">
            <span>Role:</span>
            <select
              value={filters.role}
              onChange={(event) => {
                setFilters((current) => ({ ...current, role: event.target.value }));
                setPagination((current) => ({ ...current, page: 1 }));
              }}
            >
              <option value="all">All</option>
              <option value="Admin">Admin</option>
              <option value="Student">Student</option>
            </select>
          </label>

          <label className="admin-select-field">
            <span>Status:</span>
            <select
              value={filters.status}
              onChange={(event) => {
                setFilters((current) => ({ ...current, status: event.target.value }));
                setPagination((current) => ({ ...current, page: 1 }));
              }}
            >
              <option value="all">All</option>
              <option value="Active">Active</option>
              <option value="Banned">Banned</option>
            </select>
          </label>
        </div>
      </section>

      <section className="admin-split-layout">
        <section className="admin-table-panel">
          <div className="admin-table admin-table--users admin-table--native-layout admin-table--native-users">
            <table className="admin-table__native">
              <colgroup>
                <col className="admin-table__col admin-table__col--user-name" />
                <col className="admin-table__col admin-table__col--user-role" />
                <col className="admin-table__col admin-table__col--user-status" />
              </colgroup>
              <thead>
                <tr className="admin-table__head">
                  <th scope="col">User</th>
                  <th scope="col">Role</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody className="admin-table__body">
                {users.map((user) => (
                  // Only Student accounts can be moderated (ban/unban).
                  <tr
                    key={user.id}
                    className={`admin-course-row admin-course-row--users${selectedUserId === user.id ? " is-selected" : ""}`}
                    tabIndex={0}
                    onClick={() => setSelectedUserId(user.id)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        setSelectedUserId(user.id);
                      }
                    }}
                  >
                    <td><div>
                      <strong className="admin-course-row__name">{user.fullName || "Unnamed user"}</strong>
                      <p className="admin-course-row__description">{user.email}</p>
                    </div></td>
                    <td><div className="admin-pill">{user.role}</div></td>
                    <td><div className="admin-course-row__status">
                      <button
                        type="button"
                        className={`admin-status-switch${user.status !== "Banned" ? " is-active" : ""}`}
                        aria-label={`Toggle ${user.fullName || user.email}`}
                        aria-pressed={user.status !== "Banned"}
                        disabled={isSubmitting || !stringEquals(user.role, "Student")}
                        onClick={(event) => handleInlineStatusToggle(user, event)}
                      >
                        <span />
                      </button>
                    </div></td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!users.length ? (
              <div className="admin-table__body"><div className="admin-empty-state">{isLoading ? "Loading users..." : "No users found for the current filters."}</div></div>
            ) : null}
          </div>

          <div className="admin-table-panel__footer">
            <p>{isLoading ? "Syncing user registry..." : `Showing ${users.length} of ${pagination.total} users`}</p>
            <div className="admin-pagination">
              <button
                type="button"
                className={pagination.page <= 1 ? "is-disabled" : ""}
                disabled={pagination.page <= 1}
                onClick={() => setPagination((current) => ({ ...current, page: Math.max(1, current.page - 1) }))}
              >
                &#8249;
              </button>
              <button type="button" className="is-active">
                {pagination.page}
              </button>
              <button
                type="button"
                className={pagination.page >= pagination.totalPages ? "is-disabled" : ""}
                disabled={pagination.page >= pagination.totalPages}
                onClick={() =>
                  setPagination((current) => ({
                    ...current,
                    page: Math.min(pagination.totalPages || 1, current.page + 1)
                  }))
                }
              >
                &#8250;
              </button>
            </div>
          </div>
        </section>

        <aside className="admin-detail-panel">
          <div className="admin-detail-panel__header">
            <h2>User Detail</h2>
            <p>Inspect access level and apply moderation actions.</p>
          </div>

          {selectedUser ? (
            <div className="admin-detail-panel__body">
              <div className="admin-user-card">
                <Avatar
                  name={selectedUser.fullName || selectedUser.email}
                  src={selectedUser.avatarUrl || ""}
                  size="sm"
                  className="profile-mini-avatar"
                />
                <div>
                  <strong>{selectedUser.fullName || "Unnamed user"}</strong>
                  <p>{selectedUser.email}</p>
                </div>
              </div>

              <div className="admin-detail-grid">
                <div className="detail-tile">
                  <div className="detail-tile__icon">
                    <AdminGlyph kind="user" />
                  </div>
                  <div>
                    <p>Role</p>
                    <strong>{selectedUser.role}</strong>
                  </div>
                </div>
                <div className="detail-tile">
                  <div className="detail-tile__icon">
                    <AdminGlyph kind="spark" />
                  </div>
                  <div>
                    <p>Status</p>
                    <strong>{selectedUser.status}</strong>
                  </div>
                </div>
              </div>
            </div>
          ) : (
            <div className="admin-empty-state">{isDetailLoading ? "Loading user detail..." : "Select a user to inspect."}</div>
          )}
        </aside>
      </section>

      <ToastNotification toast={toast} />
    </AdminScaffold>
  );
}
