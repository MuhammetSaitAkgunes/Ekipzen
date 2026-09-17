/**
 * Hash router döneminden kalan URL'ler (e-postalardaki /#/accept-invite davetleri,
 * yer imleri) ve köke asılı eski uygulama yolları yeni temiz yollara çevrilir.
 */
export const LEGACY_APP_ROOT_PATHS: readonly string[] = [
  "/dashboard", "/overview", "/actions", "/personnel", "/recruitment",
  "/attendance", "/leaves", "/payroll", "/manager", "/settings", "/risk",
];

export function mapLegacyPath(path: string): string {
  if (path === "/login") return "/giris";
  if (path === "/accept-invite") return "/davet";
  if (LEGACY_APP_ROOT_PATHS.some((root) => path === root || path.startsWith(`${root}/`))) return `/app${path}`;
  return path;
}

export function legacyHashTarget(hash: string): string | null {
  if (!hash.startsWith("#/")) return null;
  const raw = hash.slice(1);
  const queryStart = raw.indexOf("?");
  const path = queryStart === -1 ? raw : raw.slice(0, queryStart);
  const query = queryStart === -1 ? "" : raw.slice(queryStart);
  return `${mapLegacyPath(path)}${query}`;
}
