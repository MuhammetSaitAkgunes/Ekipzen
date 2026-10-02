import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { SettingsPage } from "../settings/SettingsPage";
import { DepartmentsPanel } from "../settings/DepartmentsPanel";
import { PositionModal } from "../recruitment/PositionModal";
import { CandidateModal } from "../recruitment/CandidateModal";
import { GuidanceOperationJourney } from "./GuidanceOperationJourney";
import { guidanceStepDrafts } from "./stepDrafts";

const company = {name:"Örnek şirket",website:null,systemEmail:null,phone:null,headquartersAddress:null,logoPath:null,recordToken:"A".repeat(64)};
const department = {id:3,name:"Ürün",code:"UR",employeeCount:2,recordToken:"D".repeat(64)};
const versions = [["company-company-profile",21],["company-departments",22],["recruitment-candidate-create",23]].map(([key,id]) => ({
  ...guidanceStepDrafts.find(x=>x.contentKey===key)!, id:Number(id),contentVersion:1,state:"published",isCurrent:true,
}));
const respond=(data:unknown,status=200)=>new Response(JSON.stringify(data),{status,headers:{"Content-Type":"application/json"}});
beforeEach(()=>{
  localStorage.clear(); localStorage.setItem(SESSION_KEY,JSON.stringify({token:"T",refreshToken:"R",user:{id:"u",name:"İK",role:"hr-admin",roleLabel:"İK",initials:"İK"}}));
  stubApi({"/api/settings":{company,notifications:{},subscription:{}},"/api/setup":{needsInitialization:false,hasEffectiveApprovedPayrollSettings:true},
    "/api/departments":[department],"/api/settings/company":{...company,name:"Yeni şirket",recordToken:"B".repeat(64)},
    "/api/departments/3":{...department,name:"Düzeltilmiş"},"/api/positions":[],"/api/candidates":{id:7,name:"Yeni aday"},
    "/api/guidance/catalog":versions,"/api/guidance/catalog/versions/21":versions[0],"/api/guidance/catalog/versions/22":versions[1],
    "/api/guidance/catalog/versions/23":versions[2],"/api/guidance/help-preferences":[],"/api/guidance/operations":[],
    "/api/guidance/recruitment/checks/candidate-create":{stepKey:"candidate-create",contentVersion:2,question:"Puan?",choices:["Boş","0","100"],attemptCount:0},
  });
});
afterEach(()=>{vi.unstubAllGlobals();vi.restoreAllMocks();localStorage.clear();});
const render=(view:React.ReactNode)=>renderPage(<AuthProvider><ToastProvider>{view}</ToastProvider></AuthProvider>);
async function choose(label:string) {
  await userEvent.click(screen.getByText(label,{exact:true}));
  await userEvent.click(await within(screen.getByText(label,{exact:true}).closest("details")!).findByRole("button",{name:"Bu rehber sürümünü işlemime bağla"}));
}
const writes=(path:string,method:string)=>vi.mocked(fetch).mock.calls.filter(([url,init])=>String(url)===path&&init?.method===method);

