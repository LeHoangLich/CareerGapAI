import {expect,it} from 'vitest';
import {communityTagMatch} from './communityTagMatch';
it('labels complete and partial tag matches without counting unselected tags',()=>{expect(communityTagMatch(['java','react','spring'],['java','react'])).toEqual({tags:['java','react'],total:2,percent:100});expect(communityTagMatch(['react','nextjs'],['java','react']).percent).toBe(50)});
it('normalizes and deduplicates selected tags and handles no filter',()=>{expect(communityTagMatch(['react'],[' React ','react','java']).percent).toBe(50);expect(communityTagMatch(['react'],[]).total).toBe(0);expect(communityTagMatch(['react'],['react','java','docker']).percent).toBe(33)});
