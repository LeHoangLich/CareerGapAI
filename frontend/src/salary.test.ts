import {expect,it} from 'vitest';
import {salaryLabel,salaryPayload} from './salary';
it('preserves disclosed salary including zero, USD fractions and open ranges',()=>{
  const form={salaryMin:'1200.25',salaryMax:'2400.5',salaryCurrency:'USD',salaryPeriod:'month'};
  expect(salaryPayload(form)).toEqual({salaryMin:1200.25,salaryMax:2400.5,salaryCurrency:'USD',salaryPeriod:'month'});
  expect(salaryPayload({...form,salaryMin:'0',salaryMax:''}).salaryMin).toBe(0);
  expect(salaryLabel(salaryPayload(form))).toBe('1.200,25–2.400,5 USD / tháng');
  expect(salaryLabel(salaryPayload({...form,salaryMin:''}))).toBe('Đến 2.400,5 USD / tháng');
});
it('clears currency and period only when both amounts are empty and rejects invalid ranges',()=>{
  const form={salaryMin:'',salaryMax:'',salaryCurrency:'VND',salaryPeriod:'month'};
  expect(salaryPayload(form)).toEqual({salaryMin:null,salaryMax:null,salaryCurrency:null,salaryPeriod:null});
  expect(salaryLabel(salaryPayload(form))).toBe('Lương chưa công bố');
  expect(()=>salaryPayload({...form,salaryMin:'20',salaryMax:'10'})).toThrow();
  expect(()=>salaryPayload({...form,salaryMin:'NaN'})).toThrow();
  expect(()=>salaryPayload({...form,salaryMin:'10',salaryCurrency:'V'})).toThrow();
});
