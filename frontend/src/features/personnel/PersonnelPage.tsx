import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { downloadCsv, tableToCsvLines } from "../shared/csv";
import { ImportModal } from "./ImportModal";
import { OffboardingModal } from "./OffboardingModal";
import { PersonnelModal } from "./PersonnelModal";
import { useDepartments, useEmployees, type EmployeeFilters } from "./queries";

const formatDate = (value?: string | null): string =>
  value ? new Date(value).toLocaleDateString("tr-TR") : "";


export function PersonnelPage() {
  const { user } = useAuth();
  const { showToast } = useToast();
  const isHrAdmin = user?.role === "hr-admin";

  const [searchInput, setSearchInput] = useState("");
  const [filters, setFilters] = useState<EmployeeFilters>({ search: "", departmentId: "", status: "" });
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [cardId, setCardId] = useState<number | null | undefined>(undefined); // undefined: kapalı, null: yeni kayıt
  const [importOpen, setImportOpen] = useState(false);
  const [offboardingId, setOffboardingId] = useState<number | null>(null);
  const tableRef = useRef<HTMLTableElement>(null);

  // Arama debounce: 300ms sonra server-side filtreye yansır.
  useEffect(() => {
    const timer = setTimeout(
      () => setFilters((current) => ({ ...current, search: searchInput })),
      300,
    );
    return () => clearTimeout(timer);
  }, [searchInput]);

  const employeesQ = useEmployees(filters);
  const departmentsQ = useDepartments();

  const items = useMemo(() => employeesQ.data?.items ?? [], [employeesQ.data]);

  if (employeesQ.isPending || departmentsQ.isPending) return <PageLoading />;
  if (employeesQ.isError) return <PageError error={employeesQ.error} />;
  if (departmentsQ.isError) return <PageError error={departmentsQ.error} />;

  const departments = departmentsQ.data;
  const allVisibleSelected = items.length > 0 && items.every((e) => selected.has(e.id ?? -1));

  const toggleAll = (checked: boolean) =>
    setSelected(checked ? new Set(items.map((e) => e.id ?? -1)) : new Set());

  const toggleOne = (id: number, checked: boolean) =>
    setSelected((current) => {
      const next = new Set(current);
      if (checked) next.add(id); else next.delete(id);
      return next;
    });

  const exportCsv = () => {
    if (!tableRef.current) return;
    const hasSelection = selected.size > 0;
    const lines = tableToCsvLines(tableRef.current, (row) => {
      if (row.closest("thead")) return true;
      if (!hasSelection) return true;
      return selected.has(Number(row.dataset.id));
    });
    downloadCsv(lines, hasSelection ? "personel-secili" : "personel-listesi");
    showToast("CSV raporu indirildi.", "success");
  };

  return (
    <div id="personnel-screen">
      <div id="list-screen">
        <div className="page-header">
          <div>
            <h2>Personel Yönetimi</h2>
            <p>Sicil, özlük, iletişim ve kurumsal bilgileri tek ekrandan yönetin.</p>
          </div>
          {isHrAdmin && (
            <div className="toolbar-actions">
              <button className="btn btn-secondary" onClick={() => setImportOpen(true)}>
                <i aria-hidden="true" className="fa-solid fa-file-import" /> Excel'den İçe Aktar
              </button>
              <button className="btn btn-primary" onClick={() => setCardId(null)}>
                <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Personel
              </button>
            </div>
          )}
        </div>

        <div className="filter-bar surface">
          <div className="search-wrapper">
            <i aria-hidden="true" className="fa-solid fa-magnifying-glass" />
            <label className="sr-only" htmlFor="personnel-search">Personel ara</label>
            <input
              id="personnel-search"
              type="text"
              className="search-input"
              placeholder="Ad, departman veya görev ara"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
          </div>
          <label className="sr-only" htmlFor="personnel-dept-filter">Departman filtresi</label>
          <select
            id="personnel-dept-filter"
            className="filter-select"
            value={filters.departmentId}
            onChange={(e) => setFilters((c) => ({ ...c, departmentId: e.target.value }))}
          >
            <option value="">Departman: Tümü</option>
            {departments.map((dept) => (
              <option key={dept.id} value={dept.id}>{dept.name}</option>
            ))}
          </select>
          <label className="sr-only" htmlFor="personnel-status-filter">Durum filtresi</label>
          <select
            id="personnel-status-filter"
            className="filter-select"
            value={filters.status}
            onChange={(e) => setFilters((c) => ({ ...c, status: e.target.value }))}
          >
            <option value="">Durum: Tümü</option>
            <option value="active">Aktif</option>
            <option value="passive">Pasif</option>
          </select>
          <button className="btn btn-secondary" onClick={exportCsv}>
            <i aria-hidden="true" className="fa-solid fa-file-excel" /> Dışa Aktar
          </button>
        </div>

        <div id="personnel-bulk-bar" className="bulk-bar surface" hidden={selected.size === 0}>
          <strong id="personnel-bulk-count">{selected.size} kişi seçildi</strong>
          <div className="toolbar-actions">
            <button className="btn btn-secondary btn-sm" onClick={exportCsv}>
              <i aria-hidden="true" className="fa-solid fa-file-excel" /> Seçilenleri dışa aktar
            </button>

            <button className="btn btn-ghost btn-sm" onClick={() => setSelected(new Set())}>Seçimi temizle</button>
          </div>
        </div>

        <div className="table-container">
          <table className="pro-table" id="personnel-table" ref={tableRef}>
            <thead>
              <tr>
                <th className="csv-skip check-col">
                  <input
                    type="checkbox"
                    aria-label="Tüm personeli seç"
                    checked={allVisibleSelected}
                    onChange={(e) => toggleAll(e.target.checked)}
                  />
                </th>
                <th>Personel</th>
                <th>Departman</th>
                <th>TC Kimlik</th>
                <th>İşe Giriş</th>
                <th>İşten Ayrılma</th>
                <th>Durum</th>
                <th style={{ textAlign: "right" }} className="csv-skip">İşlemler</th>
              </tr>
            </thead>
            <tbody>
              {items.map((employee) => (
                <tr key={employee.id} data-id={employee.id}>
                  <td className="csv-skip check-col">
                    <input
                      type="checkbox"
                      className="personnel-row-check"
                      aria-label={`${employee.name} kaydını seç`}
                      checked={selected.has(employee.id ?? -1)}
                      onChange={(e) => toggleOne(employee.id ?? -1, e.target.checked)}
                    />
                  </td>
                  <td>
                    <div className="user-meta">
                      <div className="avatar-sm" aria-hidden="true">{employee.initials}</div>
                      <div className="meta-info">
                        <strong>{employee.name}</strong>
                        <small>{employee.title}</small>
                      </div>
                    </div>
                  </td>
                  <td><strong>{employee.department}</strong></td>
                  <td className="mono">{employee.nationalIdMasked}</td>
                  <td>{formatDate(employee.hireDate)}</td>
                  <td>{formatDate(employee.terminationDate)}</td>
                  <td>
                    <span className={`badge badge-${employee.status}`}>
                      {employee.status === "active" ? "Aktif" : "Pasif"}
                    </span>
                  </td>
                  <td style={{ textAlign: "right" }} className="csv-skip">
                    <div className="row-actions">
                      <button className="btn-icon-sm" title="Görüntüle" aria-label={`${employee.name} kartını görüntüle`} onClick={() => setCardId(employee.id ?? -1)}>
                        <i aria-hidden="true" className="fa-regular fa-eye" />
                      </button>
                      {isHrAdmin && (
                        <button className="btn-icon-sm" title="Düzenle" aria-label={`${employee.name} kaydını düzenle`} onClick={() => setCardId(employee.id ?? -1)}>
                          <i aria-hidden="true" className="fa-solid fa-pen" />
                        </button>
                      )}
                      {isHrAdmin && (
                        <button
                          className="btn-icon-sm"
                          title={employee.status === "active" ? "İşten çıkış başlat" : "İşten çıkış dosyası"}
                          aria-label={`${employee.name} için işten çıkış dosyasını aç`}
                          onClick={() => setOffboardingId(employee.id ?? -1)}
                        >
                          <i aria-hidden="true" className={employee.status === "active"
                            ? "fa-solid fa-user-slash"
                            : "fa-solid fa-folder-open"} />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div id="personnel-empty" className="empty-state" hidden={items.length !== 0}>
            <i aria-hidden="true" className="fa-solid fa-user-slash" />
            <h3>Eşleşen personel bulunamadı</h3>
            <p>Arama veya filtre ölçütlerini değiştirerek yeniden deneyin.</p>
          </div>
        </div>
      </div>

      <ImportModal open={importOpen} onClose={() => setImportOpen(false)} />

      {offboardingId !== null && (
        <OffboardingModal employeeId={offboardingId} onClose={() => setOffboardingId(null)} />
      )}

      {cardId !== undefined && (
        <PersonnelModal
          employeeId={cardId}
          readOnly={!isHrAdmin}
          onClose={() => setCardId(undefined)}
        />
      )}
    </div>
  );
}
