export function communityTagMatch(postTags:string[],selectedTags:string[]){
 const selected=[...new Set(selectedTags.map(t=>t.trim().toLowerCase()).filter(Boolean))];
 const tags=postTags.filter(t=>selected.includes(t.toLowerCase()));
 const count=new Set(tags.map(t=>t.toLowerCase())).size;
 return {tags,total:selected.length,percent:selected.length?Math.round(count/selected.length*100):0};
}
