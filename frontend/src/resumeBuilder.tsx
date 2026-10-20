import { pdf } from '@react-pdf/renderer';
import { useEffect, useRef, useState } from 'react';
import { api } from './api';
import { ResumeBuilderPdf, type BuilderEducation, type BuilderExperience, type BuilderProject, type ResumeBuilderContent } from './resumeBuilderPdf';

type ResumeVersion = { id: string; fileId: string; versionNo: number; documentHash: string; createdAt: string };
type Resume = { id: string; title: string; rowVersion: number; createdAt: string; updatedAt: string; versions: ResumeVersion[] };
type Revision = { id: string; revisionNo: number; origin: string; schemaVersion: number; structuredData: unknown; dataHash: string };
type Props = { ownerId: string; resumes: Resume[]; onResumesUpdated: (resumes: Resume[]) => void };
type PendingUpload = { contentKey: string; resumeId: string; versionId: string };

function useObjectUrl(blob: Blob | undefined) {
  const [url, setUrl] = useState('');
  useEffect(() => {
    if (!blob) { setUrl(''); return; }
    const next = URL.createObjectURL(blob);
    setUrl(next);
    return () => URL.revokeObjectURL(next);
  }, [blob]);
  return url;
}

const emptyExperience = (): BuilderExperience => ({ role: '', organization: '', period: '', description: '' });
const emptyEducation = (): BuilderEducation => ({ program: '', institution: '', period: '', details: '' });
const emptyProject = (): BuilderProject => ({ name: '', context: '', description: '' });
const emptyContent = (): ResumeBuilderContent => ({ schemaVersion: 2, templateVersion: 'simple-v1', displayName: '', email: '', phone: '', location: '', headline: '', summary: '', skills: [], experience: [], education: [], projects: [], certifications: [] });

function value(object: Record<string, unknown>, key: string): string {
  const field = object[key];
  return typeof field === 'string' ? field : '';
}

function fromRevision(input: unknown): ResumeBuilderContent | undefined {
  if (!input || typeof input !== 'object') return undefined;
  const raw = input as Record<string, unknown>;
  if (raw.schemaVersion !== 2 || raw.templateVersion !== 'simple-v1') return undefined;
  const list = <T,>(key: string, map: (entry: Record<string, unknown>) => T): T[] =>
    Array.isArray(raw[key]) ? (raw[key] as unknown[]).filter(entry => !!entry && typeof entry === 'object').map(entry => map(entry as Record<string, unknown>)) : [];
  return {
    ...emptyContent(), displayName: value(raw, 'displayName'), email: value(raw, 'email'), phone: value(raw, 'phone'),
    location: value(raw, 'location'), headline: value(raw, 'headline'), summary: value(raw, 'summary'),
    skills: Array.isArray(raw.skills) ? raw.skills.filter((item): item is string => typeof item === 'string') : [],
    experience: list('experience', item => ({ role: value(item, 'role'), organization: value(item, 'organization'), period: value(item, 'period'), description: value(item, 'description') })),
    education: list('education', item => ({ program: value(item, 'program'), institution: value(item, 'institution'), period: value(item, 'period'), details: value(item, 'details') })),
    projects: list('projects', item => ({ name: value(item, 'name'), context: value(item, 'context'), description: value(item, 'description') })),
    certifications: Array.isArray(raw.certifications) ? raw.certifications.filter((item): item is string => typeof item === 'string') : []
  };
}

function normalized(content: ResumeBuilderContent): ResumeBuilderContent {
  const unique = (items: string[]) => [...new Map(items.map(item => item.trim()).filter(Boolean).map(item => [item.toLocaleLowerCase('vi-VN'), item.trim()])).values()];
  return { ...content, skills: unique(content.skills), certifications: unique(content.certifications) };
}

function validate(content: ResumeBuilderContent): string | undefined {
  const data = normalized(content);
  if (!data.displayName.trim()) return 'Hãy nhập tên hiển thị trên CV.';
  if (!data.skills.length && !data.experience.length && !data.education.length && !data.projects.length && !data.headline.trim() && !data.summary.trim())
    return 'Hãy nhập ít nhất một nội dung nghề nghiệp trước khi xuất CV.';
  if (data.experience.some(item => !item.role.trim() || !item.organization.trim() || !item.description.trim()))
    return 'Mỗi kinh nghiệm đã bắt đầu cần có chức danh, đơn vị và mô tả.';
  if (data.education.some(item => !item.program.trim() || !item.institution.trim()))
    return 'Mỗi học vấn đã bắt đầu cần có chương trình và cơ sở đào tạo.';
  if (data.projects.some(item => !item.name.trim() || !item.description.trim()))
    return 'Mỗi dự án đã bắt đầu cần có tên và mô tả.';
  if (data.skills.length > 20 || data.experience.length > 10 || data.education.length > 10 || data.projects.length > 10 || data.certifications.length > 20)
    return 'Số mục đã vượt giới hạn của bản CV thử nghiệm.';
  return undefined;
}

