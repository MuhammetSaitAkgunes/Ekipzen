/** Uygulama genelindeki yol sabitleri. Bileşen importu yoktur; her yerden güvenle import edilir. */
export const APP_BASE = "/app";
export const LOGIN_PATH = "/giris";
export const INVITE_PATH = "/davet";
export const appPath = (path: string): string => `${APP_BASE}${path}`;
export const roleHomeFor = (_role: string | null | undefined): string =>
  appPath("/home");
