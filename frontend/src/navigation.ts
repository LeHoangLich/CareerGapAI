import { useEffect, useLayoutEffect, useState } from 'react';

export type WorkspaceView = 'home' | 'analysis-history' | 'community' | 'jobs' | 'job' | 'applications' | 'application' | 'notifications' | 'private-resumes' | 'cv-builder' | 'profile'
  | 'recruiter-company' | 'recruiter-applicants' | 'public-resumes' | 'account' | 'access' | 'recruiter-jobs' | 'shared-profile' | 'administration';

type NavigationSession = { authenticated: boolean; roles: string[]; deletionPending?: boolean };
export const recruiterViews: WorkspaceView[] = ['recruiter-company', 'recruiter-jobs', 'recruiter-applicants'];
export const resumeViews: WorkspaceView[] = ['private-resumes', 'cv-builder', 'profile'];
export const viewLabels: Record<WorkspaceView, string> = {
  home: 'Trang chủ tổng hợp',
  'analysis-history': 'Lịch sử phân tích',
  community: 'Cộng đồng nghề nghiệp', jobs: 'Khám phá việc làm', job: 'Chi tiết việc làm', applications: 'Đơn ứng tuyển & Thông báo', application: 'Chi tiết hồ sơ ứng tuyển', notifications: 'Thông báo tuyển dụng',
  'private-resumes': 'Quản lý CV riêng', 'cv-builder': 'Soạn & Cải thiện CV',
  profile: 'Hồ sơ & Quyền chia sẻ', 'public-resumes': 'Kho CV công khai',
  account: 'Tài khoản', access: 'Đăng nhập', 'recruiter-jobs': 'Tin & hồ sơ ứng tuyển',
  'shared-profile': 'Hồ sơ được chia sẻ', administration: 'Quản trị',
  'recruiter-company': 'Công ty', 'recruiter-applicants': 'Ứng viên',
};

export function resolveWorkspaceView(hash: string, session?: NavigationSession): WorkspaceView {
  if (session?.deletionPending) return 'account';
  const requested = hash.replace(/^#/, '').split('?')[0];
  if (requested === 'home') return 'home';
  if (requested === 'jobs') return 'jobs';
  if (requested === 'community') return 'community';
  if (requested === 'job') return 'job';
  if (requested === 'access') return session?.authenticated ? 'home' : 'access';
  if (requested === 'account') return session?.authenticated ? 'account' : 'access';
  if (resumeViews.includes(requested as WorkspaceView))
    return !session?.authenticated ? 'access' : session.roles.includes('Candidate') ? requested as WorkspaceView : 'home';
  if (requested === 'applications')
    return !session?.authenticated ? 'access' : session.roles.includes('Candidate') ? 'applications' : 'home';
  if (requested === 'application' || requested === 'notifications') return !session?.authenticated ? 'access' : requested;
  if (requested === 'analysis-history') return !session?.authenticated ? 'access' : session.roles.some(role => role === 'Candidate' || role === 'Recruiter') ? 'analysis-history' : 'home';
  if (requested === 'public-resumes') return !session?.authenticated ? 'access'
    : session.roles.some(role => role === 'Candidate' || role === 'Recruiter') ? 'public-resumes' : 'home';
  if (recruiterViews.includes(requested as WorkspaceView) || requested === 'shared-profile') return !session?.authenticated ? 'access'
    : session.roles.includes('Recruiter') ? requested as WorkspaceView : 'home';
  if (requested === 'administration') return !session?.authenticated ? 'access'
    : session.roles.includes('Admin') ? 'administration' : 'home';
  return 'home';
}

export function useWorkspaceNavigation(session: NavigationSession | undefined, loading: boolean) {
  const [hash, setHash] = useState(() => window.location.hash
    || (new URLSearchParams(window.location.search).has('resetToken') ? '#access' : ''));
  const view = resolveWorkspaceView(hash, session);
  useEffect(() => {
    const update = () => {
      // The skip link moves keyboard focus; it is not a workspace route.
      if (window.location.hash !== '#main-content') setHash(window.location.hash);
    };
    window.addEventListener('hashchange', update);
    return () => window.removeEventListener('hashchange', update);
  }, []);
  useLayoutEffect(() => {
    if (loading) return;
    const frame = requestAnimationFrame(() => {
      document.getElementById('main-content')?.focus({ preventScroll: true });
      window.scrollTo(0, 0);
    });
    return () => cancelAnimationFrame(frame);
  }, [view, loading]);
  return view;
}
