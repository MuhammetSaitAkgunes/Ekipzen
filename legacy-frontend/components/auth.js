const AuthScreen = (mode = "login") => `
  <main class="auth-shell">
    <section class="auth-visual">
      <div class="auth-brand">
        <div class="brand-mark"><i aria-hidden="true" class="fa-solid fa-users-gear"></i></div>
        <div>
          <strong>Ekipzen</strong>
          <span>HR MASTER Suite</span>
        </div>
      </div>
      <div class="auth-copy">
        <span class="status-pill info">Demo erişim</span>
        <h1>Risk, bordro ve İK operasyonlarını tek merkezden yönetin.</h1>
        <p>Bu giriş ekranı demo session oluşturur. Gerçek güvenlik ve yetki kontrolü .NET backend policy katmanında kurulacaktır.</p>
      </div>
      <div class="auth-insight-grid">
        <div><strong>7</strong><span>kritik aksiyon</span></div>
        <div><strong>5</strong><span>bordro kontrolü</span></div>
        <div><strong>82</strong><span>uyum skoru</span></div>
      </div>
    </section>

    <section class="auth-panel">
      <div class="auth-tabs">
        <button class="auth-tab ${mode === "login" ? "active" : ""}" onclick="navigateTo('login', event)">Giriş yap</button>
        <button class="auth-tab ${mode === "signup" ? "active" : ""}" onclick="navigateTo('signup', event)">Hesap oluştur</button>
      </div>

      <div id="auth-login" class="auth-form ${mode === "login" ? "active" : ""}">
        <h2>Hoş geldiniz</h2>
        <p>Demo hesaba giriş yaparak uygulamayı inceleyebilirsiniz.</p>
        <div class="input-group">
          <label for="login-email">E-posta</label>
          <input id="login-email" class="input-control" value="ik@hrmaster.local" />
        </div>
        <div class="input-group">
          <label for="login-password">Şifre</label>
          <input id="login-password" class="input-control" type="password" value="demo123" />
        </div>
        <button class="btn btn-primary auth-submit" onclick="submitLogin()">
          <i aria-hidden="true" class="fa-solid fa-arrow-right-to-bracket"></i> Giriş yap
        </button>
      </div>

      <div id="auth-signup" class="auth-form ${mode === "signup" ? "active" : ""}">
        <h2>Demo hesap oluştur</h2>
        <p>Bilgiler yalnızca demo session için kullanılacaktır.</p>
        <div class="input-group">
          <label for="signup-name">Ad soyad</label>
          <input id="signup-name" class="input-control" value="İK Yöneticisi" />
        </div>
        <div class="input-group">
          <label for="signup-email">İş e-postası</label>
          <input id="signup-email" class="input-control" value="ik@hrmaster.local" />
        </div>
        <button class="btn btn-primary auth-submit" onclick="submitSignup()">
          <i aria-hidden="true" class="fa-solid fa-user-plus"></i> Hesap oluştur
        </button>
      </div>
    </section>
  </main>
`;

window.switchAuthTab = (tab, evt) => {
  document.querySelectorAll(".auth-tab").forEach((button) => button.classList.remove("active"));
  document.querySelectorAll(".auth-form").forEach((form) => form.classList.remove("active"));
  evt?.currentTarget?.classList.add("active");
  document.getElementById(`auth-${tab}`)?.classList.add("active");
};

window.submitLogin = () => {
  login({
    email: document.getElementById("login-email")?.value,
    password: document.getElementById("login-password")?.value,
  });
  navigateTo(getDefaultProtectedRoute());
};

window.submitSignup = () => {
  signup({
    name: document.getElementById("signup-name")?.value,
    email: document.getElementById("signup-email")?.value,
  });
  navigateTo(getDefaultProtectedRoute());
};
