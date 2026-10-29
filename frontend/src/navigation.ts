import { useEffect, useLayoutEffect, useState } from 'react';

export type WorkspaceView = 'jobs' | 'applications' | 'private-resumes' | 'cv-builder' | 'profile'
  | 'recruiter-company' | 'recruiter-applicants' | 'public-resumes' | 'account' | 'access' | 'recruiter-jobs' | 'shared-profile' | 'administration';

type NavigationSession = { authenticated: boolean; roles: string[]; deletionPending?: boolean };
export const recruiterViews: WorkspaceView[] = ['recruiter-company', 'recruiter-jobs', 'recruiter-applicants'];
export const resumeViews: WorkspaceView[] = ['private-resumes', 'cv-builder', 'profile'];
export const viewLabels: Record<WorkspaceView, string> = {
  jobs: 'Khám phá việc làm', applications: 'Đơn ứng tuyển & Thông báo',
  'private-resumes': 'Quản lý CV riêng', 'cv-builder': 'Soạn & Cải thiện CV',
  profile: 'Hồ sơ & Quyền chia sẻ', 'public-resumes': 'Kho CV công khai',
  account: 'Tài khoản', access: 'Đăng nhập', 'recruiter-jobs': 'Tin & hồ sơ ứng tuyển',
  'shared-profile': 'Hồ sơ được chia sẻ', administration: 'Quản trị',
  'recruiter-company': 'Công ty', 'recruiter-applicants': 'Ứng viên',
};

export function resolveWorkspaceView(hash: string, session?: NavigationSession): WorkspaceView {
  if (session?.deletionPending) return 'account';
  const requested = hash.replace(/^#/, '');
  if (requested === 'access') return session?.authenticated ? 'jobs' : 'access';
  if (requested === 'account') return session?.authenticated ? 'account' : 'access';
  if (resumeViews.includes(requested as WorkspaceView))
    return !session?.authenticated ? 'access' : session.roles.includes('Candidate') ? requested as WorkspaceView : 'jobs';
  if (requested === 'applications') return !session?.authenticated ? 'access' : 'applications';
  if (requested === 'public-resumes') return !session?.authenticated ? 'access'
    : session.roles.some(role => role === 'Candidate' || role === 'Recruiter') ? 'public-resumes' : 'jobs';
  if (recruiterViews.includes(requested as WorkspaceView) || requested === 'shared-profile') return !session?.authenticated ? 'access'
    : session.roles.includes('Recruiter') ? requested as WorkspaceView : 'jobs';
  if (requested === 'administration') return !session?.authenticated ? 'access'
    : session.roles.includes('Admin') ? 'administration' : 'jobs';
  return 'jobs';
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
  }, [view, hash, loading]);
  return view;
}
