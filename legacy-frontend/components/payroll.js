const payrollCurrency = new Intl.NumberFormat("tr-TR", {
  style: "currency",
  currency: "TRY",
  maximumFractionDigits: 0,
});

const payrollNumber = new Intl.NumberFormat("tr-TR", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const payrollDefaultSettings = {
  overtimeMultiplier: 1.5,
  monthlyWorkingHours: 225,
  defaultWorkedDays: 30,
  sgkEmployeeRate: 14,
  unemploymentEmployeeRate: 1,
  sgkEmployerRate: 20.5,
  unemploymentEmployerRate: 2,
  stampTaxRate: 0.759,
  sgkBaseMin: 33030,
  sgkBaseMax: 297270,
  monthlyMinWageIncomeTaxExemption: 4211,
  monthlyMinWageStampTaxExemption: 250.7,
};

const formatPayrollMoney = (value) => payrollCurrency.format(Math.round(value || 0));
const formatPayrollNumber = (value) => payrollNumber.format(value || 0);
const parsePayrollNumber = (value, fallback = 0) => {
  const parsed = Number(String(value ?? "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : fallback;
};

const getPayrollDefaultSettings = () => {
  try {
    const saved = JSON.parse(localStorage.getItem("ekipzen-payroll-settings") || "{}");
    return { ...payrollDefaultSettings, ...saved };
  } catch {
    return { ...payrollDefaultSettings };
  }
};

const getPayrollParameters = () => {
  const settings = getPayrollDefaultSettings();

  return {
    ...settings,
    period: "Nisan 2026",
    periodStatus: "Kontrol",
    minWageGross: settings.sgkBaseMin,
    sgkEmployeeRateDecimal: settings.sgkEmployeeRate / 100,
    unemploymentEmployeeRateDecimal: settings.unemploymentEmployeeRate / 100,
    sgkEmployerRateDecimal: settings.sgkEmployerRate / 100,
    unemploymentEmployerRateDecimal: settings.unemploymentEmployerRate / 100,
    stampTaxRateDecimal: settings.stampTaxRate / 100,
    taxBrackets: [
      { limit: 190000, base: 0, baseTax: 0, rate: 0.15 },
      { limit: 400000, base: 190000, baseTax: 28500, rate: 0.2 },
      { limit: 1500000, base: 400000, baseTax: 70500, rate: 0.27 },
      { limit: 5300000, base: 1500000, baseTax: 367500, rate: 0.35 },
      { limit: Infinity, base: 5300000, baseTax: 1697500, rate: 0.4 },
    ],
  };
};

const payrollEmployees = [
  {
    id: "pr-001",
    name: "Ahmet Yılmaz",
    title: "Senior Developer",
    dept: "Yazılım",
    grossSalary: 118000,
    workedDays: 30,
    overtimeHours: 12,
    premiumPay: 3500,
    roadAllowance: 1200,
    mealAllowance: 1800,
    benefitPay: 3200,
    specialDeductions: 1800,
    previousTaxBase: 312000,
    ibanComplete: true,
    timesheetComplete: true,
    approvalStatus: "Kontrol",
    notes: "SGK tavanına yakın kazanç; matrah kontrolü önerilir.",
  },
  {
    id: "pr-002",
    name: "Selin Koç",
    title: "UI Designer",
    dept: "Tasarım",
    grossSalary: 76000,
    workedDays: 30,
    overtimeHours: 5,
    premiumPay: 2400,
    roadAllowance: 900,
    mealAllowance: 1300,
    benefitPay: 2800,
    specialDeductions: 950,
    previousTaxBase: 186000,
    ibanComplete: true,
    timesheetComplete: true,
    approvalStatus: "Onaya Hazır",
    notes: "Yan hak istisna notu kontrol edildi.",
  },
  {
    id: "pr-003",
    name: "Burak Demir",
    title: "Satış Temsilcisi",
    dept: "Satış",
    grossSalary: 54000,
    workedDays: 28,
    overtimeHours: 0,
    premiumPay: 18500,
    roadAllowance: 800,
    mealAllowance: 1300,
    benefitPay: 2100,
    specialDeductions: 1200,
    previousTaxBase: 148000,
    ibanComplete: false,
    timesheetComplete: true,
    approvalStatus: "Eksik Veri",
    notes: "IBAN eksik; prim ödemesi bu ay gelir vergisi dilimini etkiliyor.",
  },
  {
    id: "pr-004",
    name: "Ayşe Vural",
    title: "İK Uzmanı",
    dept: "İK",
    grossSalary: 62000,
    workedDays: 30,
    overtimeHours: 0,
    premiumPay: 0,
    roadAllowance: 900,
    mealAllowance: 1600,
    benefitPay: 2400,
    specialDeductions: 700,
    previousTaxBase: 167000,
    ibanComplete: true,
    timesheetComplete: true,
    approvalStatus: "Onaylandı",
    notes: "Standart bordro akışı.",
  },
  {
    id: "pr-005",
    name: "Mert Can",
    title: "DevOps Engineer",
    dept: "Operasyon",
    grossSalary: 98000,
    workedDays: 27,
    overtimeHours: 19,
    premiumPay: 2700,
    roadAllowance: 1200,
    mealAllowance: 1800,
    benefitPay: 3500,
    specialDeductions: 1500,
    previousTaxBase: 274000,
    ibanComplete: true,
    timesheetComplete: false,
    approvalStatus: "Eksik Veri",
    notes: "Puantaj eksik; nöbet ve fazla mesai kontrol edilmeli.",
  },
];

const calculateIncomeTaxByBracket = (taxBase, brackets) => {
  const bracket = brackets.find((item) => taxBase <= item.limit) || brackets[brackets.length - 1];
  return bracket.baseTax + Math.max(taxBase - bracket.base, 0) * bracket.rate;
};

const buildPayrollInput = (employee, parameters) => {
  const hourlyRate = employee.grossSalary / parameters.monthlyWorkingHours;
  const overtimePay =
    employee.overtimePay ??
    hourlyRate * (employee.overtimeHours || 0) * parameters.overtimeMultiplier;

  return {
    ...employee,
    hourlyRate,
    overtimePay,
    additionalPay:
      (employee.additionalPay || 0) +
      (employee.premiumPay || 0) +
      (employee.roadAllowance || 0) +
      (employee.mealAllowance || 0) +
      (employee.benefitPay || 0),
  };
};

const calculatePayrollPreview = (employee, periodParameters = getPayrollParameters()) => {
  const prepared = buildPayrollInput(employee, periodParameters);
  const dayRatio = prepared.workedDays / periodParameters.defaultWorkedDays;
  const baseGross = prepared.grossSalary * dayRatio;
  const grossEarnings = baseGross + prepared.overtimePay + prepared.additionalPay;
  const sgkBase = Math.min(
    Math.max(grossEarnings, periodParameters.sgkBaseMin * dayRatio),
    periodParameters.sgkBaseMax
  );
  const sgkEmployee = sgkBase * periodParameters.sgkEmployeeRateDecimal;
  const unemploymentEmployee = sgkBase * periodParameters.unemploymentEmployeeRateDecimal;
  const incomeTaxBase = Math.max(grossEarnings - sgkEmployee - unemploymentEmployee, 0);
  const cumulativeBefore = prepared.previousTaxBase;
  const cumulativeAfter = cumulativeBefore + incomeTaxBase;
  const rawIncomeTax =
    calculateIncomeTaxByBracket(cumulativeAfter, periodParameters.taxBrackets) -
    calculateIncomeTaxByBracket(cumulativeBefore, periodParameters.taxBrackets);
  const incomeTax = Math.max(rawIncomeTax - periodParameters.monthlyMinWageIncomeTaxExemption, 0);
  const stampTax = Math.max(
    grossEarnings * periodParameters.stampTaxRateDecimal -
      periodParameters.monthlyMinWageStampTaxExemption,
    0
  );
  const totalDeductions =
    sgkEmployee + unemploymentEmployee + incomeTax + stampTax + prepared.specialDeductions;
  const netPay = grossEarnings - totalDeductions;
  const employerSgk = sgkBase * periodParameters.sgkEmployerRateDecimal;
  const employerUnemployment = sgkBase * periodParameters.unemploymentEmployerRateDecimal;
  const employerCost = grossEarnings + employerSgk + employerUnemployment;

  const warnings = [];
  if (!prepared.timesheetComplete) warnings.push("Puantaj eksik");
  if (!prepared.ibanComplete) warnings.push("IBAN eksik");
  if (sgkBase >= periodParameters.sgkBaseMax) warnings.push("SGK tavan kontrolü");
  if (cumulativeBefore < 190000 && cumulativeAfter > 190000) warnings.push("Vergi dilimi geçişi");
  if (prepared.approvalStatus === "Kontrol") warnings.push("Kontrol bekliyor");

  return {
    ...prepared,
    baseGross,
    grossEarnings,
    sgkBase,
    sgkEmployee,
    unemploymentEmployee,
    incomeTaxBase,
    incomeTax,
    stampTax,
    totalDeductions,
    netPay,
    employerSgk,
    employerUnemployment,
    employerCost,
    warnings,
  };
};

const calculateSinglePayrollScenario = (scenario, parameters = getPayrollParameters()) =>
  calculatePayrollPreview(
    {
      id: "scenario",
      name: scenario.name || "Tekil Hesaplama",
      title: "Ön hesap senaryosu",
      dept: "Senaryo",
      grossSalary: scenario.grossSalary,
      workedDays: scenario.workedDays,
      overtimeHours: scenario.overtimeHours,
      overtimeMultiplier: scenario.overtimeMultiplier,
      premiumPay: scenario.premiumPay,
      roadAllowance: scenario.roadAllowance,
      mealAllowance: scenario.mealAllowance,
      benefitPay: scenario.benefitPay,
      specialDeductions: scenario.specialDeductions,
      previousTaxBase: scenario.previousTaxBase,
      ibanComplete: true,
      timesheetComplete: true,
      approvalStatus: "Ön Hesap",
      notes: "Bu hesap dönem bordrosuna işlenmedi.",
      overtimePay:
        (scenario.grossSalary / parameters.monthlyWorkingHours) *
        scenario.overtimeHours *
        scenario.overtimeMultiplier,
    },
    { ...parameters, overtimeMultiplier: scenario.overtimeMultiplier }
  );

const getPayrollData = () => {
  const parameters = getPayrollParameters();
  const rows = payrollEmployees.map((employee) => calculatePayrollPreview(employee, parameters));
  const totals = rows.reduce(
    (acc, row) => ({
      gross: acc.gross + row.grossEarnings,
      net: acc.net + row.netPay,
      employerCost: acc.employerCost + row.employerCost,
      deductions: acc.deductions + row.totalDeductions,
    }),
    { gross: 0, net: 0, employerCost: 0, deductions: 0 }
  );
  const controls = [
    {
      label: "Eksik Puantaj",
      value: rows.filter((row) => !row.timesheetComplete).length,
      detail: "Fazla mesai ve çalışılan gün kapanışı bekliyor.",
      level: "high",
      icon: "fa-clock",
    },
    {
      label: "Eksik IBAN",
      value: rows.filter((row) => !row.ibanComplete).length,
      detail: "Ödeme listesine girmeden tamamlanmalı.",
      level: "high",
      icon: "fa-building-columns",
    },
    {
      label: "SGK Matrah Uyarısı",
      value: rows.filter((row) => row.sgkBase >= parameters.sgkBaseMax || row.sgkBase <= parameters.sgkBaseMin * 0.95).length,
      detail: "Alt/üst sınır ve ücret dışı ödeme etkisi izleniyor.",
      level: "medium",
      icon: "fa-shield-halved",
    },
    {
      label: "Vergi Matrahı Uyarısı",
      value: rows.filter((row) => row.warnings.includes("Vergi dilimi geçişi")).length,
      detail: "Kümülatif gelir vergisi dilimi değişen kayıtlar.",
      level: "medium",
      icon: "fa-scale-balanced",
    },
    {
      label: "Onay Bekleyen",
      value: rows.filter((row) => row.approvalStatus !== "Onaylandı").length,
      detail: "Kontrol veya onay aşamasında bekleyen bordrolar.",
      level: "low",
      icon: "fa-file-signature",
    },
  ];

  return {
    parameters,
    rows,
    totals,
    controls,
    steps: [
      { id: "prep", label: "Hazırlık", status: "done", meta: "Puantaj ve personel verisi" },
      { id: "calc", label: "Hesaplama", status: "done", meta: "Ön hesap oluşturuldu" },
      { id: "check", label: "Kontrol", status: "active", meta: "5 uyarı inceleniyor" },
      { id: "approve", label: "Onay", status: "pending", meta: "3 kayıt bekliyor" },
      { id: "slip", label: "Pusula", status: "pending", meta: "Onay sonrası yayın" },
    ],
  };
};

const payrollStatusClass = (status) =>
  ({
    Onaylandı: "approved",
    "Onaya Hazır": "info",
    Kontrol: "pending",
    "Eksik Veri": "rejected",
    "Ön Hesap": "info",
  }[status] || "pending");

const renderPayrollParameters = (parameters) => `
  <div class="card payroll-parameters">
    <div class="card-header-clean">
      <div>
        <h4>2026 Bordro Parametreleri</h4>
        <p class="text-muted">Demo ön hesap için kullanılan Türkiye 4/a parametreleri.</p>
      </div>
      <span class="status-pill info">Ön hesap</span>
    </div>
    <div class="parameter-grid">
      <div><span>Fazla mesai çarpanı</span><strong>${formatPayrollNumber(parameters.overtimeMultiplier)}</strong></div>
      <div><span>Aylık çalışma saati</span><strong>${formatPayrollNumber(parameters.monthlyWorkingHours)}</strong></div>
      <div><span>SGK PEK alt sınır</span><strong>${formatPayrollMoney(parameters.sgkBaseMin)}</strong></div>
      <div><span>SGK PEK üst sınır</span><strong>${formatPayrollMoney(parameters.sgkBaseMax)}</strong></div>
      <div><span>SGK işçi / işsizlik</span><strong>%${formatPayrollNumber(parameters.sgkEmployeeRate)} + %${formatPayrollNumber(parameters.unemploymentEmployeeRate)}</strong></div>
      <div><span>Damga vergisi</span><strong>‰${formatPayrollNumber(parameters.stampTaxRate)}</strong></div>
    </div>
    <div class="payroll-note">
      <i aria-hidden="true" class="fa-solid fa-circle-info"></i>
      <span>Bu ekran bordro deneyimi ve kontrol akışı için demo hesap sunar; üretim kullanımında mevzuat ve müşavir doğrulaması gerekir.</span>
    </div>
  </div>
`;

const renderPayrollPeriodTab = (data) => {
  const { parameters, rows, totals, controls, steps } = data;

  return `
    <div class="payroll-kpi-grid">
      <div class="stat-box payroll-kpi">
        <span class="sb-label">Dönem Durumu</span>
        <strong class="sb-val">${parameters.periodStatus}</strong>
        <small>${parameters.period} bordrosu</small>
      </div>
      <div class="stat-box payroll-kpi">
        <span class="sb-label">Çalışan</span>
        <strong class="sb-val">${rows.length}</strong>
        <small>${rows.filter((row) => row.approvalStatus === "Onaylandı").length} onaylandı</small>
      </div>
      <div class="stat-box payroll-kpi">
        <span class="sb-label">Toplam Brüt</span>
        <strong class="sb-val">${formatPayrollMoney(totals.gross)}</strong>
        <small>Ek ödemeler dahil</small>
      </div>
      <div class="stat-box payroll-kpi">
        <span class="sb-label">Toplam Net</span>
        <strong class="sb-val">${formatPayrollMoney(totals.net)}</strong>
        <small>Ödeme önizlemesi</small>
      </div>
      <div class="stat-box payroll-kpi">
        <span class="sb-label">İşveren Maliyeti</span>
        <strong class="sb-val">${formatPayrollMoney(totals.employerCost)}</strong>
        <small>Prim işveren payı dahil</small>
      </div>
    </div>

    <section class="card payroll-flow-card">
      <div class="card-header-clean">
        <div>
          <h4>Bordro Akışı</h4>
          <p class="text-muted">Dönem kapanışı için adım adım operasyon takibi.</p>
        </div>
      </div>
      <div class="payroll-flow">
        ${steps
          .map(
            (step) => `
              <button class="payroll-step ${step.status}" onclick="switchPayrollStep('${step.id}', event)">
                <span>${step.label}</span>
                <strong>${step.status === "done" ? "Tamam" : step.status === "active" ? "Aktif" : "Bekliyor"}</strong>
                <small>${step.meta}</small>
              </button>
            `
          )
          .join("")}
      </div>
    </section>

    <div class="payroll-main-grid">
      <section class="card payroll-control-card">
        <div class="card-header-clean">
          <div>
            <h4>Kontrol Merkezi</h4>
            <p class="text-muted">Eksik veri, matrah ve onay uyarıları.</p>
          </div>
          <button class="btn btn-secondary btn-sm" onclick="showToast('Kontrol listesi işaretlendi; uyarılar denetim izine kaydedildi.', 'success')"><i aria-hidden="true" class="fa-solid fa-check-double"></i> Kontrol edildi</button>
        </div>
        <div class="payroll-control-grid">
          ${controls
            .map(
              (control) => `
                <div class="payroll-control ${control.level}">
                  <div class="payroll-control-icon"><i aria-hidden="true" class="fa-solid ${control.icon}"></i></div>
                  <div>
                    <span>${control.label}</span>
                    <strong>${control.value}</strong>
                    <p>${control.detail}</p>
                  </div>
                </div>
              `
            )
            .join("")}
        </div>
      </section>

      ${renderPayrollParameters(parameters)}
    </div>

    <section class="table-container payroll-table-card">
      <div class="payroll-table-header">
        <div>
          <h4>Çalışan Bordro Tablosu</h4>
          <p class="text-muted">Satıra tıklayarak bordro pusulası ve hesap detayını açın.</p>
        </div>
        <div class="toolbar-actions">
          <label class="sr-only" for="payroll-dept-filter">Departman filtresi</label>
          <select id="payroll-dept-filter" class="small-select" onchange="filterPayrollTable()">
            <option value="">Tüm departmanlar</option>
            ${[...new Set(rows.map((row) => row.dept))].map((dept) => `<option value="${dept}">${dept}</option>`).join("")}
          </select>
        </div>
      </div>
      <table class="data-table payroll-table">
        <thead>
          <tr>
            <th>Personel</th><th>Departman</th><th>Brüt</th><th>Fazla Mesai</th><th>Ek Ödeme</th><th>Kesinti</th><th>SGK Matrahı</th><th>GV Matrahı</th><th>Net</th><th>Durum</th><th></th>
          </tr>
        </thead>
        <tbody>
          ${rows
            .map(
              (row) => `
                <tr data-dept="${row.dept}">
                  <td><strong>${row.name}</strong><small>${row.title}</small></td>
                  <td>${row.dept}</td>
                  <td>${formatPayrollMoney(row.grossEarnings)}</td>
                  <td>${formatPayrollMoney(row.overtimePay)}<small>${row.overtimeHours || 0} saat</small></td>
                  <td>${formatPayrollMoney(row.additionalPay)}</td>
                  <td>${formatPayrollMoney(row.totalDeductions)}</td>
                  <td>${formatPayrollMoney(row.sgkBase)}</td>
                  <td>${formatPayrollMoney(row.incomeTaxBase)}</td>
                  <td><strong>${formatPayrollMoney(row.netPay)}</strong></td>
                  <td><span class="status-pill ${payrollStatusClass(row.approvalStatus)}">${row.approvalStatus}</span></td>
                  <td><button class="btn btn-secondary btn-sm" onclick="openPayrollDetail('${row.id}')">Detay</button></td>
                </tr>
              `
            )
            .join("")}
        </tbody>
      </table>
    </section>
  `;
};

const getDefaultScenario = (parameters = getPayrollParameters()) => {
  const employee = payrollEmployees[0];
  return {
    employeeId: employee.id,
    name: employee.name,
    grossSalary: employee.grossSalary,
    workedDays: parameters.defaultWorkedDays,
    overtimeHours: 3,
    overtimeMultiplier: parameters.overtimeMultiplier,
    premiumPay: 0,
    roadAllowance: employee.roadAllowance,
    mealAllowance: employee.mealAllowance,
    benefitPay: employee.benefitPay,
    specialDeductions: 0,
    previousTaxBase: employee.previousTaxBase,
  };
};

const readPayrollScenarioForm = () => {
  const employeeId = document.getElementById("scenario-employee")?.value;
  const employee = payrollEmployees.find((item) => item.id === employeeId);

  return {
    employeeId,
    name: employee?.name || "Tekil Hesaplama",
    grossSalary: parsePayrollNumber(document.getElementById("scenario-gross")?.value),
    workedDays: parsePayrollNumber(document.getElementById("scenario-days")?.value, 30),
    overtimeHours: parsePayrollNumber(document.getElementById("scenario-overtime-hours")?.value),
    overtimeMultiplier: parsePayrollNumber(document.getElementById("scenario-overtime-multiplier")?.value, 1.5),
    premiumPay: parsePayrollNumber(document.getElementById("scenario-premium")?.value),
    roadAllowance: parsePayrollNumber(document.getElementById("scenario-road")?.value),
    mealAllowance: parsePayrollNumber(document.getElementById("scenario-meal")?.value),
    benefitPay: parsePayrollNumber(document.getElementById("scenario-benefit")?.value),
    specialDeductions: parsePayrollNumber(document.getElementById("scenario-deductions")?.value),
    previousTaxBase: parsePayrollNumber(document.getElementById("scenario-tax-base")?.value),
  };
};

const renderScenarioResult = (scenario, parameters = getPayrollParameters()) => {
  const result = calculateSinglePayrollScenario(scenario, parameters);

  return `
    <div class="scenario-result-head">
      <div>
        <span class="status-pill info">Ön hesap</span>
        <h4>${result.name}</h4>
        <p>Bu hesap dönem bordrosuna işlenmedi.</p>
      </div>
      <strong>${formatPayrollMoney(result.netPay)}</strong>
    </div>
    <div class="scenario-highlight-grid">
      <div><span>Saatlik ücret</span><strong>${formatPayrollMoney(result.hourlyRate)}</strong></div>
      <div><span>Fazla mesai</span><strong>${formatPayrollMoney(result.overtimePay)}</strong><small>${scenario.overtimeHours} saat × ${formatPayrollNumber(scenario.overtimeMultiplier)}</small></div>
      <div><span>Toplam brüt</span><strong>${formatPayrollMoney(result.grossEarnings)}</strong></div>
      <div><span>İşveren maliyeti</span><strong>${formatPayrollMoney(result.employerCost)}</strong></div>
    </div>
    <div class="scenario-breakdown-grid">
      <section>
        <h5>Kazançlar</h5>
        ${renderPayrollLine("Brüt ücret", result.baseGross)}
        ${renderPayrollLine("Fazla mesai", result.overtimePay)}
        ${renderPayrollLine("Prim", scenario.premiumPay)}
        ${renderPayrollLine("Yol yardımı", scenario.roadAllowance)}
        ${renderPayrollLine("Yemek yardımı", scenario.mealAllowance)}
        ${renderPayrollLine("Yan hak / ek ödeme", scenario.benefitPay)}
      </section>
      <section>
        <h5>Kesintiler</h5>
        ${renderPayrollLine("SGK işçi payı", result.sgkEmployee, "deduction")}
        ${renderPayrollLine("İşsizlik işçi payı", result.unemploymentEmployee, "deduction")}
        ${renderPayrollLine("Gelir vergisi", result.incomeTax, "deduction")}
        ${renderPayrollLine("Damga vergisi", result.stampTax, "deduction")}
        ${renderPayrollLine("Özel kesinti", scenario.specialDeductions, "deduction")}
      </section>
      <section>
        <h5>Matrahlar</h5>
        ${renderPayrollLine("SGK matrahı", result.sgkBase)}
        ${renderPayrollLine("Gelir vergisi matrahı", result.incomeTaxBase)}
        ${renderPayrollLine("Önceki kümülatif", scenario.previousTaxBase)}
        ${renderPayrollLine("Dönem sonu kümülatif", scenario.previousTaxBase + result.incomeTaxBase)}
      </section>
    </div>
  `;
};

const renderPayrollCalculatorTab = (data) => {
  const scenario = getDefaultScenario(data.parameters);

  return `
    <div class="payroll-calculator-grid">
      <section class="card payroll-calculator-form">
        <div class="card-header-clean">
          <div>
            <h4>Tekil Hesaplama</h4>
            <p class="text-muted">Personel seçin, fazla mesai ve kazanç kalemlerini girerek ön hesap alın.</p>
          </div>
          <span class="status-pill info">Senaryo</span>
        </div>
        <div class="form-grid">
          <div class="input-group col-6">
            <label>Personel</label>
            <select id="scenario-employee" class="input-control" onchange="fillPayrollScenarioFromEmployee(this.value)">
              ${payrollEmployees.map((employee) => `<option value="${employee.id}" ${employee.id === scenario.employeeId ? "selected" : ""}>${employee.name} - ${employee.title}</option>`).join("")}
            </select>
          </div>
          <div class="input-group col-3">
            <label>Brüt ücret</label>
            <input id="scenario-gross" class="input-control" type="number" value="${scenario.grossSalary}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Çalışılan gün</label>
            <input id="scenario-days" class="input-control" type="number" value="${scenario.workedDays}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Fazla mesai saati</label>
            <input id="scenario-overtime-hours" class="input-control" type="number" value="${scenario.overtimeHours}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Fazla mesai çarpanı</label>
            <input id="scenario-overtime-multiplier" class="input-control" type="number" step="0.1" value="${scenario.overtimeMultiplier}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Prim</label>
            <input id="scenario-premium" class="input-control" type="number" value="${scenario.premiumPay}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Yol yardımı</label>
            <input id="scenario-road" class="input-control" type="number" value="${scenario.roadAllowance}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Yemek yardımı</label>
            <input id="scenario-meal" class="input-control" type="number" value="${scenario.mealAllowance}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Yan hak / ek ödeme</label>
            <input id="scenario-benefit" class="input-control" type="number" value="${scenario.benefitPay}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Özel kesinti</label>
            <input id="scenario-deductions" class="input-control" type="number" value="${scenario.specialDeductions}" oninput="runPayrollScenario()" />
          </div>
          <div class="input-group col-3">
            <label>Önceki GV matrahı</label>
            <input id="scenario-tax-base" class="input-control" type="number" value="${scenario.previousTaxBase}" oninput="runPayrollScenario()" />
          </div>
        </div>
        <div class="payroll-note">
          <i aria-hidden="true" class="fa-solid fa-circle-info"></i>
          <span>Fazla mesai tutarı, brüt ücret / ${formatPayrollNumber(data.parameters.monthlyWorkingHours)} saat üzerinden çarpanla hesaplanır.</span>
        </div>
      </section>
      <aside class="card payroll-scenario-result" id="payroll-scenario-result">
        ${renderScenarioResult(scenario, data.parameters)}
      </aside>
    </div>
  `;
};

const renderPayrollSettingsTab = (parameters) => {
  const fields = [
    ["overtimeMultiplier", "Fazla mesai çarpanı", "0.1"],
    ["monthlyWorkingHours", "Aylık çalışma saati", "1"],
    ["defaultWorkedDays", "Varsayılan çalışılan gün", "1"],
    ["sgkEmployeeRate", "SGK işçi oranı (%)", "0.01"],
    ["unemploymentEmployeeRate", "İşsizlik işçi oranı (%)", "0.01"],
    ["sgkEmployerRate", "SGK işveren oranı (%)", "0.01"],
    ["unemploymentEmployerRate", "İşsizlik işveren oranı (%)", "0.01"],
    ["stampTaxRate", "Damga vergisi oranı (%)", "0.001"],
    ["sgkBaseMin", "SGK PEK alt sınırı", "1"],
    ["sgkBaseMax", "SGK PEK üst sınırı", "1"],
    ["monthlyMinWageIncomeTaxExemption", "Asgari ücret GV istisnası", "0.01"],
    ["monthlyMinWageStampTaxExemption", "Asgari ücret damga istisnası", "0.01"],
  ];

  return `
    <section class="card payroll-settings-card">
      <div class="card-header-clean">
        <div>
          <h4>Bordro Ayarları</h4>
          <p class="text-muted">Şirket varsayılanlarını düzenleyin. Ayarlar bu tarayıcıda saklanır.</p>
        </div>
        <div class="toolbar-actions">
          <button class="btn btn-secondary" onclick="resetPayrollDefaultSettings()"><i aria-hidden="true" class="fa-solid fa-rotate-left"></i> Varsayılana dön</button>
          <button class="btn btn-primary" onclick="savePayrollDefaultSettings()"><i aria-hidden="true" class="fa-solid fa-floppy-disk"></i> Kaydet</button>
        </div>
      </div>
      <div class="payroll-settings-grid">
        ${fields
          .map(
            ([key, label, step]) => `
              <div class="input-group">
                <label>${label}</label>
                <input id="setting-${key}" class="input-control" type="number" step="${step}" value="${parameters[key]}" />
              </div>
            `
          )
          .join("")}
      </div>
      <div class="payroll-settings-feedback" aria-live="polite"></div>
      <div class="payroll-note">
        <i aria-hidden="true" class="fa-solid fa-circle-info"></i>
        <span>Bu değerler dönem bordrosu ve tekil hesaplama ön hesabında kullanılır. Üretim öncesi mevzuat ve müşavir doğrulaması gerekir.</span>
      </div>
    </section>
  `;
};

const Payroll = () => {
  const data = getPayrollData();
  const canUseCalculator = canAccessRoute("payroll-calculator");
  const canUseSettings = canAccessRoute("payroll-settings");

  return `
    <div id="payroll-screen">
      <div class="page-header">
        <div>
          <h2>Bordro</h2>
          <p>${data.parameters.period} dönemi için hesaplama, kontrol, onay ve tekil bordro senaryolarını yönetin.</p>
        </div>
        <div class="header-actions">
          ${
            canUseCalculator
              ? '<button class="btn btn-secondary" onclick="navigateTo(\'payroll-calculator\', event)"><i aria-hidden="true" class="fa-solid fa-calculator"></i> Tekil hesapla</button>'
              : ""
          }
          <button class="btn btn-primary" onclick="showToast('Dönem bordrosu onay akışına gönderildi.', 'success')"><i aria-hidden="true" class="fa-solid fa-paper-plane"></i> Onaya gönder</button>
        </div>
      </div>

      <div class="payroll-tabs">
        <button class="payroll-tab active" onclick="navigateTo('payroll', event)">
          <i aria-hidden="true" class="fa-solid fa-table-list"></i> Dönem Bordrosu
        </button>
        ${
          canUseCalculator
            ? '<button class="payroll-tab" onclick="navigateTo(\'payroll-calculator\', event)"><i aria-hidden="true" class="fa-solid fa-calculator"></i> Tekil Hesaplama</button>'
            : ""
        }
        ${
          canUseSettings
            ? '<button class="payroll-tab" onclick="navigateTo(\'payroll-settings\', event)"><i aria-hidden="true" class="fa-solid fa-sliders"></i> Bordro Ayarları</button>'
            : ""
        }
      </div>

      <section id="payroll-period" class="payroll-tab-content active">
        ${renderPayrollPeriodTab(data)}
      </section>
      ${canUseCalculator ? `<section id="payroll-calculator" class="payroll-tab-content">${renderPayrollCalculatorTab(data)}</section>` : ""}
      ${canUseSettings ? `<section id="payroll-settings" class="payroll-tab-content">${renderPayrollSettingsTab(data.parameters)}</section>` : ""}

      <div id="payroll-detail-overlay" class="payroll-detail-overlay"></div>
    </div>
  `;
};

const renderPayrollLine = (label, value, tone = "") => `
  <div class="payroll-line ${tone}">
    <span>${label}</span>
    <strong>${formatPayrollMoney(value)}</strong>
  </div>
`;

const renderPayrollDetail = (row, parameters) => `
  <div class="payroll-detail-panel">
    <div class="payroll-detail-head">
      <div>
        <span class="status-pill ${payrollStatusClass(row.approvalStatus)}">${row.approvalStatus}</span>
        <h3>${row.name}</h3>
        <p>${row.title} · ${row.dept} · ${parameters.period}</p>
      </div>
      <button class="btn-icon-sm" onclick="closePayrollDetail()" title="Kapat" aria-label="Bordro detayını kapat"><i aria-hidden="true" class="fa-solid fa-xmark"></i></button>
    </div>

    <div class="payroll-detail-summary">
      <div><span>Brüt Kazanç</span><strong>${formatPayrollMoney(row.grossEarnings)}</strong></div>
      <div><span>Toplam Kesinti</span><strong>${formatPayrollMoney(row.totalDeductions)}</strong></div>
      <div><span>Net Ücret</span><strong>${formatPayrollMoney(row.netPay)}</strong></div>
      <div><span>İşveren Maliyeti</span><strong>${formatPayrollMoney(row.employerCost)}</strong></div>
    </div>

    <div class="payroll-detail-grid">
      <section class="card payroll-detail-section">
        <div class="card-header-clean"><h4>Kazançlar</h4></div>
        ${renderPayrollLine("Brüt ücret", row.baseGross)}
        ${renderPayrollLine("Fazla mesai", row.overtimePay)}
        ${renderPayrollLine("Prim", row.premiumPay)}
        ${renderPayrollLine("Yol yardımı", row.roadAllowance)}
        ${renderPayrollLine("Yemek yardımı", row.mealAllowance)}
        ${renderPayrollLine("Yan hak / ek ödeme", row.benefitPay)}
        <div class="payroll-note compact"><i aria-hidden="true" class="fa-solid fa-circle-info"></i><span>${row.notes}</span></div>
      </section>

      <section class="card payroll-detail-section">
        <div class="card-header-clean"><h4>Kesintiler</h4></div>
        ${renderPayrollLine("SGK işçi payı", row.sgkEmployee, "deduction")}
        ${renderPayrollLine("İşsizlik işçi payı", row.unemploymentEmployee, "deduction")}
        ${renderPayrollLine("Gelir vergisi", row.incomeTax, "deduction")}
        ${renderPayrollLine("Damga vergisi", row.stampTax, "deduction")}
        ${renderPayrollLine("Özel kesintiler", row.specialDeductions, "deduction")}
      </section>

      <section class="card payroll-detail-section">
        <div class="card-header-clean"><h4>Vergi / SGK Matrahları</h4></div>
        ${renderPayrollLine("Saatlik ücret", row.hourlyRate)}
        ${renderPayrollLine("SGK matrahı", row.sgkBase)}
        ${renderPayrollLine("Gelir vergisi matrahı", row.incomeTaxBase)}
        ${renderPayrollLine("Önceki kümülatif matrah", row.previousTaxBase)}
        ${renderPayrollLine("Dönem sonu kümülatif", row.previousTaxBase + row.incomeTaxBase)}
        <div class="payroll-warning-stack">
          ${row.warnings.map((warning) => `<span class="status-pill pending">${warning}</span>`).join("") || '<span class="status-pill approved">Uyarı yok</span>'}
        </div>
      </section>

      <section class="card payroll-slip-preview">
        <div class="card-header-clean">
          <h4>Bordro Pusulası Önizleme</h4>
          <span class="status-pill info">Demo</span>
        </div>
        <div class="slip-paper">
          <div class="slip-brand"><strong>Ekipzen</strong><span>${parameters.period} Bordro Pusulası</span></div>
          <div class="slip-row"><span>Personel</span><strong>${row.name}</strong></div>
          <div class="slip-row"><span>Çalışılan gün</span><strong>${row.workedDays}</strong></div>
          <div class="slip-row"><span>Fazla mesai</span><strong>${row.overtimeHours || 0} saat · ${formatPayrollMoney(row.overtimePay)}</strong></div>
          <div class="slip-row"><span>Brüt kazanç</span><strong>${formatPayrollMoney(row.grossEarnings)}</strong></div>
          <div class="slip-row"><span>Yasal kesintiler</span><strong>${formatPayrollMoney(row.totalDeductions - row.specialDeductions)}</strong></div>
          <div class="slip-row"><span>Özel kesintiler</span><strong>${formatPayrollMoney(row.specialDeductions)}</strong></div>
          <div class="slip-row total"><span>Net ödenecek</span><strong>${formatPayrollMoney(row.netPay)}</strong></div>
        </div>
      </section>
    </div>

    <div class="payroll-detail-footer">
      <button class="btn btn-secondary" onclick="closePayrollDetail()">Kapat</button>
      <button class="btn btn-secondary" onclick="showToast('${row.name} bordrosu kontrol edildi olarak işaretlendi.', 'success')"><i aria-hidden="true" class="fa-solid fa-check-double"></i> Kontrol edildi</button>
      <button class="btn btn-primary" onclick="closePayrollDetail(); showToast('${row.name} bordrosu onaya gönderildi.', 'success')"><i aria-hidden="true" class="fa-solid fa-paper-plane"></i> Onaya gönder</button>
    </div>
  </div>
`;

window.switchPayrollTab = (tabId, evt) => {
  document.querySelectorAll(".payroll-tab").forEach((tab) => tab.classList.remove("active"));
  document.querySelectorAll(".payroll-tab-content").forEach((content) => content.classList.remove("active"));
  document.getElementById(tabId)?.classList.add("active");

  const activeButton =
    evt?.currentTarget ||
    Array.from(document.querySelectorAll(".payroll-tab")).find((tab) =>
      tab.getAttribute("onclick")?.includes(tabId === "payroll-period" ? "payroll'" : tabId)
    );
  activeButton?.classList.add("active");

  if (tabId === "payroll-calculator") runPayrollScenario();
};

window.fillPayrollScenarioFromEmployee = (employeeId) => {
  const parameters = getPayrollParameters();
  const employee = payrollEmployees.find((item) => item.id === employeeId);
  if (!employee) return;

  const values = {
    "scenario-gross": employee.grossSalary,
    "scenario-days": parameters.defaultWorkedDays,
    "scenario-overtime-hours": 3,
    "scenario-overtime-multiplier": parameters.overtimeMultiplier,
    "scenario-premium": employee.premiumPay || 0,
    "scenario-road": employee.roadAllowance || 0,
    "scenario-meal": employee.mealAllowance || 0,
    "scenario-benefit": employee.benefitPay || 0,
    "scenario-deductions": 0,
    "scenario-tax-base": employee.previousTaxBase,
  };

  Object.entries(values).forEach(([id, value]) => {
    const input = document.getElementById(id);
    if (input) input.value = value;
  });
  runPayrollScenario();
};

window.runPayrollScenario = () => {
  const resultPanel = document.getElementById("payroll-scenario-result");
  if (!resultPanel) return;

  resultPanel.innerHTML = renderScenarioResult(readPayrollScenarioForm(), getPayrollParameters());
};

window.savePayrollDefaultSettings = () => {
  const settings = Object.fromEntries(
    Object.keys(payrollDefaultSettings).map((key) => [
      key,
      parsePayrollNumber(document.getElementById(`setting-${key}`)?.value, payrollDefaultSettings[key]),
    ])
  );
  localStorage.setItem("ekipzen-payroll-settings", JSON.stringify(settings));

  const feedback = document.querySelector(".payroll-settings-feedback");
  if (feedback) feedback.textContent = "Ayarlar kaydedildi. Dönem bordrosu ve tekil hesaplama bu varsayılanları kullanacak.";
  showToast("Bordro ayarları kaydedildi.", "success");
};

window.resetPayrollDefaultSettings = () => {
  localStorage.removeItem("ekipzen-payroll-settings");
  showToast("Bordro ayarları varsayılana döndürüldü.", "info");
  window.navigateTo("payroll-settings");
};

window.filterPayrollTable = () => {
  const dept = document.getElementById("payroll-dept-filter")?.value || "";
  document.querySelectorAll(".payroll-table tbody tr").forEach((row) => {
    row.style.display = !dept || row.dataset.dept === dept ? "" : "none";
  });
};

window.openPayrollDetail = (employeeId) => {
  const data = getPayrollData();
  const row = data.rows.find((item) => item.id === employeeId);
  const overlay = document.getElementById("payroll-detail-overlay");
  if (!row || !overlay) return;

  overlay.innerHTML = renderPayrollDetail(row, data.parameters);
  overlay.classList.add("active");
};

window.closePayrollDetail = () => {
  const overlay = document.getElementById("payroll-detail-overlay");
  if (!overlay) return;

  overlay.classList.remove("active");
  overlay.innerHTML = "";
};

window.switchPayrollStep = (stepId, evt) => {
  document.querySelectorAll(".payroll-step").forEach((step) => step.classList.remove("selected"));
  evt?.currentTarget?.classList.add("selected");

  const controlCard = document.querySelector(".payroll-control-card");
  if (controlCard) controlCard.setAttribute("data-active-step", stepId);
};
