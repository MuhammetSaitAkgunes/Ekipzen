import { useEffect, useMemo, useRef, useState } from "react";
import { GuidanceDrawer } from "../guidance/GuidanceDrawer";
import { GuidanceSourceLink } from "../guidance/GuidanceSourceLink";
import { GuidanceStepLessons } from "../guidance/GuidanceStepLessons";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { appPath } from "../../paths";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { downloadCsv, tableToCsvLines } from "../shared/csv";
import { ImportModal } from "./ImportModal";
import { OffboardingModal } from "./OffboardingModal";
import { PersonnelRecordGuide } from "./PersonnelRecordGuide";

import { useDepartments, useEmployees, type EmployeeFilters } from "./queries";

const formatDate = (value?: string | null): string =>
  value ? new Date(value).toLocaleDateString("tr-TR") : "";


export function PersonnelPage() {
  const { user } = useAuth();
  const { showToast } = useToast();
  const isHrAdmin = user?.role === "hr-admin";

  const navigate = useNavigate();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const [searchInput, setSearchInput] = useState(params.get("q") ?? "");
  const [filters, setFilters] = useState<EmployeeFilters>({ search: params.get("q") ?? "", departmentId: params.get("department") ?? "", status: params.get("status") ?? "", page: Math.max(1, Math.floor(Number(params.get("page")) || 1)) });
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [importOpen, setImportOpen] = useState(false);
  const [offboardingId, setOffboardingId] = useState<number | null>(null);
  const tableRef = useRef<HTMLTableElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);

  // Arama debounce: 300ms sonra server-side filtreye yansır.
  useEffect(() => {
    const timer = setTimeout(
      () => setFilters((current) => current.search === searchInput ? current : ({ ...current, search: searchInput, page: 1 })),
      300,
    );
    return () => clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    const next = new URLSearchParams();
    if (filters.search) next.set("q", filters.search);
    if (filters.departmentId) next.set("department", filters.departmentId);
    if (filters.status) next.set("status", filters.status);
    if ((filters.page ?? 1) > 1) next.set("page", String(filters.page));
    setParams(next, { replace: true, state: location.state });
  }, [filters, setParams, location.state]);
  const openProfile = (id: number | null, edit = false) => {
    const scroll = document.getElementById("main-content")?.scrollTop ?? 0;
    navigate(location.pathname + location.search, { replace: true, state: { scroll } });
    navigate(appPath(id === null ? "/personnel/new" : `/personnel/${id}${edit ? "?edit=1" : ""}`), {
      state: { returnTo: location.pathname + location.search, scroll },
    });
  };
  const employeesQ = useEmployees(filters);
  useEffect(() => {
    if (!employeesQ.isSuccess) return;
    const main = document.getElementById("main-content");
    if (main) main.scrollTop = location.state?.scroll ?? 0;
  }, [employeesQ.isSuccess, location.state]);

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
            <h2>Çalışanlar</h2>
            <p>{employeesQ.data.total ?? items.length} çalışan</p>
          </div>
          <div className="toolbar-actions">
            <GuidanceDrawer title="Çalışanlar rehberi">
              <GuidanceSourceLink processKey="personnel" />
              <GuidanceStepLessons process="personnel" />
              <PersonnelRecordGuide
                resultCount={employeesQ.data.total ?? items.length}
                singleMatch={employeesQ.data.total === 1 && items.length === 1 && items[0].id != null
                  ? { id: items[0].id, name: items[0].name ?? "Çalışan" } : null}
                hasFilters={Boolean(filters.search.trim() || filters.departmentId || filters.status)}
                isHrAdmin={isHrAdmin}
                onFocusSearch={() => { document.querySelector<HTMLDialogElement>(".ez-guidance-drawer[open]")?.close(); searchRef.current?.focus(); }}
                onClearFilters={() => {
                  setSearchInput("");
                  setFilters({ search: "", departmentId: "", status: "", page: 1 });
                  setSelected(new Set());
                }}
                onOpenRecord={(id) => openProfile(id)}
              />
            </GuidanceDrawer>
          {isHrAdmin && (
            <>
              <button className="btn btn-secondary" onClick={() => setImportOpen(true)}>
                <i aria-hidden="true" className="fa-solid fa-file-import" /> Excel'den İçe Aktar
              </button>
              <button className="btn btn-primary" onClick={() => openProfile(null)}>
                <i aria-hidden="true" className="fa-solid fa-plus" /> Çalışan ekle
              </button>
            </>
          )}
          </div>
        </div>

        <div className="filter-bar surface">
          {Boolean(filters.search || filters.departmentId || filters.status) && <button type="button" className="btn btn-secondary" onClick={() => {
            setSearchInput("");
            setFilters({ search: "", departmentId: "", status: "", page: 1 });
            setSelected(new Set());
          }}>Filtreleri temizle</button>}
          <div className="search-wrapper">
            <i aria-hidden="true" className="fa-solid fa-magnifying-glass" />
            <label className="sr-only" htmlFor="personnel-search">Personel ara</label>
            <input
              id="personnel-search"
              ref={searchRef}
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
            onChange={(e) => setFilters((c) => ({ ...c, departmentId: e.target.value, page: 1 }))}
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
            onChange={(e) => setFilters((c) => ({ ...c, status: e.target.value, page: 1 }))}
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

                <th>İşe Giriş</th>

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
                        <button type="button" className="ez-person-link" onClick={() => openProfile(employee.id ?? -1)}>{employee.name}</button>
                        <small>{employee.title}</small>
                      </div>
                    </div>
                  </td>
                  <td data-label="Departman"><strong>{employee.department}</strong></td>

                  <td data-label="İşe giriş">{formatDate(employee.hireDate)}</td>

                  <td>
                    <span className={`badge badge-${employee.status}`}>
                      {employee.status === "active" ? "Aktif" : "Pasif"}
                    </span>
                  </td>
                  <td style={{ textAlign: "right" }} className="csv-skip">
                    <div className="row-actions">
                      <button className="btn-icon-sm" title="Görüntüle" aria-label={`${employee.name} kartını görüntüle`} onClick={() => openProfile(employee.id ?? -1)}>
                        <i aria-hidden="true" className="fa-regular fa-eye" />
                      </button>
                      {isHrAdmin && (
                        <button className="btn-icon-sm" title="Düzenle" aria-label={`${employee.name} kaydını düzenle`} onClick={() => openProfile(employee.id ?? -1, true)}>
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

      <div className="ez-pagination" aria-label="Çalışan listesi sayfaları">
        <span>{employeesQ.data.total ?? items.length} çalışan · Sayfa {filters.page ?? 1}</span>
        <div><button className="btn btn-secondary btn-sm" disabled={(filters.page ?? 1) <= 1} onClick={() => setFilters((f) => ({ ...f, page: (f.page ?? 1) - 1 }))}>Önceki</button>
        <button className="btn btn-secondary btn-sm" disabled={(filters.page ?? 1) >= (employeesQ.data.totalPages ?? 1)} onClick={() => setFilters((f) => ({ ...f, page: (f.page ?? 1) + 1 }))}>Sonraki</button></div>
      </div>
      <ImportModal open={importOpen} onClose={() => setImportOpen(false)} />

      {offboardingId !== null && (
        <OffboardingModal employeeId={offboardingId} onClose={() => setOffboardingId(null)} />
      )}


    </div>
  );
}
