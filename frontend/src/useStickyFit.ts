import { useEffect, useRef } from 'react';

/** Sticky panels must remain fully reachable on short viewports and at browser zoom. */
export function useStickyFit() {
  const ref = useRef<HTMLElement>(null);
  useEffect(() => {
    const element = ref.current;
    if (!element) return;
    let frame = 0;
    const update = () => {
      const rect = element.getBoundingClientRect();
      const fits = rect.height > 0 && rect.height <= window.innerHeight - 48;
      element.dataset.stickyFits = String(fits);
      element.dataset.stickyActive = String(fits && window.matchMedia('(min-width: 1024px)').matches
        && rect.top <= 24 && window.scrollY > 0);
    };
    const onScroll = () => { if (!frame) frame = requestAnimationFrame(() => { frame = 0; update(); }); };
    const observer = new ResizeObserver(update);
    observer.observe(element);
    window.addEventListener('resize', update);
    window.addEventListener('scroll', onScroll, { passive: true });
    update();
    return () => { observer.disconnect(); cancelAnimationFrame(frame); window.removeEventListener('resize', update); window.removeEventListener('scroll', onScroll); };
  }, []);
  return ref;
}
