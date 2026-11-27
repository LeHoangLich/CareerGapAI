import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { moveListItem } from './motion';
import { StaticLoading } from './ui';

describe('motion helpers and loading variants', () => {
  it('moveListItem reorders items immutably and ignores out-of-bounds moves', () => {
    const original = ['A', 'B', 'C'];
    expect(moveListItem(original, 0, 2)).toEqual(['B', 'C', 'A']);
    expect(moveListItem(original, 2, 1)).toEqual(['A', 'C', 'B']);
    expect(moveListItem(original, -1, 1)).toEqual(['A', 'B', 'C']);
    expect(moveListItem(original, 0, 5)).toEqual(['A', 'B', 'C']);
    expect(original).toEqual(['A', 'B', 'C']);
  });

  it('StaticLoading renders status, progressbar, and variant-specific skeletons', () => {
    for (const variant of ['default', 'cards', 'analysis', 'table'] as const) {
      const html = renderToStaticMarkup(<StaticLoading variant={variant} message="Đang tải test…" />);
      expect(html).toContain('role="status"');
      expect(html).toContain('role="progressbar"');
      expect(html).toContain('Đang tải test…');
      expect(html).toContain('skeleton-line');
    }
    const cardsHtml = renderToStaticMarkup(<StaticLoading variant="cards" />);
    expect(cardsHtml).toContain('loading-skeleton-grid');
  });
});
