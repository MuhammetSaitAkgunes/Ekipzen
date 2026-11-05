import type { ComponentProps } from "react";
import { GuidanceDrawer } from "./GuidanceDrawer";
import { GuidanceOperationSource } from "./GuidanceOperationSource";
import { guidanceStepTitles } from "./stepDefinitions";
import { GuidanceWorkspaceProvider } from "./GuidanceWorkspace";

export function GuidanceOperationDrawer(props: ComponentProps<typeof GuidanceOperationSource>) {
  return <div className="ez-operation-help">
    <GuidanceDrawer
      title={guidanceStepTitles[props.process]?.[props.step] ?? "İşlem rehberi"}
      triggerLabel={props.value === null ? "Rehber" : "Rehber · seçildi"}
    >
      <GuidanceWorkspaceProvider>
        <GuidanceOperationSource {...props} />
      </GuidanceWorkspaceProvider>
    </GuidanceDrawer>
  </div>;
}
