document.addEventListener("DOMContentLoaded", () => {
  bootstrapApp();
});

window.addEventListener("hashchange", () => {
  renderCurrentRoute();
});

window.bootstrapApp = (initialRoute) => {
  if (initialRoute) {
    const route = findRoute(initialRoute);
    if (route && setHashPath(route.path)) return;
  }

  renderCurrentRoute();
};

const renderCurrentRoute = () => {
  const fallbackPath = isAuthenticated() ? getDefaultProtectedRoute() : DefaultPublicRoute;
  if (!getHashPath()) {
    navigateTo(fallbackPath);
    return;
  }
  const route = getRouteByPath(getHashPath() || fallbackPath) || getRouteByPath(fallbackPath);
  renderRoute(route);
};

const ensureAppShell = () => {
  const root = document.getElementById("root");
  if (!document.querySelector(".app-container")) {
    root.innerHTML = Layout();
    initializeShellPreferences();
  }
};

const renderAuthRoute = (route) => {
  document.getElementById("root").innerHTML = AuthScreen(route.authMode || "login");
};

const renderRestrictedRoute = (route) => {
  ensureAppShell();
  setPageChrome({ title: "Yetki Gerekli", eyebrow: "Ekipzen", navKey: route.navKey });
  document.getElementById("main-content").innerHTML = `
    <section class="surface empty-state">
      <i aria-hidden="true" class="fa-solid fa-lock"></i>
      <h2>Bu alan için yetki gerekli</h2>
      <p>Bu rol ile seçilen modüle erişim kapalı. Gerçek yetki kontrolü backend policy katmanında yapılacaktır.</p>
    </section>
  `;
};

const renderRoute = (route) => {
  if (!route) {
    navigateTo(isAuthenticated() ? getDefaultProtectedRoute() : DefaultPublicRoute);
    return;
  }

  if (route.public) {
    if (isAuthenticated()) {
      navigateTo(getDefaultProtectedRoute());
      return;
    }
    renderAuthRoute(route);
    return;
  }

  if (!isAuthenticated()) {
    navigateTo(DefaultPublicRoute);
    return;
  }

  if (!canAccessRoute(route)) {
    renderRestrictedRoute(route);
    return;
  }

  ensureAppShell();
  setPageChrome(route);

  const mainContent = document.getElementById("main-content");
  mainContent.innerHTML = route.render ? route.render() : emptyRouteState();
  route.afterRender?.();
};

const setPageChrome = (route) => {
  const pageTitle = document.getElementById("page-title");
  const pageEyebrow = document.getElementById("page-eyebrow");
  if (pageTitle) pageTitle.innerText = route.title || "Yakında";
  if (pageEyebrow) pageEyebrow.innerText = route.eyebrow || "Ekipzen";

  document.querySelectorAll(".nav-item").forEach((el) => el.classList.remove("active"));
  const activeItem = document.querySelector(`.nav-item[data-page="${route.navKey || route.key}"]`);
  if (activeItem) activeItem.classList.add("active");
};

const emptyRouteState = () => `
  <section class="surface empty-state">
    <i aria-hidden="true" class="fa-regular fa-compass"></i>
    <h2>Bu alan hazırlanıyor</h2>
    <p>Seçtiğiniz modül henüz demo kapsamına eklenmedi.</p>
  </section>
`;

window.navigateTo = (routeOrPath, evt) => {
  evt?.preventDefault?.();

  const route = findRoute(routeOrPath) || getRouteByPath(isAuthenticated() ? getDefaultProtectedRoute() : DefaultPublicRoute);
  if (setHashPath(route.path)) return;
  renderRoute(route);
};

const initializeShellPreferences = () => {
  const savedTheme = localStorage.getItem("ekipzen-theme") || "light";
  const sidebarCollapsed = localStorage.getItem("ekipzen-sidebar") === "collapsed";

  applyTheme(savedTheme);
  applySidebarState(sidebarCollapsed);
};

