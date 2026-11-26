import { useState } from 'react';
import { api } from './api';
export function CompanyLogo({name,url}:{name:string;url?:string|null}) {
  const [failed,setFailed]=useState<string>();
  return url&&failed!==url?<img className="company-logo" src={url} alt={`Logo ${name}`} loading="lazy" onError={()=>setFailed(url)}/>:<span className="company-logo company-logo-fallback" aria-label={`Công ty ${name}`}>{name.slice(0,2).toLocaleUpperCase('vi')}</span>;
}
export function CompanyLogoEditor({company,onUpdated}:{company:{id:string;name:string;logoUrl?:string|null;rowVersion:number};onUpdated:()=>Promise<void>}) {
  const [file,setFile]=useState<File>();const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [message,setMessage]=useState('');
  async function save(remove=false){
    if(!remove&&(!file||file.size>2*1024*1024||!['image/png','image/jpeg'].includes(file.type))){setError('Chọn ảnh PNG hoặc JPEG không quá 2 MB.');return;}
    setBusy(true);setError('');setMessage('');let uploaded:string|undefined;
    try{
      if(!remove){const form=new FormData();form.append('file',file!);uploaded=(await api<{id:string}>('/api/community/images',{method:'POST',body:form})).id;}
      await api(`/api/companies/${company.id}/logo`,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({imageId:uploaded??null,rowVersion:company.rowVersion})});
      setFile(undefined);await onUpdated();setMessage(remove?'Đã gỡ logo.':'Đã lưu logo doanh nghiệp.');
    }catch(e){setError((e as Error).message+' Hãy tải lại danh sách công ty trước khi thử lại.');if(uploaded)await api(`/api/community/images/${uploaded}`,{method:'DELETE'}).catch(()=>{});}
    finally{setBusy(false)}
  }
  return <section className="company-logo-editor" aria-label={`Logo doanh nghiệp ${company.name}`}><CompanyLogo name={company.name} url={company.logoUrl}/><label>Chọn logo cho {company.name}<input type="file" accept="image/png,image/jpeg" disabled={busy} onChange={e=>{setFile(e.target.files?.[0]);setError('');setMessage('')}}/></label><p>PNG/JPEG, tối đa 2 MB. Nên dùng ảnh vuông; logo hiển thị công khai khi công ty có tin công khai.</p><div className="row"><button type="button" className="secondary" disabled={busy||!file} onClick={()=>void save()}>{busy?'Đang lưu logo…':'Tải lên và lưu logo'}</button><button type="button" className="secondary" disabled={busy||!company.logoUrl} onClick={()=>void save(true)}>Gỡ logo</button></div>{error&&<p role="alert">{error}</p>}{message&&<p role="status">{message}</p>}</section>;
}
