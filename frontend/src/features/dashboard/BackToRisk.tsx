import { useNavigate } from "react-router-dom";
import { appPath } from "../../paths";

export function BackToRisk() {
  const navigate = useNavigate();
  return (
    <button className="btn btn-secondary btn-sm" onClick={() => navigate(appPath("/dashboard"))}>
      <i aria-hidden="true" className="fa-solid fa-arrow-left" /> Risk Merkezi
    </button>
  );
}
