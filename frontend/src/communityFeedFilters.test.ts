import {describe,it,expect} from 'vitest';
import {communityFeedParams,communityFilterTags,communityFeedPage,communityFeedHref,communityTagHref} from './communityFeedFilters';
describe('community feed filter URLs',()=>{
 it('combines legacy and new tag names without losing punctuation or Vietnamese',()=>{
  const p=communityFeedParams('#community?tag=React&tags=react,%20Docker%20,C%23,.net,k%E1%BB%B9%20n%C4%83ng');expect(communityFilterTags(p)).toEqual(['react','docker','c#','.net','kỹ năng']);
 });
 it('adds tags in current author/feed context and resets pagination',()=>{
  const href=communityTagHref('Docker','#community?tab=following&person=user&q=React&category=technical&sort=views&tag=react&page=3');const p=communityFeedParams(href);expect(p.get('tab')).toBe('following');expect(p.get('person')).toBe('user');expect(p.get('q')).toBe('React');expect(p.get('category')).toBe('technical');expect(p.get('sort')).toBe('views');expect(p.has('tag')).toBe(false);expect(communityFilterTags(p)).toEqual(['react','docker']);expect(communityFeedPage(p)).toBe(1);
 });
 it('clears tags without discarding unrelated filters or author',()=>{
  const href=communityFeedHref(new URLSearchParams('person=author&tag=react&tags=docker&q=sample&category=career'),{tags:[],page:1});const p=communityFeedParams(href);expect(p.has('tag')).toBe(false);expect(p.has('tags')).toBe(false);expect(p.get('person')).toBe('author');expect(p.get('q')).toBe('sample');expect(p.get('category')).toBe('career');
 });
 it('links from moderation into the public feed and never exceeds eight tags',()=>{
  expect(communityTagHref('react','#community?tab=moderation&review=reports&q=private')).toBe('#community?tags=react');const p=communityFeedParams(communityTagHref('extra','#community?tags=a,b,c,d,e,f,g,h'));expect(communityFilterTags(p)).toHaveLength(8);
 });
 it('encodes hash and plus literally and validates URL page bounds',()=>{
  const href=communityTagHref('C++','#community?post=old');const p=communityFeedParams(href);expect(p.has('post')).toBe(false);expect(p.get('tags')).toBe('c++');expect(communityFeedPage(new URLSearchParams('page=0'))).toBe(1);expect(communityFeedPage(new URLSearchParams('page=3'))).toBe(3);expect(communityFeedPage(new URLSearchParams('page=NaN'))).toBe(1);
 });
});
