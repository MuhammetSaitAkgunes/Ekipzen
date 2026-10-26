import { vi } from "vitest";

/** Anahtar tam path'tir ("/api/dashboard/metrics"); query string yok sayılır. */
export function stubApi(routes: Record<string, unknown>): void {
  // Common read-only workspace fixture: no central content has been published.
  const responses: Record<string, unknown> = { "/api/guidance/catalog": [], "/api/guidance/help-preferences": [], ...routes };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input).split("?")[0];
      if (!(path in responses)) {
        return new Response(JSON.stringify({ title: `Stub tanımlı değil: ${path}` }), { status: 404 });
      }
      return new Response(JSON.stringify(responses[path]), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }),
  );
}