const applySidebarState = (collapsed) => {
  const app = document.querySelector(".app-container");
  if (!app) return;

  app.classList.toggle("sidebar-collapsed", collapsed);
  localStorage.setItem("ekipzen-sidebar", collapsed ? "collapsed" : "expanded");
};

window.toggleSidebar = () => {
  const app = document.querySelector(".app-container");
  applySidebarState(!app?.classList.contains("sidebar-collapsed"));
};

const applyTheme = (theme) => {
  const nextTheme = theme === "dark" ? "dark" : "light";
  document.documentElement.setAttribute("data-theme", nextTheme);
  localStorage.setItem("ekipzen-theme", nextTheme);

  const icon = document.getElementById("theme-icon");
  if (icon) {
    icon.className = nextTheme === "dark" ? "fa-solid fa-sun" : "fa-solid fa-moon";
  }
};

window.toggleTheme = () => {
  const currentTheme = document.documentElement.getAttribute("data-theme") || "light";
  applyTheme(currentTheme === "dark" ? "light" : "dark");
};

window.handleLogout = () => {
  logout();
  navigateTo("login");
};

/* ---------- Geri bildirim (toast) ---------- */

const ensureToastRegion = () => {
  let region = document.getElementById("toast-region");
  if (!region) {
    region = document.createElement("div");
    region.id = "toast-region";
    region.className = "toast-region";
    region.setAttribute("aria-live", "polite");
    document.body.appendChild(region);
  }
  return region;
};

window.showToast = (message, type = "success") => {
  const region = ensureToastRegion();
  const icons = {
    success: "fa-circle-check",
    error: "fa-circle-xmark",
    warning: "fa-triangle-exclamation",
    info: "fa-circle-info",
  };

  const toast = document.createElement("div");
  toast.className = `toast toast-${type}`;
  toast.innerHTML = `<i aria-hidden="true" class="fa-solid ${icons[type] || icons.info}"></i><span>${message}</span>`;
  region.appendChild(toast);

  requestAnimationFrame(() => toast.classList.add("visible"));
  setTimeout(() => {
    toast.classList.remove("visible");
    setTimeout(() => toast.remove(), 250);
  }, 3200);
};

/* ---------- CSV dışa aktarma ---------- */

window.exportTableToCSV = (tableSelector, fileName = "rapor", rowFilter) => {
  const table = document.querySelector(tableSelector);
  if (!table) {
    showToast("Dışa aktarılacak tablo bulunamadı.", "error");
    return;
  }

  const lines = [];
  table.querySelectorAll("tr").forEach((row) => {
    if (row.style.display === "none") return;
    if (rowFilter && !rowFilter(row)) return;

    const cells = Array.from(row.querySelectorAll("th, td"))
      .filter((cell) => !cell.classList.contains("csv-skip"))
      .map((cell) => `"${cell.innerText.replace(/\s+/g, " ").trim().replace(/"/g, '""')}"`);
    if (cells.length) lines.push(cells.join(";"));
  });

  // BOM: Excel'in Türkçe karakterleri UTF-8 olarak açması için.
  const blob = new Blob(["﻿" + lines.join("\n")], { type: "text/csv;charset=utf-8;" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = `${fileName}.csv`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(link.href);
  showToast("CSV raporu indirildi.", "success");
};

/* ---------- Global kısayollar ---------- */

document.addEventListener("keydown", (event) => {
  const activeTag = document.activeElement?.tagName;
  const isTyping = ["INPUT", "TEXTAREA", "SELECT"].includes(activeTag);

  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
    event.preventDefault();
    document.getElementById("global-search-input")?.focus();
  } else if (event.key === "/" && !isTyping) {
    event.preventDefault();
    document.getElementById("global-search-input")?.focus();
  } else if (event.key === "Escape") {
    window.closeGlobalSearch?.();
  }
});

document.addEventListener("click", (event) => {
  if (!event.target.closest(".header-search")) {
    window.closeGlobalSearch?.();
  }
});
