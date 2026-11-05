import type { ComponentProps, ReactNode } from "react";
import { GuidanceDrawer } from "./GuidanceDrawer";
import { GuidanceOperationSource } from "./GuidanceOperationSource";
import { guidanceStepTitles } from "./stepDefinitions";
import { GuidanceWorkspaceProvider } from "./GuidanceWorkspace";

export function GuidanceOperationDrawer({ children, ...props }: ComponentProps<typeof GuidanceOperationSource> & { children?: ReactNode }) {
  return <div className="ez-operation-help" role="group" aria-label={props.label ?? "Gerçek işlemde kullandığım rehber sürümü"}>
    <GuidanceDrawer
      title={guidanceStepTitles[props.process]?.[props.step] ?? "İşlem rehberi"}
      triggerLabel={props.value === null ? "Rehber" : "Rehber · seçildi"}
    >
      <GuidanceWorkspaceProvider>
        <GuidanceOperationSource {...props} />
      </GuidanceWorkspaceProvider>
      {children}
    </GuidanceDrawer>
  </div>;
}
