import { useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { PersonnelModal } from "./PersonnelModal";

export function PersonnelProfilePage() {
  const { employeeId } = useParams();
  const { user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const isNew = location.pathname.endsWith("/new");
  const id = isNew ? null : Number(employeeId);
  const editing = user?.role === "hr-admin" && (isNew || params.get("edit") === "1");
  const returnTo = typeof location.state?.returnTo === "string" && /^\/app\/personnel(?:\?|$)/.test(location.state.returnTo) ? location.state.returnTo : appPath("/personnel");
  const initialTab = params.get("tab") === "tab-evrak" ? "tab-evrak" : undefined;
  const goBack = () => navigate(returnTo, { state: { scroll: location.state?.scroll ?? 0 } });
  const close = () => { if (editing && !isNew) setParams({}, { state: location.state }); else goBack(); };
  const onCreated = (createdId: number) => navigate(appPath(`/personnel/${createdId}?edit=1&guide=1&tab=tab-evrak`), {
    replace: true,
    state: { returnTo, scroll: location.state?.scroll ?? 0, guideCreated: true },
  });
  if (!isNew && (!Number.isSafeInteger(id) || (id ?? 0) <= 0)) return <section className="ez-empty"><h1>Çalışan bulunamadı</h1><button className="btn btn-secondary" onClick={goBack}>Çalışanlara dön</button></section>;
  return <div className="ez-profile-page"><button className="ez-back-link" onClick={goBack}><span aria-hidden="true">←</span> Çalışanlara dön</button>
    {location.state?.guideCreated && <div className="ez-guide-next" role="status">
      <strong>Çalışan kartı oluşturuldu.</strong> Şimdi evrakları yükleyin; ardından ücret paketini ve uygulama dışındaki işe giriş adımlarını ayrı ayrı kontrol edin.
    </div>}
    <PersonnelModal key={`${id}-${editing}`} employeeId={id} readOnly={!editing} embedded onClose={close}
      onCreated={onCreated} guided={params.get("guide") === "1"} initialTab={initialTab}
      onEdit={user?.role === "hr-admin" ? () => setParams({ edit: "1" }, { state: location.state }) : undefined} />
  </div>;
}
