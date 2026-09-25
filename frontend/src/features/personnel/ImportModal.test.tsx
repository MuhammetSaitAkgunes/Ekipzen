import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { ImportModal } from "./ImportModal";

const rapor = {
  toplamSatir: 3,
  gecerliSatir: 1,
  hataliSatir: 2,
  mukerrerSatir: 0,
  bilinmeyenDepartmanlar: ["Olmayan Departman"],
  sorunlar: [{ satirNo: 3, alan: "Ad", mesaj: "Zorunlu alan." }],
};

beforeEach(() => stubApi({
  "/api/employees/import/preview": rapor,
  "/api/employees/import": { olusturulanSatir: 1, atlananSatir: 2, sorunlar: rapor.sorunlar },
}));
afterEach(() => vi.unstubAllGlobals());

const render = () =>
  renderPage(
    <ToastProvider>
      <ImportModal open onClose={() => {}} />
    </ToastProvider>,
  );

const dosyaSec = async (name = "personel.xlsx") => {
  await userEvent.upload(
    screen.getByLabelText(/Excel dosyası/i),
    new File(["x"], name, {
      type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    }),
  );
};

const importCalls = () => vi.mocked(fetch).mock.calls.filter(
  ([url, init]) => String(url) === "/api/employees/import" && init?.method === "POST",
);

test("önizleme dosyayı kaydetmeden sorunları ve varsayımları açıklar", async () => {
  render();
  await dosyaSec();
  expect(await screen.findByText("Zorunlu alan.")).toBeInTheDocument();
  expect(screen.getByText("Olmayan Departman")).toBeInTheDocument();
  expect(screen.getByText(/aktarılan herkes belirsiz süreli olarak kaydedilir/)).toBeInTheDocument();
  expect(screen.getByText(/yalnız 1 geçerli satır aktarılır/)).toBeInTheDocument();
  expect(importCalls()).toHaveLength(0);
});

test("geçerli satır yoksa ön kontrol açılamaz", async () => {
  stubApi({ "/api/employees/import/preview": { ...rapor, gecerliSatir: 0, hataliSatir: 3 } });
  render();
  await dosyaSec();
  expect(await screen.findByText(/Aktarılabilir satır yok/)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeDisabled();
});

test("dosya seçilmeden ön kontrol açılamaz", () => {
  render();
  expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeDisabled();
});

test("kısmi aktarım için üç açık doğrulama gerekir ve gerçek sonuç gösterilir", async () => {
  render();
  await dosyaSec();
  await screen.findByText("Zorunlu alan.");
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  expect(screen.getByRole("region", { name: "Aktarmadan önce kontrol edin" })).toHaveTextContent("1");
  const confirm = screen.getByRole("button", { name: "Kontrol ettim, aktar" });
  expect(confirm).toBeDisabled();
  expect(importCalls()).toHaveLength(0);
  await userEvent.click(screen.getByRole("checkbox", { name: /Şablondaki örnek personel satırını sildim/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Tüm aktarılacak çalışanların belirsiz süreli/ }));
  expect(confirm).toBeDisabled();
  await userEvent.click(screen.getByRole("checkbox", { name: /Hatalı ve mükerrer satırlar atlanırken/ }));
  expect(confirm).toBeEnabled();
  await userEvent.click(confirm);
  await waitFor(() => expect(importCalls()).toHaveLength(1));
  expect(await screen.findByText("Gerçek aktarım sonucu")).toBeInTheDocument();
  expect(screen.getByText(/personel kaydı oluşturuldu;/)).toBeInTheDocument();
  expect(screen.getByText(/belge ve ücret adımlarını ayrıca tamamlayın/)).toBeInTheDocument();
});

test("düzeltilmiş dosya yeniden seçilince önceki ön kontrol geçersiz olur", async () => {
  render();
  await dosyaSec("ilk.xlsx");
  await screen.findByText("Zorunlu alan.");
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Şablondaki örnek personel satırını sildim/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Tüm aktarılacak çalışanların belirsiz süreli/ }));
  await dosyaSec("duzeltilmis.xlsx");
  expect(screen.queryByRole("region", { name: "Aktarmadan önce kontrol edin" })).not.toBeInTheDocument();
  await waitFor(() => expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeEnabled());
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  expect(screen.getByRole("region", { name: "Aktarmadan önce kontrol edin" })).toHaveTextContent("duzeltilmis.xlsx");
  expect(screen.getByRole("checkbox", { name: /Şablondaki örnek personel satırını sildim/ })).not.toBeChecked();
  expect(screen.getByRole("button", { name: "Kontrol ettim, aktar" })).toBeDisabled();
  expect(importCalls()).toHaveLength(0);
});

test("önizleme ile gerçek sonuç farklıysa farkı açıklar", async () => {
  stubApi({
    "/api/employees/import/preview": { ...rapor, gecerliSatir: 3, hataliSatir: 0, bilinmeyenDepartmanlar: [], sorunlar: [] },
    "/api/employees/import": { olusturulanSatir: 2, atlananSatir: 1, sorunlar: [] },
  });
  render();
  await dosyaSec();
  await waitFor(() => expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeEnabled());
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Şablondaki örnek personel satırını sildim/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Tüm aktarılacak çalışanların belirsiz süreli/ }));
  await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  expect(await screen.findByText(/Gerçek sonuç önizlemeden farklı/)).toBeInTheDocument();
});

test("belirsiz aktarım hatasında aynı dosya yeniden gönderilemez", async () => {
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL) => {
    if (String(url) === "/api/employees/import") {
      return new Response(JSON.stringify({ title: "Sunucu hatası" }), {
        status: 500,
        headers: { "Content-Type": "application/json" },
      });
    }
    return new Response(JSON.stringify({ ...rapor, gecerliSatir: 3, hataliSatir: 0, bilinmeyenDepartmanlar: [], sorunlar: [] }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  }));
  render();
  await dosyaSec();
  await waitFor(() => expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeEnabled());
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Şablondaki örnek personel satırını sildim/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Tüm aktarılacak çalışanların belirsiz süreli/ }));
  await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Aktarım sonucu doğrulanamadı");
  expect(screen.getByRole("button", { name: "Kontrol ettim, aktar" })).toBeDisabled();
  expect(importCalls()).toHaveLength(1);
});

test("eksik önizleme raporu aktarımı durdurur", async () => {
  stubApi({ "/api/employees/import/preview": { toplamSatir: 3, gecerliSatir: 1 } });
  render();
  await dosyaSec();
  expect(await screen.findByRole("alert")).toHaveTextContent("Önizleme raporu eksik");
  expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeDisabled();
  expect(importCalls()).toHaveLength(0);
});
