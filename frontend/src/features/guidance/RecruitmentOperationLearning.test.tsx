import { chooseOperationGuidance } from "../../test/chooseOperationGuidance";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { CandidateDetail } from "../recruitment/CandidateDetail";
import { CandidateCorrectionModal } from "../recruitment/CandidateCorrectionModal";
import { CandidateEvidenceModal, type EvidenceSelection } from "../recruitment/CandidateEvidenceModal";
import { PositionModal } from "../recruitment/PositionModal";
import type { CandidateDetailDto, PositionDto } from "../recruitment/queries";
import { guidanceStepDrafts } from "./stepDrafts";

const candidate = { id:5,name:"Deniz",appliedRole:"Uzman",positionId:null,positionTitle:null,status:"Mülakat",score:null,
  initials:"D",version:4,experienceYears:0,notes:[],evaluations:[],history:[],skills:[],experiences:[] } as CandidateDetailDto;
const position = {id:12,title:"Uzman",departmentId:null,isOpen:true,openCount:2,version:3,candidateCount:1} as PositionDto;
const note:EvidenceSelection={kind:"notes",record:{id:7,noteType:"İK Görüşmesi",text:"İlk not",version:2}};
const evaluation:EvidenceSelection={kind:"evaluations",record:{id:8,criterion:"SQL",score:4,maxScore:5,version:1}};
const sources=["candidate-create","first-review"].map((step,i)=>({...guidanceStepDrafts.find(x=>x.contentKey===`recruitment-${step}`)!,id:31+i,state:"published",isCurrent:true,contentVersion:1}));
const respond=(data:unknown,status=200)=>new Response(JSON.stringify(data),{status,headers:{"Content-Type":"application/json"}});
beforeEach(()=>{
  localStorage.clear();localStorage.setItem(SESSION_KEY,JSON.stringify({token:"T",refreshToken:"R",user:{id:"u",name:"İK",role:"hr-admin",roleLabel:"İK",initials:"İK"}}));
  stubApi({"/api/positions":[position],"/api/positions/12/corrections":[],"/api/departments":[],"/api/candidates/5":candidate,"/api/candidates/5/corrections":[],
    "/api/candidates/5/notes/7/revisions":{original:{text:"İlk not",noteType:"İK Görüşmesi",criterion:null,score:null,maxScore:null},revisions:[]},
    "/api/candidates/5/evaluations/8/revisions":{original:{text:null,noteType:null,criterion:"SQL",score:4,maxScore:5},revisions:[]},
    "/api/guidance/catalog":sources,"/api/guidance/catalog/versions/31":sources[0],"/api/guidance/catalog/versions/32":sources[1],
    "/api/guidance/help-preferences":[],"/api/guidance/operations":[],
  });
});
afterEach(()=>{vi.unstubAllGlobals();vi.restoreAllMocks();localStorage.clear();});
const render=(view:React.ReactNode)=>renderPage(<AuthProvider><ToastProvider>{view}</ToastProvider></AuthProvider>);
const writes=(path:string,method:string)=>vi.mocked(fetch).mock.calls.filter(([url,init])=>String(url)===path&&init?.method===method);
const choose = chooseOperationGuidance;

test("pozisyon düzeltmesi ilk sürüm ve ayrı kaynağı yalnız son onayda gönderir",async()=>{
  const base=globalThis.fetch;vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/positions/12"&&init?.method==="PUT"?respond({...position,openCount:1,version:4}):base(input,init)));
  render(<PositionModal position={position} onClose={()=>{}}/>);
  const count=screen.getByLabelText("Açık kadro sayısı");await userEvent.clear(count);await userEvent.type(count,"1");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi"),"Kadro kaynağı düzeltildi");await choose("Pozisyon düzeltirken kullandığım rehber sürümü");
  await userEvent.click(screen.getByRole("button",{name:"Bilgileri kontrol et"}));expect(writes("/api/positions/12","PUT")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button",{name:"Düzeltmeyi kaydet"}));await waitFor(()=>expect(writes("/api/positions/12","PUT")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/positions/12","PUT")[0][1]?.body))).toMatchObject({expectedVersion:3,guidanceContentRevisionId:31});
});

