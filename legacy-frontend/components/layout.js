const navIcons = {
  dashboard: "fa-shield-heart",
  overview: "fa-chart-line",
  actions: "fa-list-check",
  personnel: "fa-user-tie",
  recruitment: "fa-briefcase",
  attendance: "fa-clock",
  leaves: "fa-plane-departure",
  payroll: "fa-money-check-dollar",
  manager: "fa-chart-pie",
  settings: "fa-gear",
};

const navGroups = [
  { label: "Ana Sayfa", keys: ["dashboard", "overview", "actions"] },
  { label: "Çekirdek İK", keys: ["personnel", "recruitment", "attendance", "leaves"] },
  { label: "Bordro", keys: ["payroll"] },
  { label: "Yönetim", keys: ["manager", "settings"] },
];

const renderNavItems = () => {
  const accessible = getNavigationRoutes().filter((route) => canAccessRoute(route));
  const groupedKeys = navGroups.flatMap((group) => group.keys);
  const groups = [
    ...navGroups,
    { label: "Diğer", keys: accessible.map((r) => r.key).filter((key) => !groupedKeys.includes(key)) },
  ];
  let rendered = 0;

  return groups
    .map((group) => {
      const routes = group.keys
        .map((key) => accessible.find((route) => route.key === key))
        .filter(Boolean);
      if (!routes.length) return "";

      const items = routes
        .map((route) => {
          const activeClass = rendered === 0 ? "active" : "";
          rendered += 1;
          return `
            <a href="#${route.path}" data-page="${route.key}" class="nav-item ${activeClass}" onclick="navigateTo('${route.key}', event)">
              <i aria-hidden="true" class="fa-solid ${navIcons[route.key]}"></i> <span>${route.title}</span>
            </a>
          `;
        })
        .join("");

      return `<li class="nav-group-label">${group.label}</li>${items}`;
    })
    .join("");
};

const Layout = (content) => {
  const user = getCurrentUser() || demoUsers["hr-admin"];
  const actionCount = getOpenActionCount();

  return `
    <div class="app-container">
      <aside class="sidebar">
        <div class="brand">
          <div class="brand-mark"><i aria-hidden="true" class="fa-solid fa-users-gear"></i></div>
          <div>
            <strong>Ekipzen</strong>
            <span>HR MASTER Suite</span>
          </div>
        </div>
        <ul class="nav-links">
          ${renderNavItems()}
        </ul>
        <div class="sidebar-insight">
          <span>Bugünkü öncelik</span>
          <strong>${actionCount} açık aksiyon</strong>
          <small>Risk, bordro ve uyum takibi</small>
        </div>
      </aside>

      <header class="header">
        <div class="header-tools">
          <button class="btn-icon shell-toggle" onclick="toggleSidebar()" title="Menüyü daralt/genişlet" aria-label="Menüyü daralt veya genişlet">
            <i aria-hidden="true" class="fa-solid fa-bars-staggered"></i>
          </button>
          <button class="btn-icon theme-toggle" onclick="toggleTheme()" title="Tema değiştir" aria-label="Açık/koyu tema değiştir">
            <i id="theme-icon" aria-hidden="true" class="fa-solid fa-moon"></i>
          </button>
        </div>
        <div class="header-heading">
          <span id="page-eyebrow">Ekipzen</span>
          <div class="header-title" id="page-title">Risk Merkezi</div>
        </div>
        <button class="header-action-button" onclick="navigateTo('actions', event)" title="Aksiyon Merkezi" aria-label="Aksiyon Merkezi: ${actionCount} açık aksiyon">
          <i aria-hidden="true" class="fa-solid fa-list-check"></i>
          <span>${actionCount}</span>
        </button>
        <div class="header-search">
          <i aria-hidden="true" class="fa-solid fa-magnifying-glass"></i>
          <label class="sr-only" for="global-search-input">Personel, aksiyon veya sayfa ara</label>
          <input
            id="global-search-input"
            type="text"
            placeholder="Ara: personel, aksiyon, sayfa… (Ctrl+K)"
            autocomplete="off"
            role="combobox"
            aria-expanded="false"
            aria-controls="global-search-results"
            oninput="handleGlobalSearchInput()"
            onfocus="handleGlobalSearchInput()"
            onkeydown="handleGlobalSearchKeydown(event)"
          />
          <div id="global-search-results" class="search-results" role="listbox" aria-label="Arama sonuçları" hidden></div>
        </div>
        <div class="user-profile">
          <div class="user-avatar" aria-hidden="true">${user.initials}</div>
          <div>
            <strong>${user.name}</strong>
            <span>${user.roleLabel}</span>
          </div>
          <select class="role-switcher" onchange="switchDemoRole(this.value)" title="Demo rol değiştir" aria-label="Demo rol değiştir">
            <option value="hr-admin" ${user.role === "hr-admin" ? "selected" : ""}>İK Admin</option>
            <option value="manager" ${user.role === "manager" ? "selected" : ""}>Yönetici</option>
            <option value="employee" ${user.role === "employee" ? "selected" : ""}>Çalışan</option>
          </select>
          <button class="btn-icon-sm" onclick="handleLogout()" title="Çıkış yap" aria-label="Çıkış yap">
            <i aria-hidden="true" class="fa-solid fa-arrow-right-from-bracket"></i>
          </button>
        </div>
      </header>

      <main class="main-content" id="main-content">
        ${content || "<h1>Hoş geldiniz</h1><p>Sol menüden işlem seçebilirsiniz.</p>"}
      </main>
    </div>
  `;
};

