import { useEffect, useId, useRef, useState } from 'react';
import { ArrowRight, CaretDown, X } from '@phosphor-icons/react';

export function OnboardingBanner({ recruiter = false }: { recruiter?: boolean }) {
  const storageKey = `careergap:onboarding:v1:${recruiter ? 'recruiter' : 'candidate'}`;
  const [hidden, setHidden] = useState(() => {
    if (window.location.hash === '#how-it-works') return false;
    try { return localStorage.getItem(storageKey) === 'hidden'; } catch { return false; }
  });
  const [expanded, setExpanded] = useState(window.location.hash === '#how-it-works');
  const restoreButton = useRef<HTMLButtonElement>(null);
  const detailId = useId();
  useEffect(() => {
    const openFromLink = (event?: Event) => {
      if (window.location.hash === '#how-it-works' || event?.type === 'careergap:show-guide') { setHidden(false); setExpanded(true); }
    };
    window.addEventListener('hashchange', openFromLink);
    window.addEventListener('careergap:show-guide', openFromLink);
    return () => { window.removeEventListener('hashchange', openFromLink); window.removeEventListener('careergap:show-guide', openFromLink); };
  }, []);
  function show() {
    setHidden(false); setExpanded(true);
    try { localStorage.removeItem(storageKey); } catch { /* The UI preference also works in memory. */ }
  }
  function hide() {
    setHidden(true); setExpanded(false);
    try { localStorage.setItem(storageKey, 'hidden'); } catch { /* Storage is optional. */ }
    requestAnimationFrame(() => restoreButton.current?.focus({ preventScroll: true }));
  }
  const steps = recruiter ? [
    { title: 'Đăng tin rõ ràng', description: 'Xác nhận yêu cầu cho từng vị trí.', href: '#recruiter-jobs' },
    { title: 'Đối chiếu bằng chứng', description: 'Xem CV trong đúng ngữ cảnh ứng tuyển.', href: '#recruiter-jobs' },
    { title: 'Chủ động sàng lọc', description: 'Bạn quyết định, AI hỗ trợ.', href: '#recruiter-jobs' },
  ] : [
    { title: 'Chuẩn bị CV', description: 'Tải CV hoặc soạn từ dữ kiện của bạn.', href: '#private-resumes' },
    { title: 'Đối chiếu yêu cầu', description: 'Chọn CV để tìm việc và xem bằng chứng.', href: '#jobs' },
    { title: 'Chọn cơ hội', description: 'Ứng tuyển và theo dõi hồ sơ đã nộp.', href: '#applications' },
  ];
  if (hidden) return <div className="onboarding-restore flex justify-end">
    <button ref={restoreButton} type="button" className="link-button" onClick={show}>Xem hướng dẫn</button>
  </div>;
  return <section className="onboarding-banner" aria-labelledby="intro-title">
    <div className="onboarding-heading flex flex-wrap items-center justify-between gap-4">
      <div className="min-w-0">
        <p className="eyebrow">{recruiter ? 'TUYỂN DỤNG CÓ CƠ SỞ' : 'TỪ HỒ SƠ ĐẾN CƠ HỘI'}</p>
        <h2 id="intro-title">{recruiter ? 'Hiểu hồ sơ. Chọn người phù hợp.' : 'Rõ hồ sơ. Đúng cơ hội.'}</h2>
      </div>
      <div className="flex shrink-0 items-center gap-4">
        <button type="button" className="link-button onboarding-toggle" aria-expanded={expanded} aria-controls={detailId} onClick={() => setExpanded(!expanded)}>
          {expanded ? 'Thu gọn' : 'Hướng dẫn'}<CaretDown size={15} aria-hidden="true"/>
        </button>
        <button type="button" className="onboarding-dismiss secondary" aria-label="Ẩn hướng dẫn" onClick={hide}><X size={16} aria-hidden="true"/></button>
      </div>
    </div>
    <ol className="onboarding-steps" aria-label="Ba bước sử dụng">
      {steps.map((step, index) => <li key={step.title} className="flex min-w-0 items-center gap-2">
        <span className="onboarding-number" aria-hidden="true">0{index + 1}</span>
        <a href={step.href}>{step.title}</a>
        {index < steps.length - 1 && <ArrowRight size={14} className="onboarding-arrow" aria-hidden="true"/>}
      </li>)}
    </ol>
    <div id={detailId} hidden={!expanded} className="onboarding-details">
      <p>{recruiter ? 'Quản lý tin tuyển dụng, đối chiếu bằng chứng từ CV và chủ động quyết định từng bước sàng lọc.' : 'Chuẩn bị CV, đối chiếu yêu cầu và tự tin với bước tiếp theo. AI hỗ trợ; bạn quyết định.'}</p>
      <ol>{steps.map(step => <li key={step.title}><strong>{step.title}.</strong> {step.description}</li>)}</ol>
    </div>
  </section>;
}
