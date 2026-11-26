import { useEffect, useRef, useState } from 'react';

export const DEFAULT_POLL_MAX_ATTEMPTS = 60;
export const DEFAULT_POLL_TIMEOUT_MESSAGE =
  'Tác vụ đang mất nhiều thời gian hơn dự kiến. Hệ thống đã tạm dừng tự kiểm tra; bạn có thể bấm kiểm tra lại sau.';

export type BoundedPollerOptions = {
  active: boolean;
  maxAttempts?: number;
  initialDelayMs?: number;
  intervalMs: number | ((attempt: number) => number);
  tick: (signal: AbortSignal) => Promise<boolean>;
  onTimeout: () => void;
  onError: (error: unknown) => void;
};

/** Bounded, pauseable poller used by AI status panels so hidden views do not poll indefinitely. */
export function createBoundedPoller({
  active: initialActive,
  maxAttempts = DEFAULT_POLL_MAX_ATTEMPTS,
  initialDelayMs = 0,
  intervalMs,
  tick,
  onTimeout,
  onError,
}: BoundedPollerOptions) {
  let active = initialActive;
  let stopped = false;
  let done = false;
  let inFlight = false;
  let attempts = 0;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let controller: AbortController | undefined;

  const delayFor = (attempt: number) =>
    typeof intervalMs === 'function' ? intervalMs(attempt) : intervalMs;

  function clearScheduled() {
    if (timer !== undefined) {
      clearTimeout(timer);
      timer = undefined;
    }
    controller?.abort();
    controller = undefined;
    inFlight = false;
  }

  async function runOnce() {
    if (stopped || done || !active || inFlight) return;
    if (attempts >= maxAttempts) {
      done = true;
      onTimeout();
      return;
    }
    attempts += 1;
    inFlight = true;
    const current = new AbortController();
    controller = current;
    try {
      const finished = await tick(current.signal);
      if (stopped || current.signal.aborted) return;
      inFlight = false;
      controller = undefined;
      if (finished) {
        done = true;
        return;
      }
      if (attempts >= maxAttempts) {
        done = true;
        onTimeout();
        return;
      }
      if (active) {
        timer = setTimeout(() => { void runOnce(); }, delayFor(attempts));
      }
    } catch (error) {
      if (stopped || current.signal.aborted) return;
      inFlight = false;
      controller = undefined;
      done = true;
      onError(error);
    }
  }

  return {
    start() {
      if (stopped || done || !active) return;
      if (initialDelayMs > 0) {
        timer = setTimeout(() => { void runOnce(); }, initialDelayMs);
      } else {
        void runOnce();
      }
    },
    setActive(nextActive: boolean) {
      if (nextActive === active || stopped || done) {
        active = nextActive;
        return;
      }
      active = nextActive;
      if (!active) {
        clearScheduled();
      } else {
        clearScheduled();
        void runOnce();
      }
    },
    stop() {
      stopped = true;
      clearScheduled();
    },
    getAttempts() {
      return attempts;
    },
  };
}

export type UseAiJobPollingOptions = {
  pollKey: string | undefined;
  active?: boolean;
  maxAttempts?: number;
  initialDelayMs?: number;
  intervalMs?: number | ((attempt: number) => number);
  tick: (signal: AbortSignal) => Promise<boolean>;
  onError?: (error: unknown) => void;
  timeoutMessage?: string;
};

/** Poll an async AI job key while view is active, pausing on hide and stopping after maxAttempts. */
export function useAiJobPolling({
  pollKey,
  active = true,
  maxAttempts = DEFAULT_POLL_MAX_ATTEMPTS,
  initialDelayMs = 0,
  intervalMs = 2500,
  tick,
  onError,
  timeoutMessage = DEFAULT_POLL_TIMEOUT_MESSAGE,
}: UseAiJobPollingOptions) {
  const [timedOut, setTimedOut] = useState(false);
  const [restartTick, setRestartTick] = useState(0);
  const tickRef = useRef(tick);
  tickRef.current = tick;
  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;
  const intervalRef = useRef(intervalMs);
  intervalRef.current = intervalMs;
  const timeoutRef = useRef(timeoutMessage);
  timeoutRef.current = timeoutMessage;
  const activeRef = useRef(active);
  activeRef.current = active;
  const pollerRef = useRef<ReturnType<typeof createBoundedPoller> | undefined>(undefined);

  useEffect(() => {
    setTimedOut(false);
    if (!pollKey) {
      pollerRef.current = undefined;
      return;
    }
    const poller = createBoundedPoller({
      active: activeRef.current,
      maxAttempts,
      initialDelayMs,
      intervalMs: attempt =>
        typeof intervalRef.current === 'function' ? intervalRef.current(attempt) : intervalRef.current,
      tick: signal => tickRef.current(signal),
      onTimeout: () => {
        setTimedOut(true);
        onErrorRef.current?.(new Error(timeoutRef.current));
      },
      onError: error => {
        onErrorRef.current?.(error);
      },
    });
    pollerRef.current = poller;
    poller.start();
    return () => {
      poller.stop();
      if (pollerRef.current === poller) pollerRef.current = undefined;
    };
  }, [pollKey, maxAttempts, initialDelayMs, restartTick]);

  useEffect(() => {
    pollerRef.current?.setActive(active);
  }, [active]);

  return {
    timedOut,
    retryPolling: () => setRestartTick(value => value + 1),
  };
}
