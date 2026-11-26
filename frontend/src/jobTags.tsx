import { SkillPicker } from './skillPicker';
export function JobTagPicker({label,tags,onChange,disabled=false}:{label:string;tags:string[];onChange:(tags:string[])=>void;disabled?:boolean}){
 return <section aria-label={"Mục tag: "+label}><p>Chọn tối đa 10 tag từ danh mục CareerGap.</p><div className="row job-tags">{tags.map(tag=><button type="button" className="secondary" key={tag} disabled={disabled} aria-label={`Bỏ tag ${tag}`} onClick={()=>onChange(tags.filter(t=>t!==tag))}>{tag} ×</button>)}</div><SkillPicker label={label} values={tags} max={10} disabled={disabled} onAdd={name=>onChange([...tags,name])}/></section>;
}
