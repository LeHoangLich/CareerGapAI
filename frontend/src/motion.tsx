import { useRef, useState, type ChangeEvent, type DragEvent, type ReactNode } from 'react';
import { LazyMotion, MotionConfig, domMax, m } from 'motion/react';
import { UploadSimple } from '@phosphor-icons/react';

export const MOTION_SPRING = {
  type: 'spring' as const,
  stiffness: 420,
  damping: 32,
  mass: 0.8,
};

export function MotionRoot({ children }: { children: ReactNode }) {
  return (
    <MotionConfig reducedMotion="user">
      <LazyMotion features={domMax}>{children}</LazyMotion>
    </MotionConfig>
  );
}

export function Spinner({ size = 16, label }: { size?: number; label?: string }) {
  return (
    <svg
      className="ui-spinner"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      role={label ? 'status' : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
    >
      <circle
        cx="12"
        cy="12"
        r="9"
        stroke="currentColor"
        strokeOpacity="0.24"
        strokeWidth="2.5"
      />
      <path
        d="M21 12a9 9 0 0 0-9-9"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function ProgressBar({
  value,
  label,
  indeterminate = false,
}: {
  value?: number;
  label?: string;
  indeterminate?: boolean;
}) {
  const clamped = value === undefined ? undefined : Math.max(0, Math.min(100, value));
  return (
    <div
      className="progress-track"
      role="progressbar"
      aria-label={label ?? 'Tiến độ xử lý'}
      aria-valuenow={indeterminate ? undefined : clamped}
      aria-valuemin={0}
      aria-valuemax={100}
    >
      <div
        className={indeterminate ? 'progress-fill is-indeterminate' : 'progress-fill'}
        style={indeterminate ? undefined : { width: `${clamped ?? 0}%` }}
      />
    </div>
  );
}

/** Scroll-triggered reveal reserved strictly for Public Home / Landing sections. */
export function ScrollReveal({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <m.div
      className={className}
      initial={{ opacity: 0, y: 12 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, amount: 0.12 }}
      transition={{ duration: 0.24, ease: [0.2, 0, 0, 1] }}
    >
      {children}
    </m.div>
  );
}

export function TabIndicator({
  layoutId = 'workspace-nav-active',
  className = '',
}: {
  layoutId?: string;
  className?: string;
}) {
  return (
    <m.span
      layoutId={layoutId}
      className={`tab-active-indicator ${className}`.trim()}
      transition={MOTION_SPRING}
      aria-hidden="true"
    />
  );
}

export function FileDropzone({
  name,
  accept,
  required,
  label,
  hint,
  disabled = false,
}: {
  name: string;
  accept?: string;
  required?: boolean;
  label: string;
  hint?: string;
  disabled?: boolean;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [fileName, setFileName] = useState('');

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    setFileName(event.currentTarget.files?.[0]?.name ?? '');
  }

  function handleDragOver(event: DragEvent<HTMLDivElement>) {
    if (disabled) return;
    event.preventDefault();
    if (!dragging) setDragging(true);
  }

  function handleDragLeave(event: DragEvent<HTMLDivElement>) {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setDragging(false);
    }
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setDragging(false);
    if (disabled) return;
    const files = event.dataTransfer.files;
    if (!files?.length || !inputRef.current) return;
    if (typeof DataTransfer !== 'undefined') {
      const dt = new DataTransfer();
      dt.items.add(files[0]);
      inputRef.current.files = dt.files;
    }
    setFileName(files[0].name);
  }

  return (
    <div className="file-dropzone-field">
      <span className="file-dropzone-label">{label}</span>
      <m.div
        className={`file-dropzone${dragging ? ' is-dragging' : ''}${disabled ? ' is-disabled' : ''}`}
        animate={{ scale: dragging ? 1.015 : 1 }}
        transition={MOTION_SPRING}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
        <input
          ref={inputRef}
          name={name}
          type="file"
          accept={accept}
          required={required}
          disabled={disabled}
          aria-label={label}
          onChange={handleChange}
        />
        <UploadSimple size={20} className="file-dropzone-icon" aria-hidden="true" />
        <div className="file-dropzone-text">
          <strong>{fileName || (dragging ? 'Thả file vào đây…' : 'Kéo thả hoặc chọn file từ máy')}</strong>
          {hint && <small>{hint}</small>}
        </div>
      </m.div>
    </div>
  );
}

/** Pure immutable helper for keyboard-accessible list reordering. */
export function moveListItem<T>(list: readonly T[], fromIndex: number, toIndex: number): T[] {
  if (
    fromIndex < 0 ||
    toIndex < 0 ||
    fromIndex >= list.length ||
    toIndex >= list.length ||
    fromIndex === toIndex
  ) {
    return [...list];
  }
  const next = [...list];
  const [moved] = next.splice(fromIndex, 1);
  next.splice(toIndex, 0, moved);
  return next;
}
