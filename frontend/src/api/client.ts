import { acknowledgeWrite, pendingWrites, rememberUnknownWrite, replayIdentity, sessionIdentity } from "./writeRecovery";
import type { components } from "./schema";
import { clearSession, getSession, setSession, type Session } from "./session";

type ProblemDetails = { title?: string; status?: number; errors?: Record<string, string[]> };
export type AuthResponse = components["schemas"]["AuthResponse"];

export class ApiError extends Error {
  status: number;
  problem?: ProblemDetails;
  outcome?: "rejected" | "unknown" | "not-sent";
  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.status = status;
    this.problem = problem;
  }
}

export const toSession = (auth: AuthResponse): Session => {
  if (!auth.token || !auth.refreshToken || !auth.user) {
    throw new ApiError(500, "Kimlik yanıtı eksik alan içeriyor.");
  }
  return { token: auth.token, refreshToken: auth.refreshToken, user: auth.user };
};

const API_BASE = "/api";
let refreshInFlight: Promise<boolean> | null = null;

const rawFetch = async (path: string, init: RequestInit = {}): Promise<Response> => {
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body && !(init.body instanceof FormData))
    headers.set("Content-Type", "application/json");
  const token = getSession()?.token;
  if (token) headers.set("Authorization", `Bearer ${token}`);
  return fetch(`${API_BASE}${path}`, { ...init, headers });
};

