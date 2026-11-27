import { useEffect,useId,useState } from 'react';
import { api } from './api';
type Entry={id:string;name:string;aliases:string[]};
let locationsPromise: Promise<Entry[]> | null = null;
function loadLocations(): Promise<Entry[]> {
  if (!locationsPromise) {
    locationsPromise = api<{items?:Entry[]}|Entry[]>('/api/locations')
      .then(x => Array.isArray(x) ? x : Array.isArray(x?.items) ? x.items : [])
      .catch(err => { locationsPromise = null; throw err; });
  }
  return locationsPromise;
}
export function LocationInput({value,onChange,placeholder='Tỉnh, thành phố hoặc địa điểm khác',name='location'}:{value:string;onChange:(value:string)=>void;placeholder?:string;name?:string}){
 const id=useId();const [items,setItems]=useState<Entry[]>([]);
 useEffect(()=>{let disposed=false;void loadLocations().then(x=>{if(!disposed)setItems(x)}).catch(()=>{});return()=>{disposed=true}},[]);
 return <><input name={name} list={id} value={value} maxLength={250} placeholder={placeholder} onChange={e=>onChange(e.target.value)}/><datalist id={id}>{items.map(l=><option value={l.name} key={l.id}>{l.aliases.join(' · ')}</option>)}</datalist></>;
}
