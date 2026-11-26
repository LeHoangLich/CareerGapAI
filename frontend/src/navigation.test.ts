import { describe, expect, it } from 'vitest';
import { resolveWorkspaceView, resumeViews, recruiterViews } from './navigation';

const candidate = { authenticated: true, roles: ['Candidate'] };
const recruiter = { authenticated: true, roles: ['Recruiter'] };
const admin = { authenticated: true, roles: ['Admin'] };
const guest = { authenticated: false, roles: [] };

describe('workspace route resolution', () => {
  it('guards analysis history and its deep links',()=>{
    expect(resolveWorkspaceView('#analysis-history?id=example',candidate)).toBe('analysis-history');
    expect(resolveWorkspaceView('#analysis-history',recruiter)).toBe('analysis-history');
    expect(resolveWorkspaceView('#analysis-history',guest)).toBe('access');
    expect(resolveWorkspaceView('#analysis-history',admin)).toBe('home');
  });
  it('requires authentication for application notification targets',()=>{
    expect(resolveWorkspaceView('#notifications',guest)).toBe('access');
    expect(resolveWorkspaceView('#notifications',candidate)).toBe('notifications');
    expect(resolveWorkspaceView('#notifications',recruiter)).toBe('notifications');
    expect(resolveWorkspaceView('#application?id=example',guest)).toBe('access');
    for(const session of [candidate,recruiter,admin])expect(resolveWorkspaceView('#application?id=example',session)).toBe('application');
    expect(resolveWorkspaceView('#application?id=example',{...candidate,deletionPending:true})).toBe('account');
  });
  it('gates #applications to Candidate role only', () => {
    expect(resolveWorkspaceView('#applications', guest)).toBe('access');
    expect(resolveWorkspaceView('#applications', candidate)).toBe('applications');
    expect(resolveWorkspaceView('#applications', recruiter)).toBe('home');
    expect(resolveWorkspaceView('#applications', admin)).toBe('home');
  });
  it('allows public job detail links and keeps deletion-pending accounts isolated', () => {
    for (const session of [undefined, guest, candidate, recruiter, admin])
      expect(resolveWorkspaceView('#job?id=example', session)).toBe('job');
    expect(resolveWorkspaceView('#job?id=example', { ...candidate, deletionPending: true })).toBe('account');
  });
  it('allows public home and community deep links and keeps deletion-pending accounts isolated', () => {
    for (const session of [guest, candidate, recruiter, admin]) {
      expect(resolveWorkspaceView('#home', session)).toBe('home');
      expect(resolveWorkspaceView('#community?post=example', session)).toBe('community');
    }
    expect(resolveWorkspaceView('#home', { ...candidate, deletionPending: true })).toBe('account');
    expect(resolveWorkspaceView('#community', { ...candidate, deletionPending: true })).toBe('account');
  });
  it('keeps all existing candidate deep links and CV tabs', () => {
    for (const view of ['jobs', 'applications', ...resumeViews, 'public-resumes', 'account'])
      expect(resolveWorkspaceView(`#${view}`, candidate)).toBe(view);
  });
  it('uses home for the default, top, introductory and unknown hashes while keeping #jobs direct', () => {
    for (const hash of ['', '#top', '#home', '#how-it-works', '#unknown'])
      expect(resolveWorkspaceView(hash, candidate)).toBe('home');
    expect(resolveWorkspaceView('#jobs', candidate)).toBe('jobs');
    expect(resolveWorkspaceView('#jobs', guest)).toBe('jobs');
  });
  it('sends guests to access for private destinations', () => {
    for (const view of ['applications', ...resumeViews, 'public-resumes', 'account', 'administration', ...recruiterViews, 'shared-profile'])
      expect(resolveWorkspaceView(`#${view}`, guest)).toBe('access');
    expect(resolveWorkspaceView('#jobs', guest)).toBe('jobs');
  });
  it('keeps recruiter links while excluding candidate and admin screens', () => {
    for (const view of [...recruiterViews, 'shared-profile', 'public-resumes', 'account'])
      expect(resolveWorkspaceView(`#${view}`, recruiter)).toBe(view);
    for (const view of ['applications', ...resumeViews, 'administration'])
      expect(resolveWorkspaceView(`#${view}`, recruiter)).toBe('home');
    for (const view of recruiterViews) {
      expect(resolveWorkspaceView(`#${view}`, candidate)).toBe('home');
      expect(resolveWorkspaceView(`#${view}`, admin)).toBe('home');
    }
  });
  it('keeps admin access separate from private and public CVs', () => {
    expect(resolveWorkspaceView('#administration', admin)).toBe('administration');
    for (const view of ['applications', ...resumeViews, 'public-resumes', 'shared-profile'])
      expect(resolveWorkspaceView(`#${view}`, admin)).toBe('home');
  });
  it('isolates deletion-pending accounts regardless of their hash', () => {
    for (const hash of ['#jobs', '#private-resumes', '#administration', '#access'])
      expect(resolveWorkspaceView(hash, { ...candidate, deletionPending: true })).toBe('account');
  });
  it('leaves the sign-in screen after successful authentication', () => {
    expect(resolveWorkspaceView('#access', guest)).toBe('access');
    expect(resolveWorkspaceView('#access', candidate)).toBe('home');
  });
});
