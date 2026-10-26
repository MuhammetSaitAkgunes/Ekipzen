import { createContext, useCallback, useContext, useMemo, useRef, useState, type FocusEvent, type ReactNode } from "react";

type Workspace = { active: string | null; register: (id: string, priority: number) => () => void; activate: (id: string | null) => void; focus: (event: FocusEvent<HTMLElement>) => void };
const GuidanceWorkspace = createContext<Workspace | null>(null);

export function GuidanceWorkspaceProvider({ children }: { children: ReactNode }) {
  const [active, setActive] = useState<string | null>(null);
  const mounted = useRef<{ id: string; priority: number }[]>([]); const collapsed = useRef(false);
  const register = useCallback((id: string, priority: number) => {
    mounted.current = [...mounted.current.filter(item => item.id !== id), { id, priority }];
    if (!collapsed.current) setActive(current => !current || priority > (mounted.current.find(item => item.id === current)?.priority ?? -Infinity) ? id : current);
    return () => {
      mounted.current = mounted.current.filter(item => item.id !== id);
      const next = [...mounted.current].sort((a,b) => b.priority-a.priority)[0]?.id ?? null;
      setActive(current => current === id ? collapsed.current ? null : next : current);
    };
  }, []);
  const activate = useCallback((id: string | null) => { collapsed.current = id === null; setActive(id); }, []);
  const focus = useCallback((event: FocusEvent<HTMLElement>) => {
    if (collapsed.current) return;
    if ((event.target as HTMLElement).closest('.ez-workbench-guide')) return;
    const work = (event.target as HTMLElement).closest('.ez-workbench');
    const guide = work?.querySelector<HTMLElement>(':scope > .ez-workbench-layout > .ez-workbench-guide .ez-context-guide');
    if (guide?.dataset.guideId) setActive(guide.dataset.guideId);
  }, []);
  const value = useMemo(() => ({ active, register, activate, focus }), [active, register, activate, focus]);
  return <GuidanceWorkspace.Provider value={value}>{children}</GuidanceWorkspace.Provider>;
}

export const useGuidanceWorkspace = () => useContext(GuidanceWorkspace);
