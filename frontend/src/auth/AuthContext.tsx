import { createContext, useCallback, useContext, Fragment, useLayoutEffect, useRef, useSyncExternalStore, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { sessionIdentity } from "../api/writeRecovery";
import { apiFetch, toSession, type AuthResponse } from "../api/client";
import { clearSession, getSession, setSession, subscribeSession, type UserDto } from "../api/session";

type AuthValue = {
  user: UserDto | null;
  login: (email: string, password: string) => Promise<UserDto>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  // Kaynak localStorage oturumu: api/client.ts 401'de oturumu sildiğinde
  // (React dışından) bu abonelik sayesinde user anında null'a düşer.
  const user = useSyncExternalStore(subscribeSession, getSession)?.user ?? null;

  // Sorgu önbelleği kullanıcıya özeldir. Kimlik değişir değişmez (çıkış, yeniden
  // giriş, rol değiştirme) önbellek boşaltılmalı; aksi hâlde ortak bilgisayarda
  // bir sonraki kullanıcı, kendi verisi gelene kadar öncekinin personel/bordro
  // verisini görür (KVKK ihlali). Kimliğe bakıyoruz: token yenilemede aynı
  // kullanıcı devam ettiği için önbellek gereksiz yere boşaltılmaz.
  const queryClient = useQueryClient();
  const identity = sessionIdentity();
  const lastUserId = useRef(identity);
  useLayoutEffect(() => {
    const currentUserId = identity;
    if (lastUserId.current !== currentUserId) {
      lastUserId.current = currentUserId;
      queryClient.clear();
    }
  }, [identity, queryClient]);

  const accept = useCallback((auth: AuthResponse) => {
    const session = toSession(auth);
    setSession(session);
    return session.user;
  }, []);

  const login = useCallback(
    async (email: string, password: string) =>
      accept(await apiFetch<AuthResponse>("/auth/login", { method: "POST", body: JSON.stringify({ email, password }) })),
    [accept],
  );

  const logout = useCallback(async () => {
    const refreshToken = getSession()?.refreshToken;
    try {
      if (refreshToken) await apiFetch("/auth/logout", { method: "POST", body: JSON.stringify({ refreshToken }) });
    } catch {
      /* oturum zaten kapanıyor */
    }
    clearSession();
  }, []);

  return <AuthContext.Provider value={{ user, login, logout }}><Fragment key={identity}>{children}</Fragment></AuthContext.Provider>;
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error("useAuth, AuthProvider içinde kullanılmalı.");
  return value;
}