function parseLines(text: string) { return [...new Set(text.split(/\r?\n/).map(item => item.trim()).filter(Boolean))]; }
function safeFileName(title: string) {
  const stem = title.trim().replace(/[<>:"/\\|?*\x00-\x1f]/g, '-').replace(/\s+/g, '-').slice(0, 100) || 'cv-cua-toi';
  return `${stem}.pdf`;
}
async function sha256(blob: Blob) {
  const digest = await crypto.subtle.digest('SHA-256', await blob.arrayBuffer());
  return [...new Uint8Array(digest)].map(byte => byte.toString(16).padStart(2, '0')).join('');
}

export function ResumeBuilderPanel({ ownerId, resumes, onResumesUpdated }: Props) {
  const [title, setTitle] = useState('CV của tôi');
  const [targetResumeId, setTargetResumeId] = useState('');
  const [sourceVersionId, setSourceVersionId] = useState('');
  const [content, setContent] = useState(emptyContent);
  const [previewBlob, setPreviewBlob] = useState<Blob>();
  const [previewKey, setPreviewKey] = useState('');
  const [savedKey, setSavedKey] = useState('');
  const [pendingUpload, setPendingUpload] = useState<PendingUpload>();
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loadingRevision, setLoadingRevision] = useState(false);
  const recoveryKey = `careergap:builder-recovery:${ownerId}`;
  useEffect(() => {
    try {
      const stored = sessionStorage.getItem(recoveryKey);
      if (!stored) return;
      const pending = JSON.parse(stored) as PendingUpload;
      const loaded = fromRevision(JSON.parse(pending.contentKey));
      if (!loaded || !pending.resumeId || !pending.versionId) return;
      setPendingUpload(pending); setContent(loaded); setTargetResumeId(pending.resumeId); setSourceVersionId(pending.versionId);
      setMessage('Có bản lưu dang dở trong phiên trình duyệt. Hoàn tất để lưu dữ liệu vào đúng file đã tải, không tạo file trùng.');
    } catch { setError('Không đọc được bản lưu tạm; hãy nạp lại CV đã lưu hoặc bỏ bản nháp tạm.'); }
  }, [recoveryKey]);
  const generation = useRef(0);
  const contentKey = JSON.stringify(normalized(content));
  const targetResume = resumes.find(item => item.id === targetResumeId);
  const previewUrl = useObjectUrl(previewBlob);

  function edit(update: (current: ResumeBuilderContent) => ResumeBuilderContent) {
    if (pendingUpload) { setError('Hãy hoàn tất hoặc bỏ bản lưu dang dở trước khi sửa nội dung.'); return; }
    setContent(current => update(current));
    setPreviewBlob(undefined); setPreviewKey(''); setSavedKey(''); setPendingUpload(undefined); setMessage(''); setError('');
  }

  function changeTarget(id: string) {
    if (pendingUpload) { setError('Hãy hoàn tất hoặc bỏ bản lưu dang dở trước khi chọn CV khác.'); return; }
    setTargetResumeId(id);
    const selected = resumes.find(item => item.id === id);
    setSourceVersionId(selected?.versions[0]?.id ?? '');
    setTitle(selected?.title ?? 'CV của tôi');
    setContent(emptyContent()); setPreviewBlob(undefined); setPreviewKey(''); setSavedKey(''); setPendingUpload(undefined);
    setMessage(id ? 'Chọn phiên bản bên dưới rồi nạp revision mẫu để chỉnh sửa.' : 'CV mới sẽ tạo một hồ sơ riêng tư.');
  }

  async function loadRevision() {
    if (!sourceVersionId) return;
    setBusy(true); setLoadingRevision(true); setError('');
    try {
      const revisions = await api<Revision[]>(`/api/resume-versions/${sourceVersionId}/revisions`);
      const saved = revisions.find(item => item.origin === 'user_entered' && item.schemaVersion === 2);
      const loaded = saved ? fromRevision(saved.structuredData) : fromRevision(await api<unknown>(`/api/resume-versions/${sourceVersionId}/builder-content`).catch(() => undefined));
      if (!loaded) {
        setContent(emptyContent()); setSavedKey('');
        setMessage('Phiên bản này chưa có dữ liệu từ trình tạo CV; bạn có thể nhập nội dung để tạo phiên bản kế tiếp.');
      } else {
        setContent(loaded); setSavedKey('');
        setMessage(`Đã nạp dữ liệu mẫu từ revision ${saved?.revisionNo ?? 'đã xác nhận'}. Lưu thay đổi sẽ tạo file version mới; bản cũ được giữ nguyên.`);
      }
      setPreviewBlob(undefined); setPreviewKey(''); setPendingUpload(undefined);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); setLoadingRevision(false); }
  }

  async function createPreview() {
    const data = normalized(content);
    const validation = validate(data);
    if (validation) { setError(validation); return; }
    const key = JSON.stringify(data);
    const run = ++generation.current;
    setBusy(true); setError(''); setMessage('');
    try {
      const blob = await pdf(<ResumeBuilderPdf content={data}/>).toBlob();
      if (run !== generation.current || JSON.stringify(normalized(content)) !== key) return;
      setPreviewBlob(blob); setPreviewKey(key); setMessage('Bản xem trước đã được tạo. Hãy kiểm tra dấu tiếng Việt và bố cục trước khi lưu.');
    } catch { setError('Chưa dựng được PDF. Hãy thử lại sau.'); }
    finally { if (run === generation.current) setBusy(false); }
  }

  async function saveVersion() {
    const data = normalized(content);
    const validation = validate(data);
    if (validation) { setError(validation); return; }
    const key = JSON.stringify(data);
    if (!previewBlob || previewKey !== key) { setError('Hãy tạo và xem lại bản PDF mới nhất trước khi lưu.'); return; }
    if (savedKey === key) return;
    setBusy(true); setError(''); setMessage('');
    try {
      let uploaded: { resumeId: string; versionId: string };
      if (pendingUpload?.contentKey === key) uploaded = pendingUpload;
      else {
        const hash = await sha256(previewBlob);
        const previousVersions = new Set(resumes.flatMap(resume => resume.versions.map(version => version.id)));
        const form = new FormData();
        form.append('file', new File([previewBlob], safeFileName(title), { type: 'application/pdf' }));
        const uploadPath = targetResumeId ? `/api/resumes/${targetResumeId}/versions` : '/api/resumes';
        try {
          const created = await api<{ id: string; versionId: string }>(uploadPath, { method: 'POST', body: form });
          uploaded = { resumeId: created.id, versionId: created.versionId };
        } catch (uploadError) {
          // Recover a successful upload whose response was lost; new versions sort to the first resume page.
          const latest = await api<Resume[]>('/api/resumes?page=1').catch(() => []);
          const matches = latest.filter(resume => !targetResumeId || resume.id === targetResumeId).flatMap(resume => resume.versions
            .filter(version => !previousVersions.has(version.id) && version.documentHash.toLowerCase() === hash)
            .map(version => ({ resumeId: resume.id, versionId: version.id })));
          if (matches.length !== 1) throw uploadError;
          uploaded = matches[0];
        }
        const recovery = { contentKey: key, ...uploaded };
        sessionStorage.setItem(recoveryKey, JSON.stringify(recovery));
        setPendingUpload(recovery);
      }
      const revision = await api<Revision>(`/api/resume-versions/${uploaded.versionId}/revisions/manual`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ baseRevisionNo: 0, ...data })
      });
      const fresh = await api<Resume[]>('/api/resumes?page=1');
      onResumesUpdated(fresh);
      setTargetResumeId(uploaded.resumeId); setSourceVersionId(uploaded.versionId);
      window.dispatchEvent(new Event('resume-list-updated'));
      setSavedKey(key); setPendingUpload(undefined); sessionStorage.removeItem(recoveryKey);
      setMessage(`Đã lưu CV riêng tư, phiên bản mới và revision ${revision.revisionNo}. Các bản trước vẫn được giữ nguyên.`);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }

  async function completePending() {
    if (!pendingUpload) return;
    setBusy(true); setError('');
    try {
      const data = fromRevision(JSON.parse(pendingUpload.contentKey));
      if (!data) throw new Error('Dữ liệu tạm không hợp lệ.');
      const revision = await api<Revision>(`/api/resume-versions/${pendingUpload.versionId}/revisions/manual`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ baseRevisionNo: 0, ...data })
      });
      onResumesUpdated(await api<Resume[]>('/api/resumes?page=1'));
      sessionStorage.removeItem(recoveryKey); setPendingUpload(undefined); setSavedKey(pendingUpload.contentKey);
      setMessage(`Đã hoàn tất bản lưu dang dở, revision ${revision.revisionNo}; không tải thêm file.`);
      window.dispatchEvent(new Event('resume-list-updated'));
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  const updateListItem = <T,>(list: T[], index: number, update: Partial<T>) => list.map((item, i) => i === index ? { ...item, ...update } : item);
  const addItem = <T,>(list: T[], blank: () => T, max: number) => list.length < max ? [...list, blank()] : list;
  const dropItem = <T,>(list: T[], index: number) => list.filter((_, i) => i !== index);

  return <section className="card resume-builder">
    <div className="section-heading"><div><p className="eyebrow">CV THEO MẪU · BẢN THỬ NGHIỆM</p><h2>Tạo CV từ thông tin bạn cung cấp</h2></div><span className="job-status">simple-v1</span></div>
    <p>Nhập và rà soát nội dung trước khi xuất PDF. Không có AI tự viết hoặc bổ sung dữ kiện trong luồng này. Lưu sẽ tạo file PDF riêng tư và revision bất biến gắn với đúng version.</p>
    <p>Bản lưu dang dở được giữ tạm trong phiên trình duyệt của tài khoản này và xóa sau khi hoàn tất, bỏ bản nháp hoặc đăng xuất.</p>
    {pendingUpload && <div role="status"><p>File đã được tải riêng tư; phần dữ liệu cần hoàn tất.</p><button disabled={busy} onClick={() => void completePending()}>Hoàn tất bản lưu dang dở</button><button disabled={busy} className="secondary" onClick={() => { sessionStorage.removeItem(recoveryKey); setPendingUpload(undefined); setMessage('Đã bỏ bản nháp tạm. File đã tải vẫn ở CV riêng của bạn.'); }}>Bỏ bản nháp tạm</button></div>}
    <div className="builder-source-row">
      <label>CV cần cập nhật<select value={targetResumeId} onChange={e => changeTarget(e.target.value)}><option value="">Tạo CV mới</option>{resumes.map(resume => <option key={resume.id} value={resume.id}>{resume.title}</option>)}</select></label>
      {targetResume && <><label>Version dùng để nạp dữ liệu<select value={sourceVersionId} onChange={e => setSourceVersionId(e.target.value)}>{targetResume.versions.map(version => <option key={version.id} value={version.id}>Version {version.versionNo}</option>)}</select></label><button className="secondary" disabled={!sourceVersionId || busy} onClick={() => void loadRevision()}>{loadingRevision ? 'Đang nạp…' : 'Nạp revision mẫu'}</button></>}
      <label>Tên CV<input value={title} maxLength={100} onChange={e => { setTitle(e.target.value); setPendingUpload(undefined); }}/></label>
    </div>
    <form className="builder-form" onSubmit={e => { e.preventDefault(); void createPreview(); }}>
      <h3>Thông tin cá nhân</h3>
      <div className="form-columns">
        <label>Tên hiển thị *<input value={content.displayName} maxLength={150} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, displayName: value })); }}/></label>
        <label>Tiêu đề nghề nghiệp<input value={content.headline} maxLength={200} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, headline: value })); }}/></label>
        <label>Email liên hệ<input type="email" value={content.email} maxLength={254} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, email: value })); }}/></label>
        <label>Số điện thoại<input value={content.phone} maxLength={50} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, phone: value })); }}/></label>
        <label>Địa điểm<input value={content.location} maxLength={150} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, location: value })); }}/></label>
      </div>
      <label>Giới thiệu<textarea value={content.summary} maxLength={3000} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, summary: value })); }}/></label>

      <div className="builder-section-heading"><h3>Kỹ năng</h3><small>Tối đa 20 dòng; đây là thông tin bạn tự khai báo.</small></div>
      <label>Kỹ năng, mỗi dòng một mục<textarea value={content.skills.join('\n')} maxLength={2200} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, skills: parseLines(value) })); }}/></label>

      <div className="builder-section-heading"><h3>Kinh nghiệm</h3><button type="button" className="secondary" disabled={busy || content.experience.length >= 10} onClick={() => edit(current => ({ ...current, experience: addItem(current.experience, emptyExperience, 10) }))}>Thêm kinh nghiệm</button></div>
      {content.experience.map((item, index) => <fieldset className="builder-entry" key={`experience-${index}`}><legend>Kinh nghiệm {index + 1}</legend><button type="button" className="link-button" onClick={() => edit(current => ({ ...current, experience: dropItem(current.experience, index) }))}>Xóa mục</button><div className="form-columns">
        <label>Chức danh<input maxLength={160} value={item.role} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, experience: updateListItem(current.experience, index, { role: value }) })); }}/></label>
        <label>Đơn vị<input maxLength={160} value={item.organization} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, experience: updateListItem(current.experience, index, { organization: value }) })); }}/></label>
        <label>Thời gian<input maxLength={100} value={item.period} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, experience: updateListItem(current.experience, index, { period: value }) })); }}/></label>
      </div><label>Mô tả<textarea maxLength={1200} value={item.description} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, experience: updateListItem(current.experience, index, { description: value }) })); }}/></label></fieldset>)}

      <div className="builder-section-heading"><h3>Dự án</h3><button type="button" className="secondary" disabled={busy || content.projects.length >= 10} onClick={() => edit(current => ({ ...current, projects: addItem(current.projects, emptyProject, 10) }))}>Thêm dự án</button></div>
      {content.projects.map((item, index) => <fieldset className="builder-entry" key={`project-${index}`}><legend>Dự án {index + 1}</legend><button type="button" className="link-button" onClick={() => edit(current => ({ ...current, projects: dropItem(current.projects, index) }))}>Xóa mục</button><div className="form-columns">
        <label>Tên dự án<input maxLength={180} value={item.name} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, projects: updateListItem(current.projects, index, { name: value }) })); }}/></label>
        <label>Bối cảnh/vai trò<input maxLength={160} value={item.context} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, projects: updateListItem(current.projects, index, { context: value }) })); }}/></label>
      </div><label>Mô tả<textarea maxLength={1200} value={item.description} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, projects: updateListItem(current.projects, index, { description: value }) })); }}/></label></fieldset>)}

      <div className="builder-section-heading"><h3>Học vấn</h3><button type="button" className="secondary" disabled={busy || content.education.length >= 10} onClick={() => edit(current => ({ ...current, education: addItem(current.education, emptyEducation, 10) }))}>Thêm học vấn</button></div>
      {content.education.map((item, index) => <fieldset className="builder-entry" key={`education-${index}`}><legend>Học vấn {index + 1}</legend><button type="button" className="link-button" onClick={() => edit(current => ({ ...current, education: dropItem(current.education, index) }))}>Xóa mục</button><div className="form-columns">
        <label>Chương trình/ngành<input maxLength={180} value={item.program} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, education: updateListItem(current.education, index, { program: value }) })); }}/></label>
        <label>Trường/cơ sở đào tạo<input maxLength={180} value={item.institution} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, education: updateListItem(current.education, index, { institution: value }) })); }}/></label>
        <label>Thời gian<input maxLength={100} value={item.period} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, education: updateListItem(current.education, index, { period: value }) })); }}/></label>
      </div><label>Chi tiết<textarea maxLength={600} value={item.details} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, education: updateListItem(current.education, index, { details: value }) })); }}/></label></fieldset>)}

      <h3>Chứng chỉ</h3><label>Chứng chỉ, mỗi dòng một mục<textarea value={content.certifications.join('\n')} maxLength={4200} onChange={e => { const value = e.currentTarget.value; edit(current => ({ ...current, certifications: parseLines(value) })); }}/></label>
      <div className="row builder-actions"><button disabled={busy}>{busy ? 'Đang tạo PDF…' : 'Tạo bản xem trước PDF'}</button>{previewUrl && previewKey === contentKey && <a className="secondary download-link" href={previewUrl} download={safeFileName(title)}>Tải PDF bản xem trước</a>}<button type="button" className="secondary" disabled={busy || previewKey !== contentKey || savedKey === contentKey} onClick={() => void saveVersion()}>{busy ? 'Đang lưu…' : 'Lưu thành phiên bản CV'}</button></div>
    </form>
    {error && <p role="alert" className="error">{error}</p>}{message && <p role="status">{message}</p>}
    {previewUrl && <section className="builder-preview"><h3>Bản xem trước PDF</h3><p>Kiểm tra nội dung, dấu tiếng Việt và trang trước khi lưu. Văn bản trong PDF có thể chọn và sao chép.</p><iframe title="Xem trước CV PDF" src={previewUrl}/></section>}
  </section>;
}
