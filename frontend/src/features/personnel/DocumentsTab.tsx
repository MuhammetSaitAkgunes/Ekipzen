import { useRef, useState } from "react";
import { ApiError, apiDownload } from "../../api/client";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useToast } from "../../layout/ToastProvider";
import { useEmployeeDocuments, useUploadDocument, useWithdrawDocument, useWithdrawnEmployeeDocuments } from "./queries";
import type { EmployeeDocumentDto } from "./queries";

const formatSize = (bytes?: number): string =>
  bytes === undefined ? "" : bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;

export function DocumentsTab({ employeeId, readOnly }: { employeeId: number | null; readOnly: boolean }) {
  const { showToast } = useToast();
  const documentsQ = useEmployeeDocuments(employeeId);
  const withdrawnQ = useWithdrawnEmployeeDocuments(employeeId, !readOnly);
  const upload = useUploadDocument();
  const withdraw = useWithdrawDocument();
  const [documentType, setDocumentType] = useState("");
  const [uploadGuidanceVersion, setUploadGuidanceVersion] = useState<number | null>(null);
  const [withdrawGuidanceVersion, setWithdrawGuidanceVersion] = useState<number | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [withdrawTarget, setWithdrawTarget] = useState<EmployeeDocumentDto | null>(null);
  const [withdrawReason, setWithdrawReason] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  if (employeeId === null) {
    return (
      <div className="upload-drop">
        <i aria-hidden="true" className="fa-solid fa-cloud-arrow-up" />
        <h4>Dosyaları sürükleyip bırakın</h4>
        <p>Evrak yüklemek için önce personel kaydını oluşturun.</p>
      </div>
    );
  }

  const handleUpload = async () => {
    if (!selectedFile || upload.isPending) return;
    if (!documentType.trim()) {
      showToast("Evrak türünü yazın.", "warning");
      return;
    }
    try {
      await upload.mutateAsync({ id: employeeId, file: selectedFile, documentType: documentType.trim(), guidanceContentRevisionId: uploadGuidanceVersion });
      setSelectedFile(null);
      setUploadGuidanceVersion(null);
      setDocumentType("");
      if (fileInputRef.current) fileInputRef.current.value = "";
      showToast("Evrak yüklendi. Listedeki dosyayı indirip içeriğini kontrol edin.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Evrak yüklenemedi.", "error");
    }
  };

  const handleDownload = async (documentId: number, fallbackName: string) => {
    try {
      const { blob, fileName } = await apiDownload(`/employees/${employeeId}/documents/${documentId}`);
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = fileName ?? fallbackName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(link.href);
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Evrak indirilemedi.", "error");
    }
  };

  const handleWithdraw = async () => {
    if (!withdrawTarget?.id || !withdrawReason.trim() || withdraw.isPending) return;
    try {
      await withdraw.mutateAsync({
        id: employeeId, documentId: withdrawTarget.id, reason: withdrawReason.trim(), guidanceContentRevisionId: withdrawGuidanceVersion,
      });
      setWithdrawTarget(null);
      setWithdrawGuidanceVersion(null);
      setWithdrawReason("");
      showToast("Evrak geri çekildi. Doğru dosyayı ayrıca yükleyebilirsiniz.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Evrak geri çekilemedi; listeyi yenileyip tekrar kontrol edin.", "error");
    }
  };

  return (
    <>
      {!readOnly && (
        <div
          className="upload-drop"
          role="button"
          tabIndex={0}
          onClick={() => { if (!upload.isPending) fileInputRef.current?.click(); }}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              if (!upload.isPending) fileInputRef.current?.click();
            }
          }}
          onDragOver={(e) => e.preventDefault()}
          onDrop={(e) => {
            e.preventDefault();
            if (!upload.isPending) { setSelectedFile(e.dataTransfer.files?.[0] ?? null); setUploadGuidanceVersion(null); }
          }}
        >
          <i aria-hidden="true" className="fa-solid fa-cloud-arrow-up" />
          <h4>Dosyaları sürükleyip bırakın</h4>
          <p>Dosyayı seçin; yüklemeden önce türünü ve içeriğini doğrulayın.</p>
        </div>
      )}
      {!readOnly && (
        <div className="form-grid">
          <div className="input-group col-6">
            <label className="input-label" htmlFor="pm-doc-type">Evrak Türü</label>
            <input id="pm-doc-type" type="text" className="input-control" placeholder="Belgedeki adı yazın" value={documentType} onChange={(e) => setDocumentType(e.target.value)} />
          </div>
          <div className="input-group col-6">
            <label className="input-label" htmlFor="pm-doc-file">Evrak dosyası seç</label>
            <input id="pm-doc-file" ref={fileInputRef} type="file" accept=".pdf,.jpg,.jpeg,.png,.doc,.docx" disabled={upload.isPending} className="input-control" aria-label="Evrak dosyası seç" onChange={(e) => { setSelectedFile(e.target.files?.[0] ?? null); setUploadGuidanceVersion(null); }} />
          </div>
        </div>
      )}

      {!readOnly && selectedFile && <section className="ez-document-review" aria-labelledby="document-review-title" aria-live="polite">
        <h4 id="document-review-title">Yüklemeden önce kontrol edin</h4>
        <GuidanceOperationSource process="personnel-entry" step="tab-evrak" value={uploadGuidanceVersion} onChange={setUploadGuidanceVersion} disabled={upload.isPending} />
        <p><strong>Dosya:</strong> {selectedFile.name} · {formatSize(selectedFile.size)}</p>
        <p><strong>Evrak türü:</strong> {documentType.trim() || "Girilmedi"}</p>
        <p>Doğru çalışanın kartında olduğunuzu ve dosyanın gerçekten bu belgeye ait olduğunu doğrulayın. Gereksiz hassas bilgi yüklemeyin.</p>
        <p>Yanlış yüklediğiniz evrakı gerekçeyle geri çekip doğru dosyayı ayrıca yükleyebilirsiniz. Geri çekme geçmişte görünür; dosyayı kalıcı olarak silmez.</p>
        <div className="ez-document-review-actions">
          <button className="btn btn-ghost" onClick={() => {
            setSelectedFile(null);
            if (fileInputRef.current) {
              fileInputRef.current.value = "";
              fileInputRef.current.focus();
            }
          }} disabled={upload.isPending}>Seçimi kaldır</button>
          <button className="btn btn-primary" onClick={handleUpload} disabled={upload.isPending}>Dosyayı yükle</button>
        </div>
      </section>}

      {documentsQ.isPending && <p role="status">Evrak listesi yükleniyor…</p>}
      {documentsQ.isError && <p className="form-error" role="alert">Evrak listesi okunamadı; boş olduğu sonucuna varmayın.</p>}
      {!documentsQ.isPending && !documentsQ.isError && <div className="table-container">
        <table className="detail-table data-table">
          <thead>
            <tr><th>Evrak Türü</th><th>Dosya</th><th>Boyut</th><th>Yüklenme</th><th style={{ textAlign: "right" }}>İşlem</th></tr>
          </thead>
          <tbody>
            {(documentsQ.data ?? []).map((doc) => (
              <tr key={doc.id}>
                <td><strong>{doc.documentType}</strong></td>
                <td>{doc.fileName}</td>
                <td>{formatSize(doc.sizeBytes)}</td>
                <td>{doc.createdAtUtc ? new Date(doc.createdAtUtc).toLocaleDateString("tr-TR") : ""}</td>
                <td style={{ textAlign: "right" }}>
                  <button className="btn-icon-sm" title="İndir" aria-label={`${doc.fileName} dosyasını indir`} onClick={() => handleDownload(doc.id ?? 0, doc.fileName ?? "evrak")}>
                    <i aria-hidden="true" className="fa-solid fa-download" />
                  </button>
                  {!readOnly && <button className="btn-icon-sm" title="Geri çek" aria-label={`${doc.fileName} dosyasını geri çek`}
                    onClick={() => { setWithdrawTarget(doc); setWithdrawReason(""); setWithdrawGuidanceVersion(null); }}>
                    <i aria-hidden="true" className="fa-solid fa-rotate-left" />
                  </button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {(documentsQ.data ?? []).length === 0 && (
          <div className="empty-state">
            <i aria-hidden="true" className="fa-regular fa-folder-open" />
            <h3>Henüz evrak yok</h3>
            <p>Bu personel için yüklenmiş özlük evrakı bulunmuyor.</p>
          </div>
        )}
      </div>}
      {!readOnly && withdrawTarget && <section className="ez-document-review" aria-labelledby="document-withdraw-title">
        <GuidanceOperationSource process="personnel-entry" step="tab-evrak" value={withdrawGuidanceVersion} onChange={setWithdrawGuidanceVersion} disabled={withdraw.isPending} />
        <h4 id="document-withdraw-title">Evrakı geri çekmeden önce kontrol edin</h4>
        <p><strong>Dosya:</strong> {withdrawTarget.fileName} · <strong>Tür:</strong> {withdrawTarget.documentType}</p>
        <p>Bu evrak normal listeden ve indirmeden çıkar; gerekçe ve işlem geçmişi korunur. Geri çekme dosyayı kalıcı olarak silmez. Doğru dosyayı daha sonra yükleyin.</p>
        <label className="input-label" htmlFor="document-withdraw-reason">Geri çekme gerekçesi</label>
        <textarea id="document-withdraw-reason" className="input-control" maxLength={500}
          value={withdrawReason} onChange={(e) => setWithdrawReason(e.target.value)} />
        <div className="ez-document-review-actions">
          <button className="btn btn-ghost" onClick={() => { setWithdrawTarget(null); setWithdrawReason(""); }} disabled={withdraw.isPending}>Vazgeç</button>
          <button className="btn btn-primary" onClick={handleWithdraw} disabled={withdraw.isPending || !withdrawReason.trim()}>Geri çekmeyi onayla</button>
        </div>
      </section>}
      {!readOnly && <section aria-label="Geri çekilen evraklar">
        <h4>Geri çekilen evraklar</h4>
        {withdrawnQ.isPending && <p role="status">Geri çekme geçmişi yükleniyor…</p>}
        {withdrawnQ.isError && <p role="alert">Geri çekme geçmişi okunamadı; daha sonra tekrar kontrol edin.</p>}
        {withdrawnQ.data?.length === 0 && <p>Geri çekilmiş evrak yok.</p>}
        {(withdrawnQ.data ?? []).map((doc) => <p key={doc.id}>
          <strong>{doc.fileName}</strong> ({doc.documentType}) · {doc.withdrawnAtUtc ? new Date(doc.withdrawnAtUtc).toLocaleDateString("tr-TR") : ""}: {doc.withdrawalReason}
        </p>)}
      </section>}
    </>
  );
}
