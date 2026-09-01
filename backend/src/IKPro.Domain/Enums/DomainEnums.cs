namespace IKPro.Domain.Enums;

/// <summary>Personel aktiflik durumu (frontend: active | passive).</summary>
public enum EmployeeStatus
{
    Active,
    Passive
}

/// <summary>Çalışma şekli (frontend: Tam Zamanlı | Yarı Zamanlı | Uzaktan).</summary>
public enum EmploymentType
{
    FullTime,
    PartTime,
    Remote
}

/// <summary>İzin talebi durumu (frontend: approved | pending + reject/cancel).</summary>
public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

/// <summary>Canlı yoklama durumu (frontend: ontime | late | absent | early).</summary>
public enum AttendanceStatus
{
    OnTime,
    Late,
    Absent,
    Early
}

/// <summary>Puantaj satır tipi (frontend: Tam | Mesai | Rapor).</summary>
public enum TimesheetType
{
    Full,
    Overtime,
    Leave
}

/// <summary>Puantaj satır durumu (frontend: ok | late | overtime | absent).</summary>
public enum TimesheetStatus
{
    Ok,
    Late,
    Overtime,
    Absent
}

/// <summary>Bordro onay durumu (frontend: Ön Hesap | Kontrol | Onaya Hazır | Eksik Veri | Onaylandı).</summary>
public enum PayrollApprovalStatus
{
    PreCalc,
    Control,
    ReadyForApproval,
    MissingData,
    Approved
}

/// <summary>Bordro dönemi durumu.</summary>
public enum PayrollPeriodStatus
{
    Draft,
    Control,
    Approved,
    Closed
}

/// <summary>Aday pipeline durumu (frontend: Yeni | Mülakat | Teklif | Red).</summary>
public enum CandidateStatus
{
    New,
    Interview,
    Offer,
    Rejected,
    Hired
}

/// <summary>Aksiyon önceliği (frontend: high | medium | low).</summary>
public enum ActionPriority
{
    High,
    Medium,
    Low
}

/// <summary>Aksiyon durumu (frontend: open | week | done).</summary>
public enum ActionStatus
{
    Open,
    Week,
    Done
}

/// <summary>Uyum belgesi durumu (frontend: Eksik | İncelemede | Süresi Yaklaşıyor | Tamamlandı).</summary>
public enum ComplianceStatus
{
    Missing,
    InReview,
    DueSoon,
    Completed
}

/// <summary>Risk seviyesi (frontend: high | medium | low).</summary>
public enum RiskLevel
{
    High,
    Medium,
    Low
}
/// <summary>İş sözleşmesinin süre niteliği; çalışma şekli (tam/yarı/uzaktan) değildir.</summary>
public enum EmploymentContractType
{
    Indefinite,
    FixedTerm
}

/// <summary>İş ilişkisinin sona erme iş sebebi; SGK kodu ayrıca referans olarak tutulur.</summary>
public enum TerminationReason
{
    EmployeeResignation,
    EmployerTermination,
    EmployerTerminationForCause,
    MutualAgreement,
    FixedTermExpiry,
    Retirement,
    MilitaryService,
    Marriage,
    Death,
    Other
}

/// <summary>Bildirim süresinin nasıl karşılandığı; tek başına hak/tutar kararı değildir.</summary>
public enum NoticeHandling
{
    NotApplicable,
    Worked,
    PayableToEmployee,
    PayableToEmployer,
    Waived
}
