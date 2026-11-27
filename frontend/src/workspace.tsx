import { CommunityBadge } from './community';
import { useEffect, useState, type ReactNode } from "react";
import { signInReturnHref } from './jobLinks';
import { Dialog } from './ui';
import { TabIndicator } from './motion';
import { recruiterViews, resumeViews, viewLabels, type WorkspaceView } from './navigation';
import { ViewOutlet } from './persistentView';
import { OnboardingBanner } from './onboarding';
import {
  ArrowUpRight,
  Bell,
  Briefcase,
  Buildings,
  FileText,
  GearSix,
  GlobeHemisphereWest,
  House,
  IconContext,
  List,
  LockKey,
  ShieldCheck,
  SignOut,
  SquaresFour,
  UserCircle,
  X,
  type Icon,
} from "@phosphor-icons/react";

type Session = {
  userId?: string;
  authenticated: boolean;
  email?: string;
  roles: string[];
  deletionPending?: boolean;
};
type NavigationItem = { id: string; label: string; icon: Icon };

function Brand() {
  return (
    <a
      href="#home"
      className="brand flex items-center gap-2.5"
      aria-label="CareerGap AI — về trang chủ"
    >
      <span
        className="flex h-9 w-12 shrink-0 items-center justify-center rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-1"
        aria-hidden="true"
      >
        <svg viewBox="36 30 250 156" width="40" height="25" fill="none">
          <defs>
            <linearGradient id="cgBrandL" x1="50" y1="50" x2="120" y2="180" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stopColor="#1AA6BD" />
              <stop offset="55%" stopColor="#107E9F" />
              <stop offset="100%" stopColor="#084D74" />
            </linearGradient>
            <linearGradient id="cgBrandR" x1="270" y1="50" x2="200" y2="180" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stopColor="#FCA52E" />
              <stop offset="60%" stopColor="#EB821D" />
              <stop offset="100%" stopColor="#D46212" />
            </linearGradient>
          </defs>
          <circle cx="96" cy="52" r="16" fill="#1AA6BD" />
          <circle cx="236" cy="52" r="16" fill="#FCA52E" />
          <path d="M 96 72 C 72 72, 54 90, 42 114 C 53 111, 62 109, 68 108 C 64 138, 76 178, 114 178 C 132 178, 144 170, 152 158 C 142 152, 134 150, 124 152 C 102 156, 92 140, 92 120 C 92 98, 108 86, 128 94 C 134 96, 140 94, 144 88 C 132 76, 114 72, 96 72 Z" fill="url(#cgBrandL)" />
          <path d="M 236 72 C 266 72, 280 98, 278 128 C 276 160, 252 178, 220 178 C 202 178, 190 170, 180 158 C 190 152, 198 150, 208 152 C 230 156, 240 140, 240 120 C 240 98, 224 86, 204 94 C 198 96, 192 94, 188 88 C 200 76, 218 72, 236 72 Z" fill="url(#cgBrandR)" />
          <path d="M 144 88 C 130 98, 118 112, 114 126 C 111 135, 121 143, 131 137 C 138 127, 146 114, 155 102 C 159 96, 152 84, 144 88 Z" fill="#EB821D" />
          <path d="M 188 148 C 202 138, 214 124, 218 110 C 221 101, 211 93, 201 99 C 194 109, 186 122, 177 134 C 173 140, 180 152, 188 148 Z" fill="#084D74" />
          <path d="M 122 96 C 140 68, 192 68, 212 100 C 216 107, 207 115, 198 110 C 182 92, 150 92, 134 108 C 126 112, 118 103, 122 96 Z" fill="#107E9F" />
          <path d="M 210 140 C 192 168, 140 168, 120 136 C 116 129, 125 121, 134 126 C 150 144, 182 144, 198 128 C 206 124, 214 133, 210 140 Z" fill="#FCA52E" />
          <polygon points="166,106.5 168.7,114.3 176.9,114.5 170.4,119.4 172.8,127.3 166,122.6 159.2,127.3 161.6,119.4 155.1,114.5 163.3,114.3" fill="#F58B1B" />
        </svg>
      </span>
      <span>
        CareerGap<span className="ml-1 text-[var(--color-ai-accent)]">AI</span>
        <small className="brand-caption">HỒ SƠ & CƠ HỘI</small>
      </span>
    </a>
  );
}

