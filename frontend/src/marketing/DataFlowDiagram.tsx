import { modules } from "./content";
import { useInView } from "./motion";

const CENTER = { x: 300, y: 190 };
const POSITIONS = [
  { x: 110, y: 70 }, { x: 490, y: 70 }, { x: 70, y: 190 },
  { x: 530, y: 190 }, { x: 110, y: 310 }, { x: 490, y: 310 },
];

export function DataFlowDiagram() {
  const { ref, inView } = useInView<HTMLDivElement>({ once: true, threshold: 0.3 });
  return (
    <div ref={ref} className={`ez-flow${inView ? " is-drawn" : ""}`}>
      <svg viewBox="0 0 600 380" role="img" aria-labelledby="flow-title" aria-describedby="flow-desc">
        <title id="flow-title">Modüller ve personel kaydı ilişkisi</title>
        <desc id="flow-desc">Altı modül de ortadaki personel kaydına bağlıdır; bilgi bir kez girilir ve tüm modüllerde kullanılır.</desc>
        {modules.map((m, i) => (
          <path key={`l-${m.id}`} className="ez-flow__line" style={{ transitionDelay: `${i * 120}ms` }}
            d={`M ${CENTER.x} ${CENTER.y} L ${POSITIONS[i].x} ${POSITIONS[i].y}`} pathLength={1} />
        ))}
        <circle className="ez-flow__core-ring" cx={CENTER.x} cy={CENTER.y} r={62} />
        <circle className="ez-flow__core" cx={CENTER.x} cy={CENTER.y} r={52} />
        <text className="ez-flow__core-text" x={CENTER.x} y={CENTER.y - 4} textAnchor="middle">Personel</text>
        <text className="ez-flow__core-text" x={CENTER.x} y={CENTER.y + 16} textAnchor="middle">kaydı</text>
        {modules.map((m, i) => (
          <g key={`n-${m.id}`} className="ez-flow__node">
            <rect x={POSITIONS[i].x - 70} y={POSITIONS[i].y - 22} width={140} height={44} rx={12} />
            <text x={POSITIONS[i].x} y={POSITIONS[i].y + 5} textAnchor="middle">{m.title}</text>
          </g>
        ))}
      </svg>
    </div>
  );
}
