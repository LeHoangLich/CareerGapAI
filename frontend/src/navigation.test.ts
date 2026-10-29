import { describe, expect, it } from 'vitest';
import { resolveWorkspaceView, resumeViews, recruiterViews } from './navigation';

const candidate = { authenticated: true, roles: ['Candidate'] };
const recruiter = { authenticated: true, roles: ['Recruiter'] };
const admin = { authenticated: true, roles: ['Admin'] };
const guest = { authenticated: false, roles: [] };

describe('workspace route resolution', () => {
  it('keeps all existing candidate deep links and CV tabs', () => {
    for (const view of ['jobs', 'applications', ...resumeViews, 'public-resumes', 'account'])
      expect(resolveWorkspaceView(`#${view}`, candidate)).toBe(view);
  });
  it('uses jobs for the default, top, introductory and unknown hashes', () => {
    for (const hash of ['', '#top', '#jobs', '#how-it-works', '#unknown'])
      expect(resolveWorkspaceView(hash, candidate)).toBe('jobs');
  });
  it('sends guests to access for private destinations', () => {
    for (const view of ['applications', ...resumeViews, 'public-resumes', 'account', 'administration', ...recruiterViews, 'shared-profile'])
      expect(resolveWorkspaceView(`#${view}`, guest)).toBe('access');
    expect(resolveWorkspaceView('#jobs', guest)).toBe('jobs');
  });
  it('keeps recruiter links while excluding candidate and admin screens', () => {
    for (const view of [...recruiterViews, 'shared-profile', 'public-resumes', 'applications', 'account'])
      expect(resolveWorkspaceView(`#${view}`, recruiter)).toBe(view);
    for (const view of [...resumeViews, 'administration'])
      expect(resolveWorkspaceView(`#${view}`, recruiter)).toBe('jobs');
    for (const view of recruiterViews) {
      expect(resolveWorkspaceView(`#${view}`, candidate)).toBe('jobs');
      expect(resolveWorkspaceView(`#${view}`, admin)).toBe('jobs');
    }
  });
  it('keeps admin access separate from private and public CVs', () => {
    expect(resolveWorkspaceView('#administration', admin)).toBe('administration');
    for (const view of [...resumeViews, 'public-resumes', 'shared-profile'])
      expect(resolveWorkspaceView(`#${view}`, admin)).toBe('jobs');
  });
  it('isolates deletion-pending accounts regardless of their hash', () => {
    for (const hash of ['#jobs', '#private-resumes', '#administration', '#access'])
      expect(resolveWorkspaceView(hash, { ...candidate, deletionPending: true })).toBe('account');
  });
  it('leaves the sign-in screen after successful authentication', () => {
    expect(resolveWorkspaceView('#access', guest)).toBe('access');
    expect(resolveWorkspaceView('#access', candidate)).toBe('jobs');
  });
});
