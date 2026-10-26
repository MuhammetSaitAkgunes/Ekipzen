import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { CandidateDetail } from "../recruitment/CandidateDetail";
import { guidanceStepDrafts } from "./stepDrafts";

const candidate = { id:5,name:"Deniz",appliedRole:"Uzman",status:"Mülakat",score:null,version:4,initials:"D",
  notes:[],evaluations:[],history:[],skills:[],experiences:[],stageDecisions:[] };
const sources=["first-review","hire-preparation"].map((step,i)=>({...guidanceStepDrafts.find(x=>x.contentKey===`recruitment-${step}`)!,id:41+i,state:"published",isCurrent:true,contentVersion:1}));
const respond=(data:unknown,status=200)=>new Response(JSON.stringify(data),{status,headers:{"Content-Type":"application/json"}});
function fixture(data:unknown=candidate){stubApi({"/api/candidates/5":data,"/api/departments":[{id:2,name:"Teknik"}],
  "/api/guidance/catalog":sources,"/api/guidance/catalog/versions/41":sources[0],"/api/guidance/catalog/versions/42":sources[1],
  "/api/guidance/help-preferences":[],"/api/guidance/operations":[],
  "/api/guidance/recruitment/checks/first-review":{question:"Karar?",choices:["İncele","Tahmin et"],attemptCount:0,lastCorrect:null},
});}
beforeEach(()=>{localStorage.clear();localStorage.setItem(SESSION_KEY,JSON.stringify({token:"T",refreshToken:"R",user:{id:"u",name:"İK",role:"hr-admin",roleLabel:"İK",initials:"İK"}}));fixture();});
afterEach(()=>{vi.unstubAllGlobals();vi.restoreAllMocks();localStorage.clear();});
const render=()=>renderPage(<AuthProvider><ToastProvider><CandidateDetail id={5}/></ToastProvider></AuthProvider>);
const writes=(path:string,method="POST")=>vi.mocked(fetch).mock.calls.filter(([url,init])=>String(url)===path&&init?.method===method);
async function choose(label:string){await userEvent.click(screen.getByText(label,{exact:true}));await userEvent.click(await within(screen.getByText(label,{exact:true}).closest("aside")!).findByRole("button",{name:"Bu rehber sürümünü işlemime bağla"}));}

test("sonraki aşama kararı kendi dersini ve ilk aday sürümünü yalnız son teyitte gönderir",async()=>{
  const base=globalThis.fetch;vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/candidates/5/status"&&init?.method==="PATCH"?respond({...candidate,status:"Teklif",version:5}):base(input,init)));
  render();await screen.findByRole("heading",{name:"Deniz"});await userEvent.selectOptions(screen.getByLabelText("Pipeline durumu"),"Teklif");
  await choose("Aşama kararında kullandığım rehber sürümü");await userEvent.type(screen.getByLabelText("Aşama kararının gerekçesi ve dayanağı"),"Görüşmeler değerlendirildi");
  expect(writes("/api/candidates/5/status","PATCH")).toHaveLength(0);await userEvent.click(screen.getByLabelText("Aday kaydını ve aşama gerekçesini kontrol ettim."));
  await userEvent.click(screen.getByRole("button",{name:"Aşamayı kaydet"}));await waitFor(()=>expect(writes("/api/candidates/5/status","PATCH")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/candidates/5/status","PATCH")[0][1]?.body))).toMatchObject({status:"Teklif",expectedVersion:4,guidanceContentRevisionId:41});
});

async function prepareHire(){render();await screen.findByRole("heading",{name:"Deniz"});await userEvent.click(screen.getByRole("button",{name:"İşe Al"}));
  await choose("İşe alırken kullandığım rehber sürümü");await userEvent.selectOptions(screen.getByLabelText("Departman"),"2");
  await userEvent.type(screen.getByLabelText("İş e-postası"),"deniz@test.local");await userEvent.type(screen.getByLabelText("İşe giriş tarihi"),"2026-10-01");
  await userEvent.selectOptions(screen.getByLabelText("İmzalı sözleşmedeki tür"),"indefinite");await userEvent.click(screen.getByRole("button",{name:"Bilgileri kontrol et"}));}

test("işe alma eski kaynak reddinde taslak, ilk aday sürümü ve kaynak seçimini korur",async()=>{
  const base=globalThis.fetch;vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/candidates/5/hire"&&init?.method==="POST"?respond({title:"Kaynak artık güncel değil"},409):base(input,init)));
  await prepareHire();expect(writes("/api/candidates/5/hire")).toHaveLength(0);await userEvent.click(screen.getByRole("button",{name:"İşe almayı onayla"}));
  expect(await screen.findByRole("alert")).toHaveTextContent("Kaynak artık güncel değil");expect(screen.getByLabelText("İş e-postası")).toHaveValue("deniz@test.local");
  expect(screen.getByText("İşe alırken kullandığım rehber sürümü · seçildi")).toBeInTheDocument();
  expect(JSON.parse(String(writes("/api/candidates/5/hire")[0][1]?.body))).toMatchObject({expectedCandidateVersion:4,guidanceContentRevisionId:42,hireDate:"2026-10-01"});
});

test("işe alma personel sonucunu bekleyen giriş hesabından ayırır",async()=>{
  const base=globalThis.fetch;vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/candidates/5/hire"&&init?.method==="POST"?respond({candidateId:5,employeeId:9,employeeName:"Deniz",provisioning:{status:"pending",email:"deniz@test.local",attemptCount:1,lastError:null}}):base(input,init)));
  await prepareHire();await userEvent.click(screen.getByRole("button",{name:"İşe almayı onayla"}));expect(await screen.findByRole("heading",{name:"Personel kaydı oluşturuldu"})).toBeInTheDocument();
  expect(screen.getByText("Giriş hesabı hazırlanıyor; durumunu bu aday ekranından takip edin.")).toBeInTheDocument();expect(screen.getByRole("link",{name:"Personel kartını ve kalan adımları aç"})).toHaveAttribute("href",expect.stringContaining("/personnel/9"));
});

test("hesap yeniden denemesi ayrı ders ve açık son teyit ister; çalışan oluşturma göndermez",async()=>{
  fixture({...candidate,status:"İşe Alındı",linkedEmployee:{id:9,name:"Deniz",isActive:true},provisioning:{status:"failed",email:"old@test.local",attemptCount:1,lastError:"Geçici hata"}});
  const base=globalThis.fetch;vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/candidates/5/provisioning/retry"&&init?.method==="POST"?respond({title:"Başka deneme sürüyor"},409):base(input,init)));
  render();await screen.findByRole("heading",{name:"Deniz"});const email=screen.getByLabelText("Provizyon e-postası");await userEvent.clear(email);await userEvent.type(email,"correct@test.local");
  await choose("Hesap yeniden denemesinde kullandığım rehber sürümü");await userEvent.click(screen.getByRole("button",{name:"Şimdi yeniden dene"}));expect(writes("/api/candidates/5/provisioning/retry")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button",{name:"Kontrol ettim; yeniden denemeyi iste"}));await waitFor(()=>expect(writes("/api/candidates/5/provisioning/retry")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/candidates/5/provisioning/retry")[0][1]?.body))).toEqual({email:"correct@test.local",guidanceContentRevisionId:42});expect(writes("/api/candidates/5/hire")).toHaveLength(0);
  expect(email).toHaveValue("correct@test.local");expect(screen.getByText("Hesap yeniden denemesinde kullandığım rehber sürümü · seçildi")).toBeInTheDocument();
});
