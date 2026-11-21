export function communityFeedParams(hash=location.hash){return new URLSearchParams(hash.split('?')[1]??'');}
export function communityFilterTags(p:URLSearchParams){return [...new Set([...(p.get('tags')?.split(',')??[]),p.get('tag')??''].map(t=>t.trim().toLowerCase()).filter(Boolean))];}
export function communityFeedPage(p:URLSearchParams){const n=Number(p.get('page')??1);return Number.isInteger(n)&&n>=1&&n<=10000?n:1;}
type Patch={q?:string;category?:string;sort?:string;tags?:string[];page?:number};
export function communityFeedHref(p:URLSearchParams,patch:Patch){
 const next=new URLSearchParams(p);next.delete('post');
 for(const key of ['q','category','sort'] as const)if(patch[key]!==undefined){if(patch[key])next.set(key,patch[key]);else next.delete(key);}
 if(patch.tags!==undefined){next.delete('tag');if(patch.tags.length)next.set('tags',patch.tags.join(','));else next.delete('tags');}
 if(patch.page!==undefined){if(patch.page>1)next.set('page',String(patch.page));else next.delete('page');}
 return '#community'+(next.size?'?'+next:'');
}
export function communityTagHref(tag:string,hash=location.hash){let p=communityFeedParams(hash);if(['moderation','notifications'].includes(p.get('tab')??''))p=new URLSearchParams();const tags=communityFilterTags(p),normalized=tag.trim().toLowerCase();if(!tags.includes(normalized)&&tags.length<8)tags.push(normalized);return communityFeedHref(p,{tags,page:1});}
