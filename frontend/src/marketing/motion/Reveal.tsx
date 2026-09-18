import type { CSSProperties, ElementType, HTMLAttributes, ReactNode } from "react";
import { useInView } from "./useInView";
import { usePrefersReducedMotion } from "./usePrefersReducedMotion";

type RevealProps = HTMLAttributes<HTMLElement> & { as?: ElementType; delay?: number; className?: string; children?: ReactNode };

export function Reveal({ as: Tag = "div", delay = 0, className, style, children, ...rest }: RevealProps) {
  const reduced = usePrefersReducedMotion();
  const { ref, inView } = useInView<HTMLElement>({ once: true });
  const visible = reduced || inView;
  const mergedStyle = { ...style, "--ez-reveal-delay": `${delay}ms` } as CSSProperties;
  return (
    <Tag ref={ref} className={["ez-reveal", visible && "is-visible", className].filter(Boolean).join(" ")} style={mergedStyle} {...rest}>
      {children}
    </Tag>
  );
}
