import {expect,it} from 'vitest';
import {applicationHref,applicationIdFromHash} from './notificationLinks';
it('opens only a validated local application route',()=>{
  const id='74fd76a6-bbb7-4d51-881f-5ca7caf9029c';
  expect(applicationIdFromHash(applicationHref(id.toUpperCase()))).toBe(id);
  for(const hash of ['#application','#application?id=bad','#application?id=../private',`https://evil.invalid/${applicationHref(id)}`,`#job?id=${id}`])
    expect(applicationIdFromHash(hash)).toBeNull();
});