test("şirket profili kendi kaynak ve ilk kayıt belirteciyle kaydedilir; yolculuk gerçek kanıtı yeniler",async()=>{
  const base=globalThis.fetch;let saved=false;
  vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>{
    if(String(input)==="/api/settings/company"&&init?.method==="PUT")saved=true;
    if(String(input)==="/api/guidance/operations")return respond(saved?[{isCurrent:true,evidence:{evidenceKey:"p:1",operationKey:"company-profile-update",resourceId:1,outcome:"updated",occurredAtUtc:"2026-10-02T08:00:00Z",content:{contentRevisionId:21,title:"Şirket profili",contentVersion:1},preparationChanges:{resourceKind:"company-profile",resourceId:1,changedFields:["Şirket adı"]}}}]:[]);
    return base(input,init);
  }));
  render(<><SettingsPage/><GuidanceOperationJourney/></>);
  const name=await screen.findByLabelText("Şirket Adı");await userEvent.clear(name);await userEvent.type(name,"Yeni şirket");
  await choose("Şirket profili işleminde kullandığım rehber sürümü");expect(writes("/api/settings/company","PUT")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button",{name:/Değişiklikleri Kaydet/}));
  await waitFor(()=>expect(writes("/api/settings/company","PUT")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/settings/company","PUT")[0][1]?.body))).toMatchObject({name:"Yeni şirket",expectedRecordToken:company.recordToken,guidanceContentRevisionId:21});
  expect(await screen.findByText(/Gerçek kayıt türü: Şirket profili #1/)).toHaveTextContent("Şirket adı");
});

test("şirket profilinin bayat kaydı reddedilince taslak ve seçili kaynak korunur",async()=>{
  const base=globalThis.fetch;
  vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/settings/company"&&init?.method==="PUT"?respond({title:"Kayıt değişti; güncel profili inceleyin."},409):base(input,init)));
  render(<SettingsPage/>);const name=await screen.findByLabelText("Şirket Adı");await userEvent.clear(name);await userEvent.type(name,"Korunan taslak");
  await choose("Şirket profili işleminde kullandığım rehber sürümü");await userEvent.click(screen.getByRole("button",{name:/Değişiklikleri Kaydet/}));
  expect(await screen.findByText("Kayıt değişti; güncel profili inceleyin.")).toBeInTheDocument();expect(name).toHaveValue("Korunan taslak");
  expect(screen.getByText("Şirket profili işleminde kullandığım rehber sürümü · seçildi")).toBeInTheDocument();
});

test("departman ekleme kaynağı düzeltmeye taşınmaz; düzeltme ayrı kaynak ve kayıt belirteci kullanır",async()=>{
  const base=globalThis.fetch;
  vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/departments"&&init?.method==="POST"?respond({...department,id:8},201):base(input,init)));
  render(<DepartmentsPanel/>);await screen.findByText("Ürün");
  await userEvent.type(screen.getByLabelText("Yeni departman adı"),"Yeni birim");await choose("Departman eklerken kullandığım rehber sürümü");
  await userEvent.click(screen.getByRole("button",{name:"Departman ekle"}));await waitFor(()=>expect(writes("/api/departments","POST")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/departments","POST")[0][1]?.body))).toMatchObject({guidanceContentRevisionId:22});
  await userEvent.click(screen.getByRole("button",{name:"Düzenle"}));expect(screen.getByText("Departman düzeltirken kullandığım rehber sürümü")).toBeInTheDocument();
  expect(screen.queryByText("Departman düzeltirken kullandığım rehber sürümü · seçildi")).not.toBeInTheDocument();
  const name=screen.getByLabelText("Departman adı");await userEvent.clear(name);await userEvent.type(name,"Düzeltilmiş");
  await choose("Departman düzeltirken kullandığım rehber sürümü");await userEvent.click(screen.getByRole("button",{name:"Değişikliği kaydet"}));
  await waitFor(()=>expect(writes("/api/departments/3","PUT")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/departments/3","PUT")[0][1]?.body))).toMatchObject({name:"Düzeltilmiş",expectedRecordToken:department.recordToken,guidanceContentRevisionId:22});
});

test("pozisyon ön kontrolü kayıt üretmez; son oluşturma seçilen hazırlık dersini gönderir",async()=>{
  const base=globalThis.fetch;
  vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/positions"&&init?.method==="POST"?respond({id:12,title:"Uzman"},201):base(input,init)));
  render(<PositionModal onClose={()=>{}}/>);await userEvent.type(screen.getByLabelText("Pozisyon adı"),"Uzman");await screen.findByRole("option",{name:"Ürün"});
  await choose("Pozisyon açarken kullandığım rehber sürümü");await userEvent.click(screen.getByRole("button",{name:"Bilgileri kontrol et"}));
  expect(screen.getByRole("region",{name:"Pozisyon ön kontrolü"})).toHaveTextContent("Uzman");expect(writes("/api/positions","POST")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button",{name:"Pozisyonu oluştur"}));await waitFor(()=>expect(writes("/api/positions","POST")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/positions","POST")[0][1]?.body))).toMatchObject({title:"Uzman",guidanceContentRevisionId:23});
});

test("aday son onayda seçilen dersi gönderir; sunucu hatası taslağı ve puansız durumu korur",async()=>{
  const base=globalThis.fetch;const onCreated=vi.fn();
  vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/candidates"&&init?.method==="POST"?respond({title:"Pozisyon değişti; inceleyin."},409):base(input,init)));
  render(<CandidateModal onClose={()=>{}} onCreated={onCreated}/>);
  await userEvent.type(screen.getByLabelText("Ad Soyad"),"Yeni aday");await userEvent.type(screen.getByLabelText("Başvurulan rol"),"Uzman");
  await choose("Adayı oluştururken kullandığım rehber sürümü");await userEvent.click(screen.getByRole("button",{name:"Bilgileri kontrol et"}));expect(writes("/api/candidates","POST")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button",{name:"Adayı oluştur"}));expect(await screen.findByText("Pozisyon değişti; inceleyin.")).toBeInTheDocument();
  expect(JSON.parse(String(writes("/api/candidates","POST")[0][1]?.body))).toMatchObject({name:"Yeni aday",score:null,guidanceContentRevisionId:23});
  expect(screen.getByLabelText("Ad Soyad")).toHaveValue("Yeni aday");expect(screen.getByText("Adayı oluştururken kullandığım rehber sürümü · seçildi")).toBeInTheDocument();expect(onCreated).not.toHaveBeenCalled();
});
