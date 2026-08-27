import { useEffect, useMemo, useState } from "react";
import { Button, Field } from "../../components/common";
import { AdminScaffold } from "../../components/admin/AdminScaffold";
import { ROUTES } from "../../lib/routes";
import {
  createAdminTopupPackage,
  getAdminTopupPackages,
  updateAdminTopupPackage,
  updateAdminTopupPackageStatus
} from "../../services/adminTopupPackageService";
import { ToastNotification } from "../../shared/ui";

function formatVnd(amount) {
  return Number(amount || 0).toLocaleString("vi-VN");
}

function findDuplicateAmount(packages, draft, selectedId) {
  const nextAmount = Number(draft.amountVnd || 0);
  if (nextAmount <= 0) {
    return null;
  }

  return packages.find((item) => item.amountVnd === nextAmount && item.id !== selectedId) || null;
}

const EMPTY_DRAFT = {
  id: "",
  code: "",
  name: "",
  description: "",
  amountVnd: 10000,
  sortOrder: 1,
  isActive: true,
  isFeatured: false
};

const PAGE_SIZE = 5;

export function AdminWalletPackagesPage() {
  const [packages, setPackages] = useState([]);
  const [selectedId, setSelectedId] = useState("");
  const [draft, setDraft] = useState(EMPTY_DRAFT);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [toast, setToast] = useState(null);
  const [page, setPage] = useState(1);

  const selectedPackage = useMemo(
    () => packages.find((item) => item.id === selectedId) || null,
    [packages, selectedId]
  );

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil(packages.length / PAGE_SIZE)),
    [packages.length]
  );

  const pagedPackages = useMemo(() => {
    const startIndex = (page - 1) * PAGE_SIZE;
    return packages.slice(startIndex, startIndex + PAGE_SIZE);
  }, [packages, page]);

  useEffect(() => {
    let ignore = false;

    async function loadPackages() {
      setIsLoading(true);
      try {
        const data = await getAdminTopupPackages();
        if (!ignore) {
          setPackages(data);
          setPage(1);
          if (data.length) {
            setSelectedId(data[0].id);
            setDraft(data[0]);
          }
        }
      } catch (error) {
        if (!ignore) {
          setToast({ type: "error", message: error.message || "Unable to load wallet packages." });
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadPackages();
    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  function handleCreateNew() {
    setSelectedId("");
    setDraft({
      ...EMPTY_DRAFT,
      sortOrder: packages.length + 1
    });
  }

  function handleSelect(item) {
    setSelectedId(item.id);
    setDraft(item);
  }

  async function refreshPackages(nextSelectedId = selectedId) {
    const data = await getAdminTopupPackages();
    setPackages(data);
    const nextTotalPages = Math.max(1, Math.ceil(data.length / PAGE_SIZE));
    const nextSelected = data.find((item) => item.id === nextSelectedId) || data[0] || null;
    const nextSelectedIndex = nextSelected ? data.findIndex((item) => item.id === nextSelected.id) : -1;
    const nextPage = nextSelectedIndex >= 0 ? Math.floor(nextSelectedIndex / PAGE_SIZE) + 1 : Math.min(page, nextTotalPages);
    setPage(nextPage);
    setSelectedId(nextSelected?.id || "");
    setDraft(nextSelected || { ...EMPTY_DRAFT, sortOrder: data.length + 1 });
  }

  async function handleSave() {
    const duplicateAmount = findDuplicateAmount(packages, draft, selectedId);
    if (duplicateAmount) {
      setToast({
        type: "error",
        message: `The amount ${formatVnd(draft.amountVnd)} VND is already used by package ${duplicateAmount.name}.`
      });
      return;
    }

    setIsSaving(true);
    setToast(null);

    const payload = {
      code: draft.code,
      name: draft.name,
      description: draft.description,
      amountVnd: Number(draft.amountVnd || 0),
      sortOrder: Number(draft.sortOrder || 0),
      isActive: Boolean(draft.isActive),
      isFeatured: Boolean(draft.isFeatured)
    };

    try {
      const saved = selectedId
        ? await updateAdminTopupPackage(selectedId, payload)
        : await createAdminTopupPackage(payload);
      await refreshPackages(saved.id);
      setToast({ type: "success", message: selectedId ? "Wallet package updated." : "Wallet package created." });
    } catch (error) {
      setToast({ type: "error", message: error.message || "Unable to save wallet package." });
    } finally {
      setIsSaving(false);
    }
  }

  async function handleToggleStatus(item) {
    setIsSaving(true);
    setToast(null);

    try {
      const updated = await updateAdminTopupPackageStatus(item.id, !item.isActive);
      await refreshPackages(updated.id);
      setToast({ type: "success", message: updated.isActive ? "Package activated." : "Package deactivated." });
    } catch (error) {
      setToast({ type: "error", message: error.message || "Unable to update wallet package status." });
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminWalletPackages}
      heroIcon="wallet"
      heroTitle="Management"
      heroSubtitle="Wallet packages"
      title="Wallet Packages"
      subtitle="Manage preset PayOS top-up packages available to students."
    >
      <ToastNotification toast={toast} />

      <section className="admin-split-layout">
        <section className="admin-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Package Catalog</h2>
              <p>{isLoading ? "Loading package catalog..." : `${packages.length} package(s) configured.`}</p>
            </div>
            <Button variant="secondary" onClick={handleCreateNew} disabled={isSaving}>
              New Package
            </Button>
          </div>

          <div className="wallet-package-admin-list">
            {!packages.length && !isLoading ? (
              <div className="admin-empty-state">No wallet packages configured yet.</div>
            ) : null}

            {pagedPackages.map((item) => (
              <button
                key={item.id}
                type="button"
                className={`wallet-package-admin-card${selectedId === item.id ? " is-selected" : ""}`}
                onClick={() => handleSelect(item)}
              >
                <div className="wallet-package-admin-card__head">
                  <div className="wallet-package-admin-card__identity">
                    <div className="wallet-package-admin-card__title-row">
                      <strong>{item.name}</strong>
                      <span className="wallet-package-admin-card__amount">{formatVnd(item.amountVnd)} VND</span>
                    </div>
                    <div className="wallet-package-admin-card__subhead">
                      <span>{item.code}</span>
                      <span>Order {item.sortOrder}</span>
                    </div>
                  </div>
                  <span className={`student-pill ${item.isActive ? "is-green" : "is-red"}`}>
                    {item.isActive ? "Active" : "Inactive"}
                  </span>
                </div>
                <p>{item.description || "No description yet."}</p>
                <div className="wallet-package-admin-card__meta">
                  <span className={`wallet-package-admin-card__kind${item.isFeatured ? " is-featured" : ""}`}>
                    {item.isFeatured ? "Featured package" : "Standard package"}
                  </span>
                  <span>{item.id ? `ID ${item.id.slice(-6).toUpperCase()}` : ""}</span>
                </div>
              </button>
            ))}
          </div>
          <div className="admin-table-panel__footer">
            <p>{isLoading ? "Loading package catalog..." : `Showing ${pagedPackages.length} of ${packages.length} packages`}</p>
            <div className="admin-pagination">
              <button
                type="button"
                className={page <= 1 ? "is-disabled" : ""}
                disabled={page <= 1 || isLoading}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                &#8249;
              </button>
              <button type="button" className="is-active">
                {page}
              </button>
              <button
                type="button"
                className={page >= totalPages ? "is-disabled" : ""}
                disabled={page >= totalPages || isLoading}
                onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
              >
                &#8250;
              </button>
            </div>
          </div>
        </section>

        <aside className="admin-detail-panel">
          <div className="admin-detail-panel__header">
            <h2>{selectedPackage ? "Edit Package" : "Create Package"}</h2>
            <p>Students only see packages that are marked active.</p>
          </div>

          <div className="admin-detail-panel__body">
            <div className="admin-ai-form-grid">
              <Field label="Code" hint="Stable key for backend and reporting.">
                <input className="ui-text-input" value={draft.code} onChange={(event) => setDraft((current) => ({ ...current, code: event.target.value }))} disabled={isSaving} />
              </Field>

              <Field label="Name">
                <input className="ui-text-input" value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} disabled={isSaving} />
              </Field>

              <Field label="Amount VND">
                <input className="ui-text-input" type="number" min="10000" step="1000" value={draft.amountVnd} onChange={(event) => setDraft((current) => ({ ...current, amountVnd: Number(event.target.value || 0) }))} disabled={isSaving} />
              </Field>

              <Field label="Sort Order">
                <input className="ui-text-input" type="number" min="0" step="1" value={draft.sortOrder} onChange={(event) => setDraft((current) => ({ ...current, sortOrder: Number(event.target.value || 0) }))} disabled={isSaving} />
              </Field>

              <Field label="Description" className="wallet-package-admin-field--full">
                <textarea className="ui-text-input" rows="4" value={draft.description} onChange={(event) => setDraft((current) => ({ ...current, description: event.target.value }))} disabled={isSaving} />
              </Field>
            </div>

            <div className="wallet-package-admin-flags">
              <label className="wallet-package-admin-flag">
                <input type="checkbox" checked={draft.isFeatured} onChange={(event) => setDraft((current) => ({ ...current, isFeatured: event.target.checked }))} disabled={isSaving} />
                <span>Featured package</span>
              </label>
              <label className="wallet-package-admin-flag">
                <input type="checkbox" checked={draft.isActive} onChange={(event) => setDraft((current) => ({ ...current, isActive: event.target.checked }))} disabled={isSaving} />
                <span>Active for students</span>
              </label>
            </div>

            <div className="admin-ai-actions admin-ai-actions--flush">
              <span className="admin-ai-inline-note">
                Payment history stores a snapshot of package metadata at checkout time.
              </span>
              <div className="admin-ai-actions__group">
                {selectedPackage ? (
                  <Button variant="ghost" onClick={() => handleToggleStatus(selectedPackage)} disabled={isSaving}>
                    {selectedPackage.isActive ? "Deactivate" : "Activate"}
                  </Button>
                ) : null}
                <Button variant="primary" onClick={handleSave} disabled={isSaving}>
                  {isSaving ? "Saving..." : selectedPackage ? "Save Package" : "Create Package"}
                </Button>
              </div>
            </div>
          </div>
        </aside>
      </section>
    </AdminScaffold>
  );
}
