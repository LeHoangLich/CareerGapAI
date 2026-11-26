import { useEffect, useId, useRef, type ComponentProps, type ReactNode } from 'react';
import { X } from '@phosphor-icons/react';

/** Native modal semantics provide focus containment and make the background inert. */
export function Dialog({ open, title, children, onClose, busy = false, className = '', id }: {
  open: boolean; title: string; children: ReactNode; onClose: () => void;
  busy?: boolean; className?: string; id?: string;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const closeRef = useRef(onClose);
  closeRef.current = onClose;
  const outsidePointer = useRef(false);
  useEffect(() => {
    const dialog = ref.current;
    if (!open || !dialog) return;
    const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    dialog.showModal();
    document.body.style.overflow = 'hidden';
    const closeOnNavigation = () => closeRef.current();
    window.addEventListener('hashchange', closeOnNavigation);
    return () => {
      window.removeEventListener('hashchange', closeOnNavigation);
      dialog.close();
      document.body.style.overflow = previousOverflow;
      if (trigger?.isConnected) trigger.focus({ preventScroll: true });
    };
  }, [open]);
  function outside(event: React.PointerEvent<HTMLDialogElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    return event.target === event.currentTarget && (event.clientX < rect.left || event.clientX > rect.right
      || event.clientY < rect.top || event.clientY > rect.bottom);
  }
  return <dialog ref={ref} id={id} className={`app-dialog ${className}`} aria-labelledby={titleId} aria-modal="true"
    onKeyDown={event => {
      if (event.key !== 'Tab') return;
      const controls = [...event.currentTarget.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex="0"]')]
        .filter(element => element.getClientRects().length > 0);
      const first = controls[0], last = controls.at(-1);
      if (!first || !last) { event.preventDefault(); return; }
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }}
    onCancel={event => { event.preventDefault(); if (!busy) onClose(); }}
    onPointerDown={event => { outsidePointer.current = outside(event); }}
    onPointerUp={event => { if (outsidePointer.current && outside(event) && !busy) onClose(); outsidePointer.current = false; }}>
    <div className="dialog-heading"><h2 id={titleId}>{title}</h2>
      <button type="button" className="secondary dialog-close" aria-label="Đóng hộp thoại" disabled={busy} onClick={onClose}><X size={18} aria-hidden="true"/></button>
    </div>
    {open && children}
  </dialog>;
}

export function AiBadge({ accepted = false, children }: { accepted?: boolean; children?: ReactNode }) {
  return <span className={`badge ${accepted ? 'badge-ai-accepted' : 'badge-ai-suggested'}`}>
    {children ?? (accepted ? 'Bạn đã xác nhận' : 'AI đề xuất · chờ rà soát')}
  </span>;
}

export function FieldError({ id, message }: { id: string; message: string }) {
  return message ? <span id={id} className="field-error" role="alert">{message}</span> : null;
}

type Control = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
function isControl(value: EventTarget | null): value is Control {
  return value instanceof HTMLInputElement || value instanceof HTMLSelectElement || value instanceof HTMLTextAreaElement;
}
function validationText(field: Control) {
  const validity = field.validity;
  if (validity.valueMissing) return 'Hãy điền hoặc chọn thông tin này.';
  if (validity.typeMismatch) return field instanceof HTMLInputElement && field.type === 'email'
    ? 'Hãy nhập địa chỉ email hợp lệ.' : 'Hãy nhập đúng định dạng của trường này.';
  if (validity.tooShort) return `Hãy nhập ít nhất ${field instanceof HTMLSelectElement ? 0 : field.minLength} ký tự.`;
  if (validity.rangeOverflow || validity.rangeUnderflow || validity.stepMismatch) return 'Hãy nhập giá trị trong khoảng và bước cho phép.';
  return field.validationMessage;
}

/** Enhance native constraint validation without guessing which field caused an API failure.
 * Errors sit beside the label so its accessible name stays unchanged. Existing descriptions survive.
 */
export function ValidatedForm({ children, onInvalidCapture, onInputCapture, onReset, ...props }: ComponentProps<'form'>) {
  const prefix = useId();
  const sequence = useRef(0);
  const errors = useRef(new Map<Control, { node: HTMLSpanElement; description: string | null; invalid: string | null }>());
  function clear(field: Control) {
    const error = errors.current.get(field);
    if (!error) return;
    error.node.remove();
    if (error.description === null) field.removeAttribute('aria-describedby'); else field.setAttribute('aria-describedby', error.description);
    if (error.invalid === null) field.removeAttribute('aria-invalid'); else field.setAttribute('aria-invalid', error.invalid);
    errors.current.delete(field);
  }
  useEffect(() => {
    const entries = errors.current;
    return () => { for (const error of entries.values()) error.node.remove(); entries.clear(); };
  }, []);
  useEffect(() => {
    for (const [field, error] of errors.current) if (!field.isConnected) { error.node.remove(); errors.current.delete(field); }
  });
  return <form {...props} onInvalidCapture={event => {
    onInvalidCapture?.(event);
    const field = event.target;
    if (!isControl(field)) return;
    let error = errors.current.get(field);
    if (!error) {
      const node = document.createElement('span');
      node.id = `${prefix}-field-error-${sequence.current++}`;
      node.className = 'field-error';
      node.setAttribute('role', 'alert');
      (field.closest('label') ?? field).insertAdjacentElement('afterend', node);
      error = { node, description: field.getAttribute('aria-describedby'), invalid: field.getAttribute('aria-invalid') };
      errors.current.set(field, error);
      field.setAttribute('aria-describedby', [error.description, node.id].filter(Boolean).join(' '));
    }
    error.node.textContent = validationText(field);
    field.setAttribute('aria-invalid', 'true');
  }} onInputCapture={event => {
    onInputCapture?.(event);
    if (isControl(event.target)) {
      if (event.target.validity.valid) clear(event.target);
      else { const error = errors.current.get(event.target); if (error) error.node.textContent = validationText(event.target); }
    }
  }} onReset={event => { onReset?.(event); for (const field of errors.current.keys()) clear(field); }}>{children}</form>;
}
