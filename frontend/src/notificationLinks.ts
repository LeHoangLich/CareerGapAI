const guid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const applicationHref=(id:string)=>`#application?id=${encodeURIComponent(id.toLowerCase())}`;
export function applicationIdFromHash(hash:string):string|null {
  if(hash.split('?')[0]!=='#application')return null;
  const id=new URLSearchParams(hash.slice(hash.indexOf('?')+1)).get('id');
  return id&&guid.test(id)?id.toLowerCase():null;
}
