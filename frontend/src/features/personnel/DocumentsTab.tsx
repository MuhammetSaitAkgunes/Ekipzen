import { useRef, useState } from "react";
import { ApiError, apiDownload } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useEmployeeDocuments, useUploadDocument } from "./queries";

const formatSize = (bytes?: number): string =>
  bytes === undefined ? "" : bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;

export function DocumentsTab({ employeeId, readOnly }: { employeeId: number | null; readOnly: boolean }) {
  const { showToast } = useToast();
  const documentsQ = useEmployeeDocuments(employeeId);
  const upload = useUploadDocument();
  const [documentType, setDocumentType] = useState("");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
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
      await upload.mutateAsync({ id: employeeId, file: selectedFile, documentType: documentType.trim() });
      setSelectedFile(null);
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
            if (!upload.isPending) setSelectedFile(e.dataTransfer.files?.[0] ?? null);
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
            <input id="pm-doc-file" ref={fileInputRef} type="file" accept=".pdf,.jpg,.jpeg,.png,.doc,.docx" disabled={upload.isPending} className="input-control" aria-label="Evrak dosyası seç" onChange={(e) => setSelectedFile(e.target.files?.[0] ?? null)} />
          </div>
        </div>
      )}

      {!readOnly && selectedFile && <section className="ez-document-review" aria-labelledby="document-review-title" aria-live="polite">
        <h4 id="document-review-title">Yüklemeden önce kontrol edin</h4>
        <p><strong>Dosya:</strong> {selectedFile.name} · {formatSize(selectedFile.size)}</p>
        <p><strong>Evrak türü:</strong> {documentType.trim() || "Girilmedi"}</p>
        <p>Doğru çalışanın kartında olduğunuzu ve dosyanın gerçekten bu belgeye ait olduğunu doğrulayın. Gereksiz hassas bilgi yüklemeyin.</p>
        <p>Bu ekranda yüklenen dosyayı silme veya değiştirme işlemi henüz yok. Yanlış dosya yüklediyseniz erişimi ve düzeltme ihtiyacını yetkili İK yöneticisiyle değerlendirin.</p>
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
    </>
  );
}
