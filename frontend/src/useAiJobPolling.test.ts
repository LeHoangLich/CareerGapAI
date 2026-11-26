import { afterEach, describe, expect, it, vi } from 'vitest';
import { createBoundedPoller } from './useAiJobPolling';

describe('createBoundedPoller', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('stops polling and triggers onTimeout once maxAttempts is reached', async () => {
    vi.useFakeTimers();
    let ticks = 0;
    let timedOut = false;
    const poller = createBoundedPoller({
      active: true,
      maxAttempts: 3,
      intervalMs: 1000,
      tick: async () => {
        ticks += 1;
        return false;
      },
      onTimeout: () => { timedOut = true; },
      onError: () => { throw new Error('unexpected error'); },
    });

    poller.start();
    await vi.runAllTimersAsync();

    expect(ticks).toBe(3);
    expect(timedOut).toBe(true);
    expect(poller.getAttempts()).toBe(3);
  });

  it('pauses when hidden and runs a single fresh check when active again within the same attempt budget', async () => {
    vi.useFakeTimers();
    let ticks = 0;
    let timedOut = false;
    const poller = createBoundedPoller({
      active: true,
      maxAttempts: 3,
      intervalMs: 1000,
      tick: async () => {
        ticks += 1;
        return ticks >= 2;
      },
      onTimeout: () => { timedOut = true; },
      onError: () => { throw new Error('unexpected error'); },
    });

    poller.start();
    await vi.advanceTimersByTimeAsync(0);
    expect(ticks).toBe(1);

    // Hide before the next 1000ms timer fires; time advances with zero ticks.
    poller.setActive(false);
    await vi.advanceTimersByTimeAsync(10000);
    expect(ticks).toBe(1);

    // Returning to the view checks immediately and finishes cleanly on tick 2.
    poller.setActive(true);
    await vi.advanceTimersByTimeAsync(0);
    expect(ticks).toBe(2);
    await vi.advanceTimersByTimeAsync(5000);
    expect(ticks).toBe(2);
    expect(timedOut).toBe(false);
  });

  it('aborts in-flight work on stop without invoking error or completion', async () => {
    vi.useFakeTimers();
    let aborted = false;
    let errored = false;
    const poller = createBoundedPoller({
      active: true,
      maxAttempts: 5,
      intervalMs: 1000,
      tick: signal => new Promise<boolean>((_, reject) => {
        signal.addEventListener('abort', () => {
          aborted = true;
          reject(new DOMException('Aborted', 'AbortError'));
        });
      }),
      onTimeout: () => {},
      onError: () => { errored = true; },
    });

    poller.start();
    poller.stop();
    await vi.runAllTimersAsync();

    expect(aborted).toBe(true);
    expect(errored).toBe(false);
  });
});
