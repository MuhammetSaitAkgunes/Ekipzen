import { Badge } from "../ui";
import { STATUS_META, type ModuleStatus } from "./content";

export function StatusBadge({ status }: { status: ModuleStatus }) {
  const meta = STATUS_META[status];
  return (
    <span className="ez-status" data-status={status} title={meta.description}>
      <Badge tone={meta.tone}>{meta.label}</Badge>
    </span>
  );
}
