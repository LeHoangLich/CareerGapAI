import { useEffect,useId,useState } from 'react';
import { api } from './api';
type Entry={id:string;name:string;aliases:string[]};
export function LocationInput({value,onChange,placeholder='Tỉnh, thành phố hoặc địa điểm khác'}:{value:string;onChange:(value:string)=>void;placeholder?:string}){
 const id=useId();const [items,setItems]=useState<Entry[]>([]);
 useEffect(()=>{const c=new AbortController();void api<{items?:Entry[]}|Entry[]>('/api/locations',{signal:c.signal}).then(x=>{if(!c.signal.aborted)setItems(Array.isArray(x)?x:Array.isArray(x?.items)?x.items:[])}).catch(()=>{});return()=>c.abort()},[]);
 return <><input list={id} value={value} maxLength={250} placeholder={placeholder} onChange={e=>onChange(e.target.value)}/><datalist id={id}>{items.map(l=><option value={l.name} key={l.id}>{l.aliases.join(' · ')}</option>)}</datalist></>;
}
