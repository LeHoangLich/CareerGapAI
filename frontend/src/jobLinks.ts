import { resolveWorkspaceView } from './navigation';

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const workspaceRoutes = new Set([
  'home', 'jobs', 'job', 'community', 'applications', 'application', 'notifications',
  'analysis-history', 'private-resumes', 'cv-builder', 'profile', 'public-resumes',
  'account', 'recruiter-company', 'recruiter-jobs', 'recruiter-applicants',
  'shared-profile', 'administration',
]);

export function jobHref(id: string) {
  return `#job?id=${encodeURIComponent(id.toLowerCase())}`;
}

export function jobIdFromHash(hash: string): string | null {
  if (hash.split('?')[0] !== '#job') return null;
  const id = new URLSearchParams(hash.slice(hash.indexOf('?') + 1)).get('id');
  return id && guid.test(id) ? id.toLowerCase() : null;
}

export function jobShareUrl(id: string, currentUrl: string): string {
  const url = new URL(currentUrl);
  // Share the public job only; never copy reset tokens or other page query data.
  url.search = '';
  url.hash = jobHref(id);
  return url.href;
}

export function jobSignInHref(id: string) {
  return `#access?returnTo=${encodeURIComponent(jobHref(id))}`;
}

export function jobReturnAfterSignIn(hash: string): string | null {
  if (hash.split('?')[0] !== '#access') return null;
  const target = new URLSearchParams(hash.slice(hash.indexOf('?') + 1)).get('returnTo');
  const id = target && jobIdFromHash(target);
  return id ? jobHref(id) : null;
}

export function signInReturnHref(currentHash: string): string {
  const requested = currentHash.replace(/^#/, '').split('?')[0];
  if (!currentHash.startsWith('#') || requested === 'access' || !workspaceRoutes.has(requested)) {
    return '#access';
  }
  return `#access?returnTo=${encodeURIComponent(currentHash)}`;
}

export function safeReturnAfterSignIn(
  hash: string,
  session?: { authenticated: boolean; roles: string[]; deletionPending?: boolean },
): string | null {
  if (hash.split('?')[0] !== '#access') return null;
  const target = new URLSearchParams(hash.slice(hash.indexOf('?') + 1)).get('returnTo');
  if (!target || !/^#[a-z-]+(?:\?[^\r\n]*)?$/i.test(target)) return null;
  const requested = target.slice(1).split('?')[0];
  if (!workspaceRoutes.has(requested)) return null;
  if (requested === 'job') {
    const id = jobIdFromHash(target);
    return id ? jobHref(id) : null;
  }
  if (!session?.authenticated || session.deletionPending) return null;
  return resolveWorkspaceView(target, session) === requested ? target : null;
}
