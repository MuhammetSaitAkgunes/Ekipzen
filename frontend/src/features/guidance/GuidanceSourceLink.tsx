import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { processTitles } from "./catalogQueries";

export function GuidanceSourceLink({ processKey }: { processKey: string }) {
  return <p><Link to={appPath(`/guidance/catalog?process=${encodeURIComponent(processKey)}`)}>
    {processTitles[processKey]} için kaynaklı rehberi aç
  </Link></p>;
}