test("aday düzeltmesinin 409 yanıtı ilk sürüm ve seçilmiş dersi değiştirmez",async()=>{
  const base=globalThis.fetch;vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)==="/api/candidates/5"&&init?.method==="PUT"?respond({title:"Aday değişti"},409):base(input,init)));
  render(<CandidateCorrectionModal candidate={candidate} onClose={()=>{}}/>);const name=screen.getByLabelText("Ad Soyad");await userEvent.type(name," düzeltildi");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"),"Belge kontrol edildi");await choose("Aday düzeltirken kullandığım rehber sürümü");
  await userEvent.click(screen.getByRole("button",{name:"Bilgileri kontrol et"}));await userEvent.click(screen.getByRole("button",{name:"Düzeltmeyi kaydet"}));
  expect(await screen.findByRole("alert")).toHaveTextContent("Aday değişti");expect(name).toHaveValue("Deniz düzeltildi");
  expect(JSON.parse(String(writes("/api/candidates/5","PUT")[0][1]?.body))).toMatchObject({expectedVersion:4,guidanceContentRevisionId:31});
  expect(screen.getByRole("button", { name: "Rehber · seçildi" })).toBeInTheDocument();
});

test.each([note,evaluation])("not/değerlendirme düzeltmesi kendi kayıt kimliği, iki ilk sürüm ve ayrı dersini gönderir: $kind",async(selection)=>{
  const path=`/api/candidates/5/${selection.kind}/${selection.record.id}/revisions`;const base=globalThis.fetch;
  vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>String(input)===path&&init?.method==="POST"?respond({title:"Kaynak sürümü değişti"},409):base(input,init)));
  render(<CandidateEvidenceModal candidate={candidate} selection={selection} onClose={()=>{}}/>);
  const isNote=selection.kind==="notes";await userEvent.type(screen.getByLabelText(isNote?"Güncel not metni":"Güncel ölçüt")," düzeltildi");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"),"Görüşme belgesi kontrol edildi");const label=isNote?"Not düzeltirken kullandığım rehber sürümü":"Değerlendirme düzeltirken kullandığım rehber sürümü";await choose(label);
  await userEvent.click(screen.getByRole("button",{name:"Bilgileri kontrol et"}));expect(writes(path,"POST")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button",{name:"Yeni sürümü kaydet"}));expect(await screen.findByRole("alert")).toHaveTextContent("Kaynak sürümü değişti");
  expect(JSON.parse(String(writes(path,"POST")[0][1]?.body))).toMatchObject({expectedCandidateVersion:4,expectedRecordVersion:selection.record.version,guidanceContentRevisionId:32});
  expect(screen.getByRole("button",{name:"Rehber · seçildi"})).toBeInTheDocument();
});

test("not ekleme kaynağı değerlendirme formuna taşınmaz; iki son yazım ayrı seçilmiş kaynak taşır",async()=>{
  const base=globalThis.fetch;vi.stubGlobal("fetch",vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>init?.method==="POST"&&/\/api\/candidates\/5\/(notes|evaluations)$/.test(String(input))?respond({id:9},201):base(input,init)));
  render(<CandidateDetail id={5}/>);await screen.findByText("Deniz");await userEvent.click(screen.getByRole("button",{name:"Mülakat Notları"}));
  await userEvent.type(screen.getByLabelText("Mülakat notu"),"Gerçek gözlem");await choose("Not eklerken kullandığım rehber sürümü");await userEvent.click(screen.getByRole("button",{name:"Notu kontrol et"}));
  expect(writes("/api/candidates/5/notes","POST")).toHaveLength(0);await userEvent.click(screen.getByRole("button",{name:"Notu kaydet"}));await waitFor(()=>expect(writes("/api/candidates/5/notes","POST")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/candidates/5/notes","POST")[0][1]?.body))).toMatchObject({guidanceContentRevisionId:32});
  await userEvent.click(screen.getByRole("button",{name:"Değerlendirme"}));expect(screen.getByRole("group", { name: "Değerlendirme eklerken kullandığım rehber sürümü" })).toBeInTheDocument();
  await userEvent.type(screen.getByLabelText("Değerlendirme ölçütü"),"SQL");await userEvent.type(screen.getByLabelText("Verilen puan"),"4");await choose("Değerlendirme eklerken kullandığım rehber sürümü");
  await userEvent.click(screen.getByRole("button",{name:"Bilgileri kontrol et"}));await userEvent.click(screen.getByRole("button",{name:"Değerlendirmeyi kaydet"}));await waitFor(()=>expect(writes("/api/candidates/5/evaluations","POST")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/candidates/5/evaluations","POST")[0][1]?.body))).toMatchObject({guidanceContentRevisionId:32,score:4,maxScore:5});
});
