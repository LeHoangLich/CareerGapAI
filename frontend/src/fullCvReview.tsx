import { useEffect, useState } from 'react';
import { api, errorMessage } from './api';
import { useViewActive } from './persistentView';
import { AiBadge } from './ui';
import { useAiJobPolling } from './useAiJobPolling';

type Field = { path: string; value: string };
type Evidence = { path: string; sourceType: string; segmentId: string | null; quote: string | null };
type Revision = { id: string; revisionNo: number; schemaVersion: number; origin: string; structuredData: { fields: Field[] }; evidence: Evidence[] };
const scalars: Record<string, string> = { displayName: 'Họ tên', email: 'Email', phone: 'Điện thoại', location: 'Địa điểm', headline: 'Tiêu đề nghề nghiệp', summary: 'Giới thiệu' };
const groups: Record<string, Record<string, string>> = {
  experience: { role: 'Chức danh', organization: 'Đơn vị', period: 'Thời gian', description: 'Mô tả' },
  projects: { name: 'Tên', context: 'Bối cảnh/vai trò', description: 'Mô tả' },
  education: { program: 'Chương trình', institution: 'Trường', period: 'Thời gian', details: 'Chi tiết' }
};
const groupLabels: Record<string, string> = { skills: 'Kỹ năng', certifications: 'Chứng chỉ', experience: 'Kinh nghiệm', projects: 'Dự án', education: 'Học vấn' };
export function fieldLabel(path: string) {
  if (scalars[path]) return scalars[path];
  const match = /^(\w+)\[(\d+)\](?:\.(\w+))?$/.exec(path);
  return match ? `${groupLabels[match[1]]} ${Number(match[2]) + 1}${match[3] ? ` · ${groups[match[1]][match[3]]}` : ''}` : path;
}
const pathGroups: { label: string; paths: string[] }[] = [
  { label: 'Thông tin chung', paths: Object.keys(scalars) },
  { label: 'Kỹ năng', paths: Array.from({ length: 20 }, (_, i) => `skills[${i}]`) },
  { label: 'Chứng chỉ', paths: Array.from({ length: 20 }, (_, i) => `certifications[${i}]`) },
  ...Object.entries(groups).map(([group, fields]) => ({
    label: groupLabels[group],
    paths: Array.from({ length: 10 }, (_, i) => Object.keys(fields).map(field => `${group}[${i}].${field}`)).flat(),
  })),
];

export function FullCvReview({ versionId, refreshKey, parentPolling = false }: { versionId: string; refreshKey: string; parentPolling?: boolean }) {
  const active = useViewActive();
  const [revision, setRevision] = useState<Revision>();
  const [fields, setFields] = useState<Field[]>([]);
  const [path, setPath] = useState('');
  const [value, setValue] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  useEffect(() => {
    setRevision(undefined); setFields([]); setError(''); setMessage('');
  }, [versionId, refreshKey]);
  const { timedOut, retryPolling } = useAiJobPolling({
    pollKey: versionId && !parentPolling ? `${versionId}:${refreshKey}` : undefined,
    active,
    maxAttempts: 60,
    intervalMs: 2000,
    tick: async signal => {
      const { job: status } = await api<{ job: { status: string; errorCode?: string } | null }>(`/api/resume-versions/${versionId}/extraction-status`, { signal });
      if (signal.aborted) return false;
      if (status && ['queued', 'running'].includes(status.status)) {
        setMessage('Đang đọc CV đầy đủ; có thể tải lại trang mà không tạo tác vụ mới.');
        return false;
      }
      if (status?.status === 'failed') setError(errorMessage(status.errorCode ?? '', 422));
      const items = await api<Revision[]>(`/api/resume-versions/${versionId}/revisions`, { signal });
      if (signal.aborted) return false;
      const latest = items[0];
      setMessage('');
      if (latest?.schemaVersion === 3) {
        setRevision(latest);
        setFields(latest.structuredData.fields);
      }
      return true;
    },
    onError: e => setError((e as Error).message),
    timeoutMessage: 'Tác vụ vẫn đang xử lý; hãy chọn lại phiên bản để kiểm tra sau.',
  });
  const displayMessage = parentPolling ? 'Đang đọc CV đầy đủ; có thể tải lại trang mà không tạo tác vụ mới.' : message;
  if (!revision) return <>{displayMessage && <p role="status">{displayMessage}</p>}{error && <p role="alert">{error}{timedOut && <> <button type="button" className="secondary" onClick={() => { setError(''); retryPolling(); }}>Kiểm tra lại</button></>}</p>}</>;
  return <section className="card full-cv-review"><h2>Rà soát toàn bộ dữ liệu CV</h2>
    <AiBadge accepted={revision.origin !== 'ai_extracted'}/>
    <p>{revision.origin === 'ai_extracted' ? 'Chờ bạn xác nhận' : 'Bạn đã xác nhận'} · Phiên bản rà soát {revision.revisionNo}. Trường thiếu được để trống. Nội dung sửa hoặc thêm là thông tin bạn tự xác nhận.</p>
    {fields.map(field => {
      const original = revision.structuredData.fields.find(item => item.path === field.path);
      const evidence = revision.evidence.find(item => item.path === field.path);
      const documentSource = original?.value === field.value && evidence?.sourceType === 'document_span';
      return <fieldset key={field.path}><legend>{fieldLabel(field.path)}</legend>
        <label>{fieldLabel(field.path)}<textarea aria-label={fieldLabel(field.path)} value={field.value} maxLength={3000} onChange={e => { const next = e.target.value; setFields(current => current.map(item => item.path === field.path ? { ...item, value: next } : item)); }}/></label>
        {documentSource ? <p>Trích dẫn từ CV: “{evidence.quote}”</p> : <p>Bạn tự xác nhận; không phải trích dẫn từ CV.</p>}
        <button type="button" className="secondary" onClick={() => setFields(current => current.filter(item => item.path !== field.path))}>Bỏ {fieldLabel(field.path)}</button>
      </fieldset>;
    })}
    <div><label>Trường cần bổ sung<select value={path} onChange={e => setPath(e.target.value)}><option value="">Chọn trường cần bổ sung</option>{pathGroups.map(group => {
      const available = group.paths.filter(item => fields.every(field => field.path !== item));
      if (!available.length) return null;
      return <optgroup key={group.label} label={group.label}>{available.map(item => <option key={item} value={item}>{fieldLabel(item)}</option>)}</optgroup>;
    })}</select></label>
      <label>Nội dung tự bổ sung<textarea value={value} maxLength={3000} onChange={e => setValue(e.target.value)}/></label>
      <button type="button" disabled={!path || !value.trim() || fields.some(field => field.path === path)} onClick={() => { setFields(current => [...current, { path, value: value.trim() }]); setValue(''); setPath(''); }}>Thêm thông tin</button></div>
    <button type="button" disabled={busy} onClick={() => { setBusy(true); setError(''); void api<Revision>(`/api/resume-versions/${versionId}/revisions/${revision.id}/confirm-full`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ baseRevisionNo: revision.revisionNo, fields: fields.filter(field => field.value.trim()).map(field => ({ ...field, value: field.value.trim() })) })
    }).then(saved => { setRevision(saved); setFields(saved.structuredData.fields); setMessage('Đã lưu bản xác nhận mới; dữ liệu trích xuất ban đầu được giữ nguyên.'); }).catch(e => setError((e as Error).message)).finally(() => setBusy(false)); }}>Xác nhận toàn bộ dữ liệu CV</button>
    {error && <p role="alert">{error}</p>}{message && <p role="status">{message}</p>}
  </section>;
}
