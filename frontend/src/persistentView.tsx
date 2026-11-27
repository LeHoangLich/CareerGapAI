import { createContext, useContext, useLayoutEffect, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

const ViewOutletContext = createContext<HTMLDivElement | null>(null);
const ViewActiveContext = createContext(true);

export function useViewActive() {
  return useContext(ViewActiveContext);
}

export function ViewOutlet({ children }: { children: ReactNode }) {
  const [outlet, setOutlet] = useState<HTMLDivElement | null>(null);
  return <ViewOutletContext.Provider value={outlet}>
    <div className="workspace-view-outlet" ref={setOutlet}/>
    {children}
  </ViewOutletContext.Provider>;
}

/** Keep the React tree and native form controls alive once visited; only the active view joins the document. */
export function PersistentView({ active, name, children }: { active: boolean; name: string; children: ReactNode }) {
  const outlet = useContext(ViewOutletContext);
  const [mounted, setMounted] = useState(active);
  if (active && !mounted) setMounted(true);
  const [container] = useState(() => {
    const element = document.createElement('div');
    element.className = 'workspace-view';
    element.dataset.workspaceView = name;
    return element;
  });
  useLayoutEffect(() => {
    container.dataset.workspaceView = name;
    if (!active || !outlet) return;
    outlet.appendChild(container);
    return () => { container.remove(); };
  }, [active, outlet, container, name]);
  if (!mounted) return null;
  return createPortal(<ViewActiveContext.Provider value={active}>{children}</ViewActiveContext.Provider>, container);
}
