import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../api/client";
import type { components } from "../api/schema";
import { useAuth } from "../auth/AuthContext";
import { appRoutes, navIcons, type Role } from "../routes";

type SearchResultDto = components["schemas"]["SearchResultDto"];

type Item = { label: string; hint: string; icon: string; path: string };

const TYPE_ICONS: Record<string, string> = {
  personnel: "fa-user", action: "fa-list-check", candidate: "fa-briefcase",
};

export function GlobalSearch() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const [mobileOpen, setMobileOpen] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const wrapRef = useRef<HTMLDivElement>(null);

  const [debouncedQuery, setDebouncedQuery] = useState("");
  useEffect(() => { if (mobileOpen) inputRef.current?.focus(); }, [mobileOpen]);
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedQuery(query.trim()), 300);
    return () => clearTimeout(timer);
  }, [query]);

  // API araması düşerse sayfa sonuçları çalışmaya devam eder (retry yok, hata yutulur).
  const apiResults = useQuery({
    queryKey: ["search", debouncedQuery],
    queryFn: () => apiFetch<SearchResultDto[]>(`/search?q=${encodeURIComponent(debouncedQuery)}`),
    enabled: debouncedQuery.length >= 2,
    retry: false,
  });

  const pageItems: Item[] = !query.trim() || query.trim().length < 2
    ? []
    : appRoutes
        .filter((r) => r.navKey === r.key && r.roles.includes((user?.role ?? "employee") as Role))
        .filter((r) => r.title.toLocaleLowerCase("tr-TR").includes(query.trim().toLocaleLowerCase("tr-TR")))
        .map((r) => ({ label: r.title, hint: "Sayfaya git", icon: navIcons[r.key] || "fa-compass", path: r.path }));

  const apiItems: Item[] = (apiResults.data ?? []).flatMap((result) => {
    const route = appRoutes.find((r) => r.key === result.routeKey && r.roles.includes((user?.role ?? "employee") as Role));
    if (!route) return [];
    const path = result.routeKey === "personnel" && result.type === "personnel" && Number.isInteger(result.entityId) && Number(result.entityId) > 0
      ? `/app/personnel/${result.entityId}`
      : result.routeKey === "actions" && result.type === "action" && Number.isInteger(result.entityId) && Number(result.entityId) > 0
        ? `/app/inbox?tab=actions&actionId=${result.entityId}`
        : result.routeKey === "actions" ? "/app/inbox?tab=actions" : route.path;
    return [{ label: result.label ?? "", hint: result.hint ?? "", icon: TYPE_ICONS[result.type ?? ""] ?? "fa-compass", path }];
  });

  const items: Item[] = [...pageItems, ...apiItems];

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const isTyping = ["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName ?? "");
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setMobileOpen(true);
        inputRef.current?.focus();
      } else if (event.key === "/" && !isTyping) {
        event.preventDefault();
        setMobileOpen(true);
        inputRef.current?.focus();
      } else if (event.key === "Escape") {
        setOpen(false);
        setMobileOpen(false);
        if (window.matchMedia("(max-width: 600px)").matches) triggerRef.current?.focus();
      }
    };
    const onClick = (event: MouseEvent) => {
      if (!wrapRef.current?.contains(event.target as Node)) { setOpen(false); setMobileOpen(false); }
    };
    document.addEventListener("keydown", onKey);
    document.addEventListener("click", onClick);
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("click", onClick);
    };
  }, []);

  const select = (item: Item) => {
    navigate(item.path);
    setQuery("");
    setOpen(false);
    setMobileOpen(false);
  };

  return (
    <div className={`header-search ${mobileOpen ? "is-search-open" : ""}`} ref={wrapRef}>
      <button ref={triggerRef} type="button" className="btn-icon ez-mobile-search-trigger" aria-label={mobileOpen ? "Genel aramayı kapat" : "Genel aramayı aç"} aria-expanded={mobileOpen} aria-controls="global-search-input" onClick={() => {
        setMobileOpen(!mobileOpen);
        if (mobileOpen) setOpen(false);
      }}><i className={`fa-solid ${mobileOpen ? "fa-xmark" : "fa-magnifying-glass"}`} aria-hidden="true" /></button>
      <i aria-hidden="true" className="fa-solid fa-magnifying-glass" />
      <label className="sr-only" htmlFor="global-search-input">Personel, aksiyon veya sayfa ara</label>
      <input
        id="global-search-input"
        ref={inputRef}
        type="text"
        placeholder="Ara: personel, aksiyon, sayfa… (Ctrl+K)"
        autoComplete="off"
        role="combobox"
        aria-autocomplete="list"
        aria-expanded={open && items.length > 0}
        aria-activedescendant={open && items[activeIndex] ? `global-search-option-${activeIndex}` : undefined}
        aria-controls="global-search-results"
        value={query}
        onChange={(e) => { setQuery(e.target.value); setOpen(true); setActiveIndex(0); }}
        onFocus={() => setOpen(true)}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown" && items.length) { e.preventDefault(); setActiveIndex((i) => Math.min(i + 1, items.length - 1)); }
          if (e.key === "ArrowUp") { e.preventDefault(); setActiveIndex((i) => Math.max(i - 1, 0)); }
          if (e.key === "Enter" && items[activeIndex]) select(items[activeIndex]);
        }}
      />
      <div id="global-search-results" className="search-results" role="listbox" aria-label="Arama sonuçları" hidden={!open || items.length === 0}>
        {items.map((item, index) => (
          <button
            key={`${item.path}-${item.label}`}
            id={`global-search-option-${index}`}
            type="button"
            className={`search-result ${index === activeIndex ? "active" : ""}`}
            role="option"
            aria-selected={index === activeIndex}
            onClick={() => select(item)}
          >
            <i aria-hidden="true" className={`fa-solid ${item.icon}`} />
            <span>{item.label}</span>
            <small>{item.hint}</small>
          </button>
        ))}
      </div>
    </div>
  );
}
