import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { apiFetch } from "./client";
import { clearSession, getSession, setSession } from "./session";
import { acknowledgeWrite, pendingWrites } from "./writeRecovery";

const user = { id: "t06-user", role: "hr-admin", tenantId: 1 };
const login = (changes = {}) => setSession({ token: "T", refreshToken: "R", user: { ...user, ...changes } });
const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
beforeEach(() => { localStorage.clear(); sessionStorage.clear(); login(); vi.stubGlobal("fetch", vi.fn()); });
afterEach(() => { clearSession(); sessionStorage.clear(); vi.unstubAllGlobals(); });

test("ağda kaybolan yazım belirsizdir; gövde saklanmaz ve kontrol öncesi ikinci gönderim çıkmaz", async () => {
  vi.mocked(fetch).mockRejectedValueOnce(new TypeError("offline"));
  await expect(apiFetch("/actions", { method: "POST", body: JSON.stringify({ title: "Özel bilgi" }) }))
    .rejects.toMatchObject({ status: 0, outcome: "unknown" });
  expect(pendingWrites()).toHaveLength(1);
  expect(JSON.stringify(pendingWrites())).not.toContain("Özel bilgi");
  expect(JSON.stringify(sessionStorage)).not.toContain("Özel bilgi");
  await expect(apiFetch("/actions", { method: "POST" })).rejects.toMatchObject({ outcome: "not-sent" });
  expect(fetch).toHaveBeenCalledTimes(1);
  vi.mocked(fetch).mockResolvedValueOnce(json(200, [{ id: 9 }]));
  await apiFetch("/actions"); expect(pendingWrites()).toHaveLength(1);
  acknowledgeWrite(pendingWrites()[0].key);
  vi.mocked(fetch).mockResolvedValueOnce(json(201, { id: 10 }));
  await apiFetch("/actions", { method: "POST" }); expect(pendingWrites()).toHaveLength(0);
});

test.each([500, 502, 408])("%s yazım yanıtı başarısız işlem diye varsayılmaz", async status => {
  vi.mocked(fetch).mockResolvedValueOnce(json(status, { title: "Yanıt alınamadı" }));
  await expect(apiFetch("/actions/7", { method: "PUT" })).rejects.toMatchObject({ status, outcome: "unknown" });
  expect(pendingWrites()).toHaveLength(1); expect(fetch).toHaveBeenCalledTimes(1);
});

test("409 reddi ve GET ağ hatası belirsiz yazım veya işlem kanıtı üretmez", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(json(409, { title: "Kayıt değişti" }));
  await expect(apiFetch("/actions/7", { method: "PUT" })).rejects.toMatchObject({ status: 409, outcome: "rejected" });
  vi.mocked(fetch).mockRejectedValueOnce(new TypeError("offline"));
  await expect(apiFetch("/actions")).rejects.toMatchObject({ status: 0, outcome: "rejected" });
  expect(pendingWrites()).toHaveLength(0);
});

test("başarılı durum kodu ama okunamayan yazım gövdesinde sonuç yine belirsizdir", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(new Response("eksik-json", { status: 200 }));
  await expect(apiFetch("/actions", { method: "POST" })).rejects.toMatchObject({ outcome: "unknown" });
  expect(pendingWrites()).toHaveLength(1);
});

test("süren aynı yazım ikinci kez çıkmaz; başarılı ilk yanıt uyarı üretmez", async () => {
  let finish!: (value: Response) => void;
  vi.mocked(fetch).mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
  const first = apiFetch("/actions", { method: "POST" });
  await expect(apiFetch("/actions", { method: "POST" })).rejects.toMatchObject({ outcome: "not-sent" });
  expect(fetch).toHaveBeenCalledTimes(1);
  finish(json(201, { id: 9 })); await first; expect(pendingWrites()).toHaveLength(0);
});

test.each(["employee", "import", "practice"])("%s mevcut makbuzunda aynı kimlikle tekrar sonucu okunur; yeni kimlik engellenir", async kind => {
  const path = kind === "employee" ? "/employees" : kind === "import" ? "/employees/import" : "/guidance/catalog/versions/9/practice/attempts";
  const body = (id: string) => {
    if (kind === "import") { const form = new FormData(); form.set("importRequestId", id); return form; }
    return JSON.stringify(kind === "employee" ? { creationRequestId: id } : { attemptId: id });
  };
  vi.mocked(fetch).mockRejectedValueOnce(new TypeError("offline"));
  await expect(apiFetch(path, { method: "POST", body: body("old-id") })).rejects.toMatchObject({ outcome: "unknown" });
  await expect(apiFetch(path, { method: "POST", body: body("new-id") })).rejects.toMatchObject({ outcome: "not-sent" });
  vi.mocked(fetch).mockResolvedValueOnce(json(200, { result: "same receipt" }));
  await apiFetch(path, { method: "POST", body: body("old-id") });
  expect(fetch).toHaveBeenCalledTimes(2); expect(pendingWrites()).toHaveLength(0);
});

