import type { components } from "./schema";

export type UserDto = components["schemas"]["UserDto"];
export type Session = { token: string; refreshToken: string; user: UserDto };

export const SESSION_KEY = "ekipzen-session";

// Oturum tek kaynaktan okunur ve değişimi abonelere duyurulur: 401 kurtarması
// (api/client.ts) oturumu sildiğinde React tarafı da anında "çıkış yapmış"
// duruma geçer. Aksi hâlde bellekteki bayat kullanıcı yüzünden guard'lar
// login'e inişi geri çevirir ve ekran sonsuz "Yükleniyor"da kalır.
const listeners = new Set<() => void>();

// useSyncExternalStore aynı veri için aynı referansı görmeli; ham metin
// değişmedikçe çözümlenmiş nesne yeniden kullanılır.
let cachedRaw: string | null = null;
let cachedSession: Session | null = null;
let parsed = false;

export const getSession = (): Session | null => {
  const raw = localStorage.getItem(SESSION_KEY);
  if (!parsed || raw !== cachedRaw) {
    cachedRaw = raw;
    parsed = true;
    try {
      cachedSession = JSON.parse(raw || "null");
    } catch {
      cachedSession = null;
    }
  }
  return cachedSession;
};

export const subscribeSession = (listener: () => void): (() => void) => {
  if (!listeners.size) window.addEventListener("storage", onStorage);
  listeners.add(listener);
  return () => { listeners.delete(listener); if (!listeners.size) window.removeEventListener("storage", onStorage); };
};

const notify = () => listeners.forEach((listener) => listener());
const onStorage = (event: StorageEvent) => {
  if (event.key === SESSION_KEY || event.key === null) notify();
};

export const setSession = (session: Session) => {
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  notify();
};

export const clearSession = () => {
  localStorage.removeItem(SESSION_KEY);
  notify();
};