export function Workspace({
  session,
  busy,
  activeView,
  onLogout,
  children,
}: {
  session?: Session;
  busy: boolean;
  activeView: WorkspaceView;
  onLogout: () => void;
  children: ReactNode;
}) {
  const [menuOpen, setMenuOpen] = useState(false);
  const [guideOpen, setGuideOpen] = useState(false);
  const [currentHash, setCurrentHash] = useState(() => window.location.hash);
  useEffect(() => {
    const update = () => {
      setMenuOpen(false);
      setCurrentHash(window.location.hash);
    };
    const desktop = window.matchMedia('(min-width: 1024px)');
    const onResize = () => { if (desktop.matches) setMenuOpen(false); };
    desktop.addEventListener('change', onResize);
    window.addEventListener("hashchange", update);
    return () => { window.removeEventListener("hashchange", update); desktop.removeEventListener('change', onResize); };
  }, []);
  const candidate = session?.roles.includes("Candidate");
  const recruiter = session?.roles.includes("Recruiter");
  const authenticated = session?.authenticated && !session.deletionPending;
  const isPublicShell = !authenticated && !session?.deletionPending && ['home', 'jobs', 'job', 'community'].includes(activeView);
  const items: NavigationItem[] = session?.deletionPending
    ? []
    : [
        { id: "home", label: "Trang chủ tổng hợp", icon: House },
        { id: "jobs", label: "Khám phá việc làm", icon: Briefcase },
        { id: "community", label: "Cộng đồng nghề nghiệp", icon: GlobeHemisphereWest },
        ...(authenticated ? [{ id: "notifications", label: "Thông báo tuyển dụng", icon: Bell }] : []),
        ...(candidate || recruiter ? [{ id: "analysis-history", label: "Lịch sử phân tích", icon: FileText }] : []),
        ...(candidate
          ? [
              {
                id: "private-resumes",
                label: "Trung tâm CV & Hồ sơ",
                icon: FileText,
              },
              { id: "applications", label: "Đơn ứng tuyển của tôi", icon: SquaresFour },
            ]
          : []),
        ...(recruiter
          ? [
              {
                id: "recruiter-jobs",
                label: "Tin & hồ sơ ứng tuyển",
                icon: Buildings,
              },
              {
                id: "shared-profile",
                label: "Hồ sơ được chia sẻ",
                icon: UserCircle,
              },
            ]
          : []),
        ...(candidate || recruiter
          ? [
              {
                id: "public-resumes",
                label: "Kho CV công khai",
                icon: GlobeHemisphereWest,
              },
            ]
          : []),
        ...(session?.roles.includes("Admin")
          ? [{ id: "administration", label: "Quản trị", icon: ShieldCheck }]
          : []),

      ];
  const menuContent = <>
            <div className="px-6 pb-3 pt-5 text-[12px] font-semibold tracking-[0.15em] text-[var(--color-text-secondary)]">
              {recruiter ? "KHÔNG GIAN TUYỂN DỤNG" : "KHÔNG GIAN NGHỀ NGHIỆP"}
            </div>
            <nav
              className="workspace-nav grid gap-1 px-3"
              aria-label="Điều hướng chính"
            >
              {items.map(({ id, label, icon: ItemIcon }) => {
                const isActive =
                  activeView === id ||
                  (id === 'notifications' && activeView === 'application') ||
                  (id === 'jobs' && activeView === 'job') ||
                  (id === 'private-resumes' && resumeViews.includes(activeView)) ||
                  (id === 'recruiter-jobs' && recruiterViews.includes(activeView));
                return (
                  <a
                    key={id}
                    href={`#${id}`}
                    aria-current={isActive ? 'location' : undefined}
                    onClick={() => {
                      setMenuOpen(false);
                    }}
                    className="flex items-center gap-3 rounded-md px-3 py-3 text-[13px] font-medium"
                  >
                    {isActive && <TabIndicator layoutId="sidebar-nav-active" />}
                    <ItemIcon className="shrink-0" aria-hidden="true" size={19} />
                    <span>{label}</span>
                  </a>
                );
              })}
              {!session?.authenticated && <button type="button" className="workspace-guide flex items-center gap-3 rounded-md px-3 py-3 text-left" onClick={() => {
                setMenuOpen(false);
                setGuideOpen(true);
              }}><FileText size={19} aria-hidden="true"/><span>Cách CareerGap hoạt động</span></button>}
            </nav>
            <div className="sidebar-note mx-6 mt-auto pt-16 pb-7">
              <LockKey
                size={21}
                className="mb-3 text-[var(--color-primary)]"
                aria-hidden="true"
              />
              <p className="mb-1 text-[13px] font-semibold text-[var(--color-text-primary)]">
                Hồ sơ của bạn, quyền của bạn.
              </p>
              <p className="m-0 text-xs leading-relaxed text-[var(--color-text-secondary)]">
                Bạn chọn phiên bản CV và quyết định khi nào công khai.
              </p>
            </div>
            <div className="border-t border-[var(--color-border)] p-4">
              {authenticated ? (
                <a
                  href="#account"
                  aria-current={activeView === 'account' ? 'page' : undefined}
                  className="account-link flex items-center gap-3 rounded-md p-2"
                >
                  <span
                    className="flex size-9 shrink-0 items-center justify-center rounded-full bg-[var(--color-badge-bg)] text-xs font-semibold text-[var(--color-primary)]"
                    aria-hidden="true"
                  >
                    {session.email?.slice(0, 2).toUpperCase() || "CG"}
                  </span>
                  <span className="min-w-0">
                    <strong className="block text-xs font-medium">
                      {recruiter
                        ? "Nhà tuyển dụng"
                        : candidate
                          ? "Ứng viên"
                          : "Quản trị viên"}
                    </strong>
                    <small className="block truncate text-[12px]">
                      {session.email}
                    </small>
                  </span>
                </a>
              ) : (
                <span className="flex items-center gap-2 px-2 py-2 text-xs text-[var(--color-text-secondary)]">
                  <span
                    className="size-1.5 rounded-full bg-[var(--color-focus)]"
                    aria-hidden="true"
                  />
                  Chế độ khách · CareerGap AI
                </span>
              )}
            </div>
  </>;

  return (
    <IconContext.Provider value={{ size: 20, weight: "regular" }}>
      <a className="skip-link" href="#main-content" onClick={event => {
        event.preventDefault();
        document.getElementById('main-content')?.focus();
      }}>
        Đến nội dung chính
      </a>
      <div className={`workspace-shell${isPublicShell ? ' is-public-shell' : ''}`} id="top">
        <aside className="workspace-sidebar flex flex-col bg-white">
          {isPublicShell ? (
            <div className="public-topnav-inner">
              <div className="public-topnav-left">
                <Brand />
                <div className="hidden items-center gap-1 lg:flex">
                  <nav className="workspace-nav" aria-label="Điều hướng chính">
                    {items.map(({ id, label, icon: ItemIcon }) => {
                      const isActive = activeView === id || (id === 'jobs' && activeView === 'job');
                      return (
                        <a
                          key={id}
                          href={`#${id}`}
                          aria-current={isActive ? 'location' : undefined}
                          className="flex items-center gap-2 rounded-md text-[13px] font-medium"
                        >
                          {isActive && <TabIndicator layoutId="public-nav-active" />}
                          <ItemIcon className="shrink-0" aria-hidden="true" size={18} />
                          <span>{label}</span>
                        </a>
                      );
                    })}
                  </nav>
                  <button
                    type="button"
                    className="workspace-guide flex items-center gap-2 rounded-md text-left"
                    onClick={() => setGuideOpen(true)}
                  >
                    <FileText size={18} aria-hidden="true" />
                    <span>Cách CareerGap hoạt động</span>
                  </button>
                </div>
              </div>
              <div className="public-topnav-actions">
                <a
                  href={signInReturnHref(currentHash)}
                  className="header-signin hidden items-center gap-1.5 text-xs font-semibold lg:inline-flex"
                >
                  Đăng nhập <ArrowUpRight size={15} aria-hidden="true" />
                </a>
                <a href="#access" className="btn-primary-link hidden lg:inline-flex">
                  Bắt đầu miễn phí
                </a>
                <button
                  type="button"
                  className="menu-toggle secondary lg:hidden"
                  aria-label={menuOpen ? "Đóng điều hướng" : "Mở điều hướng"}
                  aria-expanded={menuOpen}
                  aria-controls="workspace-mobile-menu"
                  onClick={() => setMenuOpen(!menuOpen)}
                >
                  {menuOpen ? <X /> : <List />}
                </button>
              </div>
            </div>
          ) : (
            <>
              <div className="flex items-center justify-between gap-4 px-5 py-6 lg:px-6 lg:py-9">
                <Brand />
                <button
                  type="button"
                  className="menu-toggle secondary lg:hidden"
                  aria-label={menuOpen ? "Đóng điều hướng" : "Mở điều hướng"}
                  aria-expanded={menuOpen}
                  aria-controls="workspace-mobile-menu"
                  onClick={() => setMenuOpen(!menuOpen)}
                >
                  {menuOpen ? <X /> : <List />}
                </button>
              </div>
              <div className="hidden min-h-0 flex-1 flex-col lg:flex">{menuContent}</div>
            </>
          )}
          <Dialog id="workspace-mobile-menu" open={menuOpen} title="Điều hướng" className="navigation-drawer" onClose={() => setMenuOpen(false)}>
            <div className="drawer-brand"><Brand/></div>
            <div className="drawer-content">{menuContent}</div>
          </Dialog>
        </aside>
        <div className="workspace-body min-w-0">
          {!isPublicShell && (
            <header className="workspace-topbar flex flex-wrap items-center justify-between gap-x-4 gap-y-2 border-b border-[var(--color-border)] bg-white px-5 py-4 lg:px-10">
              <div className="flex min-w-0 items-center gap-2 text-xs text-[var(--color-text-secondary)]">
                <span>CareerGap AI</span>
                <span aria-hidden="true">/</span>
                <span className="font-medium text-[var(--color-text-primary)]">
                  {viewLabels[activeView]}
                </span>
              </div>
              {authenticated ? (
                <div className="flex min-w-0 flex-wrap items-center gap-2 sm:gap-3"><CommunityBadge key={session?.userId??'guest'} session={session}/>
                  <a href="#account" className="badge account-settings inline-flex items-center gap-2" aria-label="Cài đặt tài khoản" aria-current={activeView === 'account' ? 'page' : undefined}>
                    <GearSix size={16} aria-hidden="true"/>
                    {recruiter
                      ? "Nhà tuyển dụng"
                      : candidate
                        ? "Ứng viên"
                        : "Quản trị"}
                  </a>
                  <button
                    type="button"
                    className="header-logout link-button flex items-center gap-2 text-xs"
                    disabled={busy}
                    onClick={onLogout}
                  >
                    <SignOut size={16} aria-hidden="true" />
                    Đăng xuất
                  </button>
                </div>
              ) : (
                !session?.deletionPending && (
                  <a
                    href={signInReturnHref(currentHash)}
                    className="header-signin inline-flex shrink-0 items-center gap-2 text-xs font-semibold"
                  >
                    Đăng nhập <ArrowUpRight size={15} aria-hidden="true" />
                  </a>
                )
              )}
            </header>
          )}
          <main
            id="main-content"
            tabIndex={-1}
            className="workspace-content mx-auto w-full max-w-[1400px] px-5 pb-8 pt-8 md:px-8 lg:px-10 lg:pt-10"
          >
            {candidate && resumeViews.includes(activeView) && <div className="resume-center-heading">
              <p className="eyebrow">KHÔNG GIAN CỦA BẠN</p>
              <h1>Trung tâm CV & Hồ sơ</h1>
              <nav className="resume-center-tabs flex gap-1 overflow-x-auto" aria-label="Trung tâm CV & Hồ sơ">
                {resumeViews.map(view => <a key={view} href={`#${view}`} aria-current={activeView === view ? 'page' : undefined}>{viewLabels[view]}</a>)}
              </nav>
            </div>}
            {recruiter && recruiterViews.includes(activeView) && <div className="resume-center-heading">
              <p className="eyebrow">KHÔNG GIAN TUYỂN DỤNG</p>
              <h1>Trung tâm tuyển dụng</h1>
              <nav className="resume-center-tabs recruiter-center-tabs flex gap-1 overflow-x-auto" aria-label="Trung tâm tuyển dụng">
                {recruiterViews.map(view => <a key={view} href={`#${view}`} aria-current={activeView === view ? 'page' : undefined}>{view === 'recruiter-jobs' ? 'Tin & JD' : viewLabels[view]}</a>)}
              </nav>
            </div>}
            {['applications', 'account'].includes(activeView) && <h1 className="page-title">{viewLabels[activeView]}</h1>}
            <ViewOutlet>{children}</ViewOutlet>
          </main>
        </div>
      </div>
      <Dialog open={guideOpen} title="Cách CareerGap AI hoạt động" onClose={() => setGuideOpen(false)}>
        <p>CareerGap AI giúp chuẩn hóa dữ kiện CV, đối chiếu yêu cầu công việc có trích dẫn bằng chứng và xây dựng lộ trình bổ sung kỹ năng. AI chỉ hỗ trợ tổng hợp; bạn là người xác nhận và quyết định.</p>
        <ol className="onboarding-details">
          <li><strong>1. Chuẩn bị &amp; xác nhận CV:</strong> Tải PDF/DOCX hoặc dùng trình tạo CV để tạo dữ liệu cấu trúc có kiểm chứng.</li>
          <li><strong>2. Đối chiếu CV–JD theo bằng chứng:</strong> Xem điểm tham chiếu, 6 nhóm tiêu chí và trích dẫn từng yêu cầu trực tiếp trên từng tin.</li>
          <li><strong>3. Lộ trình học &amp; Ứng tuyển:</strong> Lưu kế hoạch bổ sung kỹ năng theo tuần và theo dõi trạng thái hồ sơ đã nộp.</li>
        </ol>
        <div className="dialog-actions">
          <a className="secondary" href="#jobs" onClick={() => setGuideOpen(false)}>Khám phá việc làm</a>
          <a href="#access" onClick={() => setGuideOpen(false)}>Đăng nhập để bắt đầu</a>
        </div>
      </Dialog>
    </IconContext.Provider>
  );
}