test("belirsiz iş tenant, kullanıcı ve rol kapsamı dışına çıkmaz; token yenilemesi kapsamı bozmaz", async () => {
  vi.mocked(fetch).mockRejectedValueOnce(new TypeError("offline"));
  await apiFetch("/actions", { method: "POST" }).catch(() => {});
  login({ tenantId: 2 }); expect(pendingWrites()).toHaveLength(0);
  login({ role: "manager" }); expect(pendingWrites()).toHaveLength(0);
  login({ id: "other" }); expect(pendingWrites()).toHaveLength(0);
  login(); expect(pendingWrites()).toHaveLength(1);
  setSession({ token: "NEW", refreshToken: "R2", user }); expect(pendingWrites()).toHaveLength(1);
});

test("401 yenilemesi başka oturum kapsamı verirse yazım yeni hesapla tekrarlanmaz", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(json(401, { title: "Yetkisiz" }))
    .mockResolvedValueOnce(json(200, { token: "NEW", refreshToken: "R2", user: { ...user, tenantId: 2 } }));
  await expect(apiFetch("/actions", { method: "POST" })).rejects.toMatchObject({ status: 401, outcome: "rejected" });
  expect(fetch).toHaveBeenCalledTimes(2); expect(pendingWrites()).toHaveLength(0);
});

test("yazımın 401 yanıtı beklenirken değişen oturumla yenileme veya tekrar yapılmaz", async () => {
  let respond!: (response: Response) => void;
  vi.mocked(fetch).mockImplementationOnce(() => new Promise(resolve => { respond = resolve; }));
  const write = apiFetch("/actions", { method: "POST" });
  login({ tenantId: 2 });
  respond(json(401, { title: "Yetkisiz" }));
  await expect(write).rejects.toMatchObject({ status: 401, outcome: "rejected" });
  expect(fetch).toHaveBeenCalledTimes(1);
  expect(getSession()?.user.tenantId).toBe(2);
  expect(pendingWrites()).toHaveLength(0);
});

test("yenileme beklenirken değişen oturum eski yenileme yanıtıyla ezilmez veya silinmez", async () => {
  let respond!: (response: Response) => void;
  let refreshStarted!: () => void;
  const started = new Promise<void>(resolve => { refreshStarted = resolve; });
  vi.mocked(fetch).mockResolvedValueOnce(json(401, { title: "Yetkisiz" }))
    .mockImplementationOnce(() => new Promise(resolve => { respond = resolve; refreshStarted(); }));
  const write = apiFetch("/actions", { method: "POST" });
  await started;
  login({ tenantId: 2 });
  respond(json(200, { token: "OLD-REFRESH", refreshToken: "R2", user }));
  await expect(write).rejects.toMatchObject({ status: 401, outcome: "rejected" });
  expect(fetch).toHaveBeenCalledTimes(2);
  expect(getSession()?.user.tenantId).toBe(2);
  expect(getSession()?.token).toBe("T");
  expect(pendingWrites()).toHaveLength(0);
});

test("sekme depolaması yazılamazsa belirsiz işlem koruması bellekte sürer", async () => {
  const set = Storage.prototype.setItem;
  const spy = vi.spyOn(Storage.prototype, "setItem").mockImplementation(function (this: Storage, key, value) {
    if (key.startsWith("ekipzen-write-recovery:")) throw new Error("depolama dolu");
    set.call(this, key, value);
  });
  try {
    vi.mocked(fetch).mockRejectedValueOnce(new TypeError("offline"));
    await apiFetch("/actions", { method: "POST" }).catch(() => {});
    expect(pendingWrites()).toHaveLength(1);
    await expect(apiFetch("/actions", { method: "POST" })).rejects.toMatchObject({ outcome: "not-sent" });
    expect(fetch).toHaveBeenCalledTimes(1);
  } finally { spy.mockRestore(); if (pendingWrites()[0]) acknowledgeWrite(pendingWrites()[0].key); }
});

test.each(["/employees/import/preview", "/attendance/preview", "/payroll/preview", "/leaves/preview", "/employees/7/compensation-packages/3/date-correction-preview"])("%s POST önizlemesi belirsiz yazım sayılmaz", async path => {
  vi.mocked(fetch).mockRejectedValueOnce(new TypeError("offline"));
  await expect(apiFetch(path, { method: "POST" })).rejects.toMatchObject({ outcome: "rejected" });
  expect(pendingWrites()).toHaveLength(0);
});
