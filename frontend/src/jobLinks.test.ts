import { describe, expect, it } from 'vitest';
import { jobHref, jobIdFromHash, jobReturnAfterSignIn, jobShareUrl, jobSignInHref } from './jobLinks';

const id = '6ca345b3-74a9-44dd-a413-c16e1413670e';
describe('public job links', () => {
  it('resolves public job links and rejects malformed or unrelated routes', () => {
    expect(jobIdFromHash(jobHref(id.toUpperCase()))).toBe(id);
    for (const hash of ['#job', '#job?id=bad', '#job?id=../private', `#jobs?id=${id}`, '#job?id=%3Cscript%3E'])
      expect(jobIdFromHash(hash)).toBeNull();
  });
  it('shares only the job destination without sensitive page query or hash data', () => {
    expect(jobShareUrl(id, 'https://careergap.example/app/?resetToken=private&v=old#account'))
      .toBe(`https://careergap.example/app/${jobHref(id)}`);
  });
  it('returns after sign-in only to a validated local public job route', () => {
    expect(jobReturnAfterSignIn(jobSignInHref(id))).toBe(jobHref(id));
    for (const target of ['https://evil.invalid', '//evil.invalid', '#administration', '#job?id=bad', `https://evil.invalid/${jobHref(id)}`])
      expect(jobReturnAfterSignIn(`#access?returnTo=${encodeURIComponent(target)}`)).toBeNull();
    expect(jobReturnAfterSignIn('#access')).toBeNull();
    expect(jobReturnAfterSignIn(`#jobs?returnTo=${encodeURIComponent(jobHref(id))}`)).toBeNull();
  });
});
