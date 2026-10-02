import { getSession, subscribeSession, type Session } from "./session";

export type PendingWrite = { key: string; path: string; method: string; at: string; replayId: string | null };
const prefix = "ekipzen-write-recovery:";
const listeners = new Set<() => void>();
const memory = new Map<string, string>();
const empty: PendingWrite[] = [];
let cachedOwner = "", cachedRaw: string | null = null, cached: PendingWrite[] = empty;

export const sessionIdentity = (session: Session | null = getSession()) => session?.user.id
  ? JSON.stringify([session.user.tenantId ?? null, session.user.id, session.user.role ?? null]) : "";
const readRaw = (owner: string) => {
  if (memory.has(owner)) return memory.get(owner)!;
  try { return sessionStorage.getItem(prefix + owner); }
  catch { return memory.get(owner) ?? null; }
};
export function pendingWrites(owner = sessionIdentity()): PendingWrite[] {
  if (!owner) return empty;
  const raw = readRaw(owner);
  if (owner !== cachedOwner || raw !== cachedRaw) {
    cachedOwner = owner; cachedRaw = raw;
    try {
      const data: unknown = JSON.parse(raw ?? "[]");
      cached = Array.isArray(data) ? data.filter((x): x is PendingWrite => !!x && typeof x.key === "string"
        && typeof x.path === "string" && typeof x.method === "string" && typeof x.at === "string"
        && (x.replayId === null || typeof x.replayId === "string")) : empty;
    } catch { cached = empty; }
  }
  return cached;
}
function save(owner: string, items: PendingWrite[]) {
  const raw = JSON.stringify(items);
  try { sessionStorage.setItem(prefix + owner, raw); memory.delete(owner); }
  catch { memory.set(owner, raw); /* Sekme belleğindeki koruma devam eder. */ }
  cachedOwner = owner; cachedRaw = raw; cached = items;
  listeners.forEach(listener => listener());
}
export function subscribeWriteRecovery(listener: () => void) {
  listeners.add(listener); const unsubscribe = subscribeSession(listener);
  return () => { listeners.delete(listener); unsubscribe(); };
}
export function rememberUnknownWrite(owner: string, path: string, method: string, replayId: string | null) {
  if (!owner) return;
  const key = method + ":" + path;
  save(owner, [...pendingWrites(owner).filter(x => x.key !== key), { key, path, method, replayId, at: new Date().toISOString() }]);
}
export function acknowledgeWrite(key: string, owner = sessionIdentity()) {
  if (owner) save(owner, pendingWrites(owner).filter(item => item.key !== key));
}
// Yalnız sunucuda mevcut makbuz sözleşmesi bulunan üç uç; diğer POST'lar tekrar güvenli sayılmaz.
export function replayIdentity(path: string, init: RequestInit): string | null {
  if ((init.method ?? "GET").toUpperCase() !== "POST") return null;
  if (path === "/employees/import" && init.body instanceof FormData)
    return String(init.body.get("importRequestId") ?? "") || null;
  if (typeof init.body !== "string") return null;
  try {
    const body = JSON.parse(init.body);
    const value = path === "/employees" ? body.creationRequestId
      : /^\/guidance\/catalog\/versions\/\d+\/practice\/attempts$/.test(path) ? body.attemptId : null;
    return typeof value === "string" && value.length > 0 ? value : null;
  } catch { return null; }
}

export function recoverySource(path: string): { title: string; target: string | null } {
  const employee = path.match(/^\/employees\/(\d+)/);
  if (employee) return { title: `Personel #${employee[1]}`, target: `/personnel/${employee[1]}` };
  if (path.startsWith("/employees")) return { title: "Personel kaydı veya aktarımı", target: "/personnel" };
  const period = path.match(/^\/payroll\/periods\/(\d+)/);
  if (period) return { title: `Bordro dönemi #${period[1]}`, target: `/payroll?periodId=${period[1]}` };
  if (path.startsWith("/payroll")) return { title: "Bordro hazırlığı", target: "/payroll" };
  const action = path.match(/^\/actions\/(\d+)/);
  if (path.startsWith("/actions")) return { title: action ? `Takip aksiyonu #${action[1]}` : "Yeni takip aksiyonu", target: action ? `/actions?actionId=${action[1]}` : "/actions" };
  if (path.startsWith("/guidance")) return { title: "Öğrenme kaydı", target: "/guidance/catalog" };
  if (path.startsWith("/candidates") || path.startsWith("/positions")) return { title: "İşe alım kaydı", target: "/recruitment" };
  if (path.startsWith("/leaves")) return { title: "İzin kaydı", target: "/leaves" };
  return { title: "İlgili kaynak kaydı", target: null };
}
