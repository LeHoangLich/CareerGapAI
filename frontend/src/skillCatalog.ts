export type CatalogSkill={id:string;name:string;category:string;aliases:string[]};
export const foldSkill=(value:string)=>value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/đ/g,'d').replace(/Đ/g,'D').trim().toLowerCase();
export function hasSkill(values:string[],skill:CatalogSkill){const names=[skill.name,...skill.aliases].map(foldSkill);return values.some(value=>names.includes(foldSkill(value.replace(/^(required|optional|bắt buộc|ưu tiên)\s*:\s*/i,''))))}
