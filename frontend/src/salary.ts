export type Salary = { salaryMin: number | null; salaryMax: number | null; salaryCurrency: string | null; salaryPeriod: string | null };
export const salaryPeriods: Record<string,string> = { hour:'giờ', day:'ngày', month:'tháng', year:'năm', project:'dự án' };
export function salaryLabel(s: Salary): string {
  if(s.salaryMin===null&&s.salaryMax===null)return 'Lương chưa công bố';
  const n=(value:number)=>value.toLocaleString('vi-VN',{maximumFractionDigits:2});
  const amount=s.salaryMin===null?`Đến ${n(s.salaryMax!)}`:s.salaryMax===null?`Từ ${n(s.salaryMin)}`:s.salaryMin===s.salaryMax?n(s.salaryMin):`${n(s.salaryMin)}–${n(s.salaryMax)}`;
  return `${amount} ${s.salaryCurrency??''}${s.salaryPeriod?` / ${salaryPeriods[s.salaryPeriod]??s.salaryPeriod}`:''}`.trim();
}
export function salaryPayload(form:{salaryMin:string;salaryMax:string;salaryCurrency:string;salaryPeriod:string}):Salary {
  const min=form.salaryMin.trim(),max=form.salaryMax.trim();
  if(!min&&!max)return {salaryMin:null,salaryMax:null,salaryCurrency:null,salaryPeriod:null};
  const salaryMin=min?Number(min):null,salaryMax=max?Number(max):null;
  if([salaryMin,salaryMax].some(n=>n!==null&&(!Number.isFinite(n)||n<0||n>9999999999999999.99))||salaryMin!==null&&salaryMax!==null&&salaryMin>salaryMax)
    throw new Error('Lương phải là số không âm; mức tối đa không nhỏ hơn mức tối thiểu.');
  const salaryCurrency=form.salaryCurrency.trim().toUpperCase();
  if(!/^[A-Z]{3}$/.test(salaryCurrency)||!salaryPeriods[form.salaryPeriod])throw new Error('Chọn đơn vị tiền tệ gồm 3 chữ cái và kỳ trả lương hợp lệ.');
  return {salaryMin,salaryMax,salaryCurrency,salaryPeriod:form.salaryPeriod};
}
