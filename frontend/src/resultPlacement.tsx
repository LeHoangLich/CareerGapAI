import type { ReactNode } from 'react';
import { createPortal } from 'react-dom';

/** An omitted target keeps the existing inline layout for recruiter screens. */
export function ResultPlacement({ target, children }: { target?: HTMLElement | null; children: ReactNode }) {
  if (target === undefined) return children;
  return target ? createPortal(children, target) : null;
}
