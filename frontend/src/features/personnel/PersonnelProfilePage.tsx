import { useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { PersonnelModal } from "./PersonnelModal";
import { OffboardingModal } from "./OffboardingModal";
import { PersonnelEntryFollowUp } from "./PersonnelEntryFollowUp";
import { useEmployeeCreationEvidence } from "./guidanceQueries";

export function PersonnelProfilePage() {
  const { employeeId } = useParams();
  const { user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const isNew = location.pathname.endsWith("/new");
  const id = isNew ? null : Number(employeeId);
  const editing = user?.role === "hr-admin" && (isNew || params.get("edit") === "1");
  const showOffboarding = !isNew && user?.role === "hr-admin" && params.get("offboarding") === "1";
  const returnTo = typeof location.state?.returnTo === "string" && /^\/app\/personnel(?:\?|$)/.test(location.state.returnTo) ? location.state.returnTo : appPath("/personnel");
  const guideActive = isNew || params.get("guide") === "1";
  const requestedTab = params.get("tab");
  const initialTab = requestedTab === "tab-kimlik" || requestedTab === "tab-iletisim" || requestedTab === "tab-is"
    || requestedTab === "tab-mali" || requestedTab === "tab-ozluk" || requestedTab === "tab-evrak" ? requestedTab : undefined;
  const evidenceQ = useEmployeeCreationEvidence(id, !isNew && guideActive && user?.role === "hr-admin");
  const goBack = () => navigate(returnTo, { state: { scroll: location.state?.scroll ?? 0 } });
  const openFollowUpTab = (tab: "tab-evrak" | "tab-mali") =>
    setParams({ edit: "1", guide: "1", tab }, { state: location.state });
  const close = () => { if (editing && !isNew) setParams({}, { state: location.state }); else goBack(); };
  const closeOffboarding = () => {
    const next = new URLSearchParams(params);
    next.delete("offboarding");
    setParams(next, { replace: true, state: location.state });
  };
  const onCreated = (createdId: number) => navigate(appPath(`/personnel/${createdId}?edit=1&guide=1&tab=tab-evrak`), {
    replace: true,
    state: { returnTo, scroll: location.state?.scroll ?? 0, guideCreated: true },
  });
  if (!isNew && (!Number.isSafeInteger(id) || (id ?? 0) <= 0)) return <section className="ez-empty"><h1>Çalışan bulunamadı</h1><button className="btn btn-secondary" onClick={goBack}>Çalışanlara dön</button></section>;
  return <div className="ez-profile-page"><button className="ez-back-link" onClick={goBack}><span aria-hidden="true">←</span> Çalışanlara dön</button>
    {(location.state?.guideCreated || evidenceQ.data?.createdByCurrentUser) && id !== null && <PersonnelEntryFollowUp
      employeeId={id}
      verifiedCreation={evidenceQ.data?.createdByCurrentUser === true}
      onOpenDocuments={() => openFollowUpTab("tab-evrak")}
      onOpenCompensation={() => openFollowUpTab("tab-mali")}
    />}
    <PersonnelModal key={`${id}-${editing}`} employeeId={id} readOnly={!editing} embedded onClose={close}
      onCreated={onCreated} guided={guideActive} initialTab={initialTab}
      onEdit={user?.role === "hr-admin" ? () => setParams({ edit: "1" }, { state: location.state }) : undefined} />
    {showOffboarding && id !== null && <OffboardingModal employeeId={id} onClose={closeOffboarding} />}
  </div>;
}
