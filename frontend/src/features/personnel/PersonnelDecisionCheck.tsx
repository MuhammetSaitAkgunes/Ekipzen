import { GuidanceDecisionCheck } from "../guidance/GuidanceDecisionCheck";
import type { PersonnelGuideStep } from "./PersonnelGuide";

export function PersonnelDecisionCheck({ step }: { step: PersonnelGuideStep }) {
  return <GuidanceDecisionCheck process="employee-entry" step={step} operationName="personel işlemi" />;
}