const runRefresh = async (): Promise<boolean> => {
  try {
    const session = getSession();
    if (!session?.refreshToken) return false;
    const identity = sessionIdentity(session);
    const response = await fetch(`${API_BASE}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    });
    if (!response.ok) return false;
    const auth = (await response.json()) as AuthResponse;
    if (identity !== sessionIdentity()) return false;
    setSession(toSession(auth));
    return true;
  } catch {
    return false;
  }
};

// Tek-uçuş refresh: eşzamanlı 401'ler aynı refresh isteğini paylaşır.
// Sıfırlama .finally ile yapılır; böylece refresh token yokken yaşanan
// await'siz erken dönüşte bile temizlik atamadan SONRA çalışır. (Gövde içindeki
// finally, ??= ataması gerçekleşmeden önce koşuyor ve cache'i kalıcı "false"
// ile zehirliyordu: sonraki girişlerde refresh bir daha hiç denenmiyordu.)
const tryRefresh = (): Promise<boolean> =>
  (refreshInFlight ??= runRefresh().finally(() => {
    refreshInFlight = null;
  }));

const toError = async (response: Response): Promise<ApiError> => {
  let problem: ProblemDetails | undefined;
  try {
    problem = await response.json();
  } catch {
    /* gövdesiz hata */
  }
  return new ApiError(response.status, problem?.title || `İstek başarısız (${response.status})`, problem);
};

// 401 → tek-uçuş refresh → tek retry. Refresh düşerse VEYA retry de 401 dönerse
// (yeni token'la bile hâlâ yetkisiz) oturum temizlenir. Yönlendirmeyi burada
// yapmıyoruz: clearSession aboneleri uyarır, AuthProvider user'ı null'a düşürür
// ve RequireAuth login'e indirir. (Eskiden window.location.hash elle set
// ediliyordu; bellekteki user bayat kaldığı için PublicOnly kullanıcıyı geri
// fırlatıyor ve ekran sonsuz "Yükleniyor"da kilitleniyordu.)
// Not: string ve FormData gövdeleri fetch'te her çağrıda yeniden serileştirilir,
// bu yüzden aynı init retry'de güvenle kullanılabilir.
const handleUnauthorized = async (
  path: string,
  response: Response,
  reissue: () => Promise<Response>,
  identity: string,
): Promise<Response> => {
  if (response.status !== 401 || path.startsWith("/auth/")) return response;

  if (identity !== sessionIdentity()) throw new ApiError(401, "Oturum kapsamı değişti; işlem yeniden gönderilmedi.");
  const refreshed = await tryRefresh();
  if (identity !== sessionIdentity()) throw new ApiError(401, "Oturum kapsamı değişti; işlem yeniden gönderilmedi.");
  if (refreshed) response = await reissue();
  if (!refreshed || response.status === 401) {
    clearSession();
  }
  return response;
};

const activeWrites = new Set<string>();
const unknownMessage = "İşlem sonucu doğrulanamadı; kayıt oluşmuş olabilir. Tekrar göndermeden gerçek kaydı, işlem geçmişini ve varsa gönderim makbuzunu inceleyin.";
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? "GET").toUpperCase();
  const owner = sessionIdentity(); const sourcePath = path.split("?")[0];
  const readOnlyPost = method === "POST" && (["/employees/import/preview", "/attendance/preview", "/payroll/preview", "/leaves/preview"].includes(sourcePath)
    || /^\/employees\/\d+\/compensation-packages\/\d+\/date-correction-preview$/.test(sourcePath));
  const write = !["GET", "HEAD", "OPTIONS"].includes(method) && !path.startsWith("/auth/") && !readOnlyPost;
  const key = method + ":" + sourcePath; const activeKey = owner + ":" + key;
  const replayId = replayIdentity(sourcePath, init);
  if (write && owner) {
    const pending = pendingWrites(owner);
    const prior = pending.find(item => item.key === key);
    if (activeWrites.has(activeKey) || (prior && (!replayId || replayId !== prior.replayId)) || (!prior && pending.length >= 20)) {
      const error = new ApiError(409, activeWrites.has(activeKey) ? "İstek hâlâ sürüyor; ikinci yazım gönderilmedi."
        : "Önceki yazımın sonucu belirsiz. Gerçek kaydı kontrol edip uyarıyı kaldırmadan yeni gönderim yapılamaz.");
      error.outcome = "not-sent"; throw error;
    }
    activeWrites.add(activeKey);
  }
  try {
    let response = await rawFetch(path, init);
    response = await handleUnauthorized(path, response, () => rawFetch(path, init), owner);
    if (!response.ok) {
      const error = await toError(response);
      error.outcome = write && (response.status >= 500 || response.status === 408) ? "unknown" : "rejected";
      if (error.outcome === "unknown") error.message = `${error.message} ${unknownMessage}`;
      throw error;
    }
    const data = response.status === 204 ? null as T : await response.json() as T;
    if (write && replayId && pendingWrites(owner).find(item => item.key === key)?.replayId === replayId)
      acknowledgeWrite(key, owner);
    return data;
  } catch (cause) {
    const error = cause instanceof ApiError ? cause : new ApiError(0, write ? unknownMessage : "Kayıt okunamadı; bağlantıyı kontrol edip yeniden yükleyin.");
    error.outcome ??= write && (error.status === 0 || error.status >= 500 || error.status === 408) ? "unknown" : "rejected";
    if (write && error.outcome === "unknown") rememberUnknownWrite(owner, sourcePath, method, replayId);
    throw error;
  } finally { if (write && owner) activeWrites.delete(activeKey); }
}

const fileNameFrom = (disposition: string | null): string | null => {
  if (!disposition) return null;
  const utf8 = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (utf8) return decodeURIComponent(utf8[1]);
  const plain = disposition.match(/filename="?([^";]+)"?/i);
  return plain ? plain[1] : null;
};

/** İkili indirme (evrak/pusula): Bearer + 401-refresh apiFetch ile aynı. */
export async function apiDownload(path: string): Promise<{ blob: Blob; fileName: string | null }> {
  const owner = sessionIdentity();
  let response = await rawFetch(path);
  response = await handleUnauthorized(path, response, () => rawFetch(path), owner);

  if (!response.ok) throw await toError(response);
  return { blob: await response.blob(), fileName: fileNameFrom(response.headers.get("Content-Disposition")) };
}