export function WorkspaceIntro({ recruiter = false }: { recruiter?: boolean }) {
  return <OnboardingBanner recruiter={recruiter}/>;
}

export function StaticLoading({
  label = "Đang tải nội dung…",
}: {
  label?: string;
}) {
  return (
    <div className="loading-state" role="status" aria-live="polite">
      <div className="loading-status-bar">
        <span className="status-badge is-ai">ĐANG KẾT NỐI</span>
        <span>{label}</span>
      </div>
      <div className="loading-skeleton-card" aria-hidden="true">
        <div className="skeleton-line h-3 w-1/4" />
        <div className="skeleton-line h-6 w-2/3" />
        <div className="skeleton-line h-3 w-1/2" />
      </div>
      <div className="loading-cards-grid" aria-hidden="true">
        <div className="loading-skeleton-card"><div className="skeleton-line h-3 w-1/3" /><div className="skeleton-line h-4 w-5/6" /><div className="skeleton-line h-3 w-2/3" /></div>
        <div className="loading-skeleton-card"><div className="skeleton-line h-3 w-1/3" /><div className="skeleton-line h-4 w-5/6" /><div className="skeleton-line h-3 w-2/3" /></div>
        <div className="loading-skeleton-card"><div className="skeleton-line h-3 w-1/3" /><div className="skeleton-line h-4 w-5/6" /><div className="skeleton-line h-3 w-2/3" /></div>
      </div>
    </div>
  );
}