/* ============================================================
   Global arama: sayfalar + personel + aksiyonlar tek kutuda.
   Klavye: Ctrl+K veya / ile odak, ok tuşları + Enter ile seçim.
   ============================================================ */

let globalSearchItems = [];
let globalSearchActiveIndex = -1;

const getGlobalSearchIndex = () => {
  const pages = getNavigationRoutes()
    .filter((route) => canAccessRoute(route))
    .map((route) => ({
      type: "Sayfa",
      icon: navIcons[route.key] || "fa-compass",
      label: route.title,
      hint: "Sayfaya git",
      routeKey: route.key,
    }));

  const people =
    typeof getDirectory === "function" && canAccessRoute("personnel")
      ? getDirectory().map((person) => ({
          type: "Personel",
          icon: "fa-user",
          label: person.name,
          hint: `${person.title} · ${person.dept}`,
          routeKey: "personnel",
          personName: person.name,
        }))
      : [];

  const actions =
    typeof getMockActions === "function" && canAccessRoute("actions")
      ? getMockActions().map((action) => ({
          type: "Aksiyon",
          icon: "fa-list-check",
          label: action.title,
          hint: `${action.source} · ${action.due}`,
          routeKey: "actions",
        }))
      : [];

  return [...pages, ...people, ...actions];
};

window.handleGlobalSearchInput = () => {
  const input = document.getElementById("global-search-input");
  if (!input) return;

  const query = input.value.trim().toLocaleLowerCase("tr-TR");
  if (query.length < 2) {
    closeGlobalSearch();
    return;
  }

  globalSearchItems = getGlobalSearchIndex()
    .filter((item) => `${item.label} ${item.hint}`.toLocaleLowerCase("tr-TR").includes(query))
    .slice(0, 8);
  globalSearchActiveIndex = globalSearchItems.length ? 0 : -1;
  renderGlobalSearchResults();
};

const renderGlobalSearchResults = () => {
  const panel = document.getElementById("global-search-results");
  const input = document.getElementById("global-search-input");
  if (!panel || !input) return;

  panel.innerHTML = globalSearchItems.length
    ? globalSearchItems
        .map(
          (item, index) => `
            <button type="button" class="search-result ${index === globalSearchActiveIndex ? "active" : ""}"
              role="option" aria-selected="${index === globalSearchActiveIndex}"
              onclick="runGlobalSearchResult(${index})">
              <i aria-hidden="true" class="fa-solid ${item.icon}"></i>
              <span class="sr-label">${item.label}</span>
              <span class="sr-type">${item.type}</span>
              <span class="sr-hint">${item.hint}</span>
            </button>
          `
        )
        .join("")
    : '<div class="search-empty">Sonuç bulunamadı</div>';

  panel.hidden = false;
  input.setAttribute("aria-expanded", "true");
  panel.querySelector(".search-result.active")?.scrollIntoView({ block: "nearest" });
};

window.handleGlobalSearchKeydown = (event) => {
  const panel = document.getElementById("global-search-results");
  if (!panel || panel.hidden) return;

  if (event.key === "ArrowDown") {
    event.preventDefault();
    globalSearchActiveIndex = Math.min(globalSearchActiveIndex + 1, globalSearchItems.length - 1);
    renderGlobalSearchResults();
  } else if (event.key === "ArrowUp") {
    event.preventDefault();
    globalSearchActiveIndex = Math.max(globalSearchActiveIndex - 1, 0);
    renderGlobalSearchResults();
  } else if (event.key === "Enter") {
    event.preventDefault();
    runGlobalSearchResult(globalSearchActiveIndex);
  } else if (event.key === "Escape") {
    closeGlobalSearch(true);
  }
};

window.runGlobalSearchResult = (index) => {
  const item = globalSearchItems[index];
  if (!item) return;

  closeGlobalSearch(true);
  navigateTo(item.routeKey);

  // Personel sonucu: liste açıldıktan sonra arama filtresini kişiyle doldur.
  if (item.personName) {
    setTimeout(() => {
      const searchInput = document.getElementById("personnel-search");
      if (searchInput) {
        searchInput.value = item.personName;
        window.filterPersonnel?.();
      }
    }, 60);
  }
};

window.closeGlobalSearch = (clearInput) => {
  const panel = document.getElementById("global-search-results");
  const input = document.getElementById("global-search-input");
  if (panel) {
    panel.hidden = true;
    panel.innerHTML = "";
  }
  if (input) {
    input.setAttribute("aria-expanded", "false");
    if (clearInput) input.value = "";
  }
  globalSearchItems = [];
  globalSearchActiveIndex = -1;
};
