import { CommunityBadge } from './community';
import { useEffect, useState, type ReactNode } from "react";
import { Dialog } from './ui';
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
      href="#top"
      className="brand flex items-center gap-2.5"
      aria-label="CareerGap AI — về đầu trang"
    >
      <span
        className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-[#245c48] text-white"
        aria-hidden="true"
      >
        <svg viewBox="0 0 24 24" width="23" height="23" fill="none">
          <path
            d="M17.5 6.5a7.5 7.5 0 1 0 0 11M12 12h7m-3-3 3 3-3 3"
            stroke="currentColor"
            strokeWidth="1.7"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </span>
      <span>
        CareerGap<span className="ml-1 text-[#245c48]">AI</span>
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
  useEffect(() => {
    const update = () => {
      setMenuOpen(false);
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
              { id: "applications", label: "Đơn ứng tuyển & Thông báo", icon: SquaresFour },
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
              {items.map(({ id, label, icon: ItemIcon }) => (
                <a
                  key={id}
                  href={`#${id}`}
                  aria-current={(activeView === id || id === 'notifications' && activeView === 'application' || id === 'jobs' && activeView === 'job' || id === 'private-resumes' && resumeViews.includes(activeView)
                    || id === 'recruiter-jobs' && recruiterViews.includes(activeView)) ? "location" : undefined}
                  onClick={() => {
                    setMenuOpen(false);
                  }}
                  className="flex items-center gap-3 rounded-md px-3 py-3 text-[13px] font-medium"
                >
                  <ItemIcon className="shrink-0" aria-hidden="true" size={19} />
                  <span>{label}</span>
                </a>
              ))}
              {!session?.authenticated && <button type="button" className="workspace-guide flex items-center gap-3 rounded-md px-3 py-3 text-left" onClick={() => {
                setMenuOpen(false);
                if (activeView === 'jobs') window.dispatchEvent(new Event('careergap:show-guide'));
                else window.location.hash = 'how-it-works';
              }}><FileText size={19} aria-hidden="true"/><span>Cách CareerGap hoạt động</span></button>}
            </nav>
            <div className="sidebar-note mx-6 mt-auto pt-16 pb-7">
              <LockKey
                size={21}
                className="mb-3 text-[#245c48]"
                aria-hidden="true"
              />
              <p className="mb-1 text-[13px] font-semibold text-[#303b34]">
                Hồ sơ của bạn, quyền của bạn.
              </p>
              <p className="m-0 text-xs leading-relaxed text-[var(--color-text-secondary)]">
                Bạn chọn phiên bản CV và quyết định khi nào công khai.
              </p>
            </div>
            <div className="border-t border-[#e6e9e5] p-4">
              {authenticated ? (
                <a
                  href="#account"
                  aria-current={activeView === 'account' ? 'page' : undefined}
                  className="account-link flex items-center gap-3 rounded-md p-2"
                >
                  <span
                    className="flex size-9 shrink-0 items-center justify-center rounded-full bg-[#edf1eb] text-xs font-semibold text-[#245c48]"
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
                    className="size-1.5 rounded-full bg-[#60816d]"
                    aria-hidden="true"
                  />
                  Bản thử nghiệm · Localhost
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
      <div className="workspace-shell" id="top">
        <aside className="workspace-sidebar flex flex-col bg-white">
          <div className="flex items-center justify-between gap-4 px-5 py-6 lg:px-6 lg:py-9">
            <Brand />
            <button
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
          <Dialog id="workspace-mobile-menu" open={menuOpen} title="Điều hướng" className="navigation-drawer" onClose={() => setMenuOpen(false)}>
            <div className="drawer-brand"><Brand/></div>
            <div className="drawer-content">{menuContent}</div>
          </Dialog>
        </aside>
        <div className="workspace-body min-w-0">
          <header className="workspace-topbar flex items-center justify-between gap-4 border-b border-[#e6e9e5] bg-white px-5 py-4 lg:px-10">
            <div className="flex min-w-0 items-center gap-2 text-xs text-[var(--color-text-secondary)]">
              <span>CareerGap AI</span>
              <span aria-hidden="true">/</span>
              <span className="font-medium text-[#303b34]">
                {viewLabels[activeView]}
              </span>
            </div>
            {authenticated ? (
              <div className="flex shrink-0 items-center gap-3"><CommunityBadge key={session?.userId??'guest'} session={session}/>
                <a href="#account" className="badge account-settings inline-flex items-center gap-2" aria-label="Cài đặt tài khoản" aria-current={activeView === 'account' ? 'page' : undefined}>
                  <GearSix size={16} aria-hidden="true"/>
                  {recruiter
                    ? "Nhà tuyển dụng"
                    : candidate
                      ? "Ứng viên"
                      : "Quản trị"}
                </a>
                <button
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
                  href="#access"
                  className="header-signin inline-flex shrink-0 items-center gap-2 text-xs font-semibold"
                >
                  Đăng nhập <ArrowUpRight size={15} aria-hidden="true" />
                </a>
              )
            )}
          </header>
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
    <div className="loading-state" role="status">
      <span>{label}</span>
      <div className="skeleton-line mt-4 h-3 w-3/4" aria-hidden="true" />
      <div className="skeleton-line mt-3 h-3 w-1/2" aria-hidden="true" />
    </div>
  );
}
