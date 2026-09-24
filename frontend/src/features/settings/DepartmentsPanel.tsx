import { useState, type FormEvent } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useDepartments } from "../personnel/queries";
import { useCreateDepartment, useUpdateDepartment } from "./departmentQueries";

type Draft = { id: number; name: string; code: string };

export function DepartmentsPanel() {
  const departmentsQ = useDepartments();
  const create = useCreateDepartment();
  const update = useUpdateDepartment();
  const { showToast } = useToast();
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [editing, setEditing] = useState<Draft | null>(null);
  const [error, setError] = useState("");

  const submitCreate = async (event: FormEvent) => {
    event.preventDefault();
    setError("");
    if (!name.trim()) { setError("Departman adını girin."); return; }
    try {
      await create.mutateAsync({ name: name.trim(), code: code.trim() || null });
      setName("");
      setCode("");
      showToast("Departman oluşturuldu; çalışan kartında seçilebilir.", "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Departman oluşturulamadı.");
    }
  };

  const submitEdit = async (event: FormEvent) => {
    event.preventDefault();
    if (!editing) return;
    setError("");
    if (!editing.name.trim()) { setError("Departman adını girin."); return; }
    try {
      await update.mutateAsync({ id: editing.id, name: editing.name.trim(), code: editing.code.trim() || null });
      setEditing(null);
      showToast("Departman bilgisi güncellendi.", "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Departman güncellenemedi.");
    }
  };

  return <section className="set-card mt-4 ez-departments-panel" aria-labelledby="settings-departments-title">
    <div className="card-head">
      <h3 id="settings-departments-title">Departmanlar</h3>
      <p>Çalışan kartında doğru ekibi seçebilmek için önce departmanları tanımlayın. Hatalı ad veya kodu daha sonra düzeltebilirsiniz.</p>
    </div>
    {error && <p className="form-error" role="alert">{error}</p>}
    <form className="ez-department-form" onSubmit={submitCreate}>
      <div className="input-group">
        <label htmlFor="new-department-name">Yeni departman adı</label>
        <input id="new-department-name" className="input-control" value={name} maxLength={128}
          onChange={(event) => setName(event.target.value)} />
      </div>
      <div className="input-group">
        <label htmlFor="new-department-code">Kısa kod (isteğe bağlı)</label>
        <input id="new-department-code" className="input-control" value={code} maxLength={32}
          onChange={(event) => setCode(event.target.value)} />
      </div>
      <button className="btn btn-primary" type="submit" disabled={create.isPending}>Departman ekle</button>
    </form>
    {departmentsQ.isPending ? <p>Departmanlar yükleniyor…</p>
      : departmentsQ.isError ? <div role="alert"><p>Departman listesi yüklenemedi.</p><button className="btn btn-secondary" onClick={() => void departmentsQ.refetch()}>Yeniden dene</button></div>
        : departmentsQ.data.length === 0 ? <p className="ez-department-empty">Henüz departman yok. Çalışan kartında departman seçmek için ilk departmanı ekleyin.</p>
          : <ul className="ez-department-list">
            {departmentsQ.data.map((department) => <li key={department.id}>
              {editing && editing.id === department.id ? <form className="ez-department-edit" onSubmit={submitEdit}>
                <p className="ez-department-edit-note">{department.employeeCount ?? 0} çalışan bu departmana bağlı. Adı veya kodu düzeltmek çalışanları başka departmana taşımaz.</p>
                <div className="input-group"><label htmlFor={`department-name-${department.id}`}>Departman adı</label>
                  <input id={`department-name-${department.id}`} className="input-control" maxLength={128}
                    value={editing.name} onChange={(event) => setEditing((current) => current ? { ...current, name: event.target.value } : current)} /></div>
                <div className="input-group"><label htmlFor={`department-code-${department.id}`}>Kısa kod</label>
                  <input id={`department-code-${department.id}`} className="input-control" maxLength={32}
                    value={editing.code} onChange={(event) => setEditing((current) => current ? { ...current, code: event.target.value } : current)} /></div>
                <div className="ez-department-actions">
                  <button className="btn btn-secondary btn-sm" type="button" disabled={update.isPending}
                    onClick={() => { setEditing(null); setError(""); }}>Vazgeç</button>
                  <button className="btn btn-primary btn-sm" type="submit" disabled={update.isPending}>Değişikliği kaydet</button>
                </div>
              </form> : <div className="ez-department-row">
                <div><strong>{department.name}</strong><small>{department.code || "Kod yok"} · {department.employeeCount ?? 0} çalışan</small></div>
                <button className="btn btn-secondary btn-sm" onClick={() => {
                  setError("");
                  setEditing({ id: department.id!, name: department.name ?? "", code: department.code ?? "" });
                }} disabled={update.isPending}>Düzenle</button>
              </div>}
            </li>)}
          </ul>}
  </section>;
}
