import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { Eye, EyeSlash, ArrowUpRight, CheckCircle, ShieldCheck, Sparkle, UserCircle, WarningCircle } from '@phosphor-icons/react';
import { api } from './api';
import { Alert, Dialog, ValidatedForm } from './ui';
import { accountInitials, type AccountSession } from './accountInitials';
export { accountInitials, type AccountSession };

type AccountTab = 'general' | 'quota' | 'security';

type AiQuotaState = {
  enabled: boolean;
  provider: string;
  unit: string;
  planLabel: string;
  periodStart: string;
  periodEnd: string;
  usedAmount: number;
  reservedAmount: number;
  limitAmount: number;
  usedPercent: number;
  jobsCountInPeriod: number;
  activeJobs: number;
  maxActiveJobs: number;
  systemExhausted: boolean;
};

type PublicResumeSelection = {
  candidateId: string;
  resumeVersionId: string;
  versionNo: number;
  status: string;
  rowVersion: number;
  publishedAt: string;
  redactionVerifiedAt: string | null;
};

export function quotaSeverity(usedPercent: number, exhausted = false): 'normal' | 'warning' | 'danger' {
  const remaining = 100 - usedPercent;
  if (exhausted || remaining <= 5) return 'danger';
  if (remaining < 20) return 'warning';
  return 'normal';
}

export function validateNewPasswordMatch(newPassword: string, confirmPassword: string): string | null {
  if (newPassword !== confirmPassword) return 'Mật khẩu xác nhận không khớp với mật khẩu mới.';
  return null;
}

function roleDisplayLabel(roles: string[]): string {
  if (roles.includes('Admin')) return 'Quản trị viên';
  if (roles.includes('Recruiter')) return 'Nhà tuyển dụng';
  if (roles.includes('Candidate')) return 'Ứng viên';
  return 'Thành viên';
}

function formatDateTime(value?: string | null): string {
  if (!value) return 'Chưa ghi nhận';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Chưa ghi nhận';
  return date.toLocaleString('vi-VN', { dateStyle: 'medium', timeStyle: 'short' });
}

function formatDateOnly(value?: string | null): string {
  if (!value) return 'Đầu chu kỳ tiếp theo';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Đầu chu kỳ tiếp theo';
  return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function formatUsd(value: number): string {
  return `$${value.toFixed(value > 0 && value < 0.01 ? 4 : 2)}`;
}

function PasswordInput({
  label,
  name,
  autoComplete,
  value,
  onChange,
  minLength,
  maxLength = 256,
  required = true,
  hint,
}: {
  label: string;
  name: string;
  autoComplete: string;
  value: string;
  onChange: (value: string) => void;
  minLength?: number;
  maxLength?: number;
  required?: boolean;
  hint?: string;
}) {
  const [visible, setVisible] = useState(false);
  const inputId = useId();
  const hintId = hint ? `${inputId}-hint` : undefined;
  return (
    <div className="password-field-group">
      <label htmlFor={inputId}>{label}</label>
      <div className="password-field">
        <input
          id={inputId}
          type={visible ? 'text' : 'password'}
          name={name}
          value={value}
          onChange={e => onChange(e.target.value)}
          required={required}
          minLength={minLength}
          maxLength={maxLength}
          autoComplete={autoComplete}
          aria-describedby={hintId}
        />
        <button
          type="button"
          className="password-toggle"
          onClick={() => setVisible(v => !v)}
          aria-label={visible ? `Ẩn ${label.toLowerCase()}` : `Hiện ${label.toLowerCase()}`}
          aria-pressed={visible}
        >
          {visible ? <EyeSlash size={18} aria-hidden="true" /> : <Eye size={18} aria-hidden="true" />}
        </button>
      </div>
      {hint && <p id={hintId} className="hint password-rule-hint">{hint}</p>}
    </div>
  );
}

export function AccountSettingsPanel({
  session,
  onSessionUpdated,
  deletionSlot,
}: {
  session: AccountSession;
  onSessionUpdated: (patch: Partial<AccountSession>) => void;
  deletionSlot?: ReactNode;
}) {
  const [tab, setTab] = useState<AccountTab>('general');
  const isCandidate = session.roles.includes('Candidate');
  const isRecruiter = session.roles.includes('Recruiter');

  // Tab 1: General (Identity + Notification & Privacy)
  const [displayName, setDisplayName] = useState(session.displayName ?? '');
  const [avatarImageId, setAvatarImageId] = useState<string | null>(session.avatarImageId ?? null);
  const [avatarUrl, setAvatarUrl] = useState<string | null>(session.avatarUrl ?? null);
  const [notifyRecruitment, setNotifyRecruitment] = useState(session.notifyRecruitment ?? true);
  const [notifyCommunity, setNotifyCommunity] = useState(session.notifyCommunity ?? true);
  const [generalBusy, setGeneralBusy] = useState(false);
  const [avatarBusy, setAvatarBusy] = useState(false);
  const [generalError, setGeneralError] = useState('');
  const [generalMessage, setGeneralMessage] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [publicResume, setPublicResume] = useState<PublicResumeSelection | null | undefined>(undefined);
  const [publicResumeBusy, setPublicResumeBusy] = useState(false);

  // Tab 2: AI Quota
  const [quota, setQuota] = useState<AiQuotaState | null>(null);
  const [quotaLoading, setQuotaLoading] = useState(false);
  const [quotaError, setQuotaError] = useState('');

  // Tab 3: Security
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordBusy, setPasswordBusy] = useState(false);
  const [passwordError, setPasswordError] = useState('');
  const [passwordMessage, setPasswordMessage] = useState('');

  const [forgotOpen, setForgotOpen] = useState(false);
  const [forgotBusy, setForgotBusy] = useState(false);
  const [forgotStatus, setForgotStatus] = useState('');
  const [forgotError, setForgotError] = useState('');

  const [logoutOthersOpen, setLogoutOthersOpen] = useState(false);
  const [logoutOthersBusy, setLogoutOthersBusy] = useState(false);
  const [logoutOthersStatus, setLogoutOthersStatus] = useState('');
  const [logoutOthersError, setLogoutOthersError] = useState('');

  useEffect(() => {
    setDisplayName(session.displayName ?? '');
    setAvatarImageId(session.avatarImageId ?? null);
    setAvatarUrl(session.avatarUrl ?? null);
    setNotifyRecruitment(session.notifyRecruitment ?? true);
    setNotifyCommunity(session.notifyCommunity ?? true);
  }, [session.displayName, session.avatarImageId, session.avatarUrl, session.notifyRecruitment, session.notifyCommunity]);

  useEffect(() => {
    if (!isCandidate) return;
    let cancelled = false;
    api<PublicResumeSelection | null>('/api/resumes/public/me')
      .then(data => { if (!cancelled) setPublicResume(data); })
      .catch(() => { if (!cancelled) setPublicResume(null); });
    return () => { cancelled = true; };
  }, [isCandidate]);

  useEffect(() => {
    if (tab !== 'quota') return;
    let cancelled = false;
    setQuotaLoading(true);
    setQuotaError('');
    api<AiQuotaState>('/api/account/ai-quota')
      .then(data => { if (!cancelled) setQuota(data); })
      .catch(err => { if (!cancelled) setQuotaError((err as Error).message); })
      .finally(() => { if (!cancelled) setQuotaLoading(false); });
    return () => { cancelled = true; };
  }, [tab]);

  async function saveProfile(next: {
    displayName: string;
    avatarImageId: string | null;
    notifyRecruitment: boolean;
    notifyCommunity: boolean;
  }, successText: string) {
    setGeneralBusy(true);
    setGeneralError('');
    setGeneralMessage('');
    try {
      const updated = await api<{
        displayName: string | null;
        avatarImageId: string | null;
        avatarUrl: string | null;
        emailConfirmed: boolean;
        lastLoginAt: string | null;
        notifyRecruitment: boolean;
        notifyCommunity: boolean;
      }>('/api/account/profile', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(next),
      });
      setDisplayName(updated.displayName ?? '');
      setAvatarImageId(updated.avatarImageId);
      setAvatarUrl(updated.avatarUrl);
      setNotifyRecruitment(updated.notifyRecruitment);
      setNotifyCommunity(updated.notifyCommunity);
      onSessionUpdated({
        displayName: updated.displayName,
        avatarImageId: updated.avatarImageId,
        avatarUrl: updated.avatarUrl,
        emailConfirmed: updated.emailConfirmed,
        lastLoginAt: updated.lastLoginAt,
        notifyRecruitment: updated.notifyRecruitment,
        notifyCommunity: updated.notifyCommunity,
      });
      setGeneralMessage(successText);
    } catch (err) {
      setGeneralError((err as Error).message);
      throw err;
    } finally {
      setGeneralBusy(false);
    }
  }

  async function handleAvatarUpload(file: File) {
    setAvatarBusy(true);
    setGeneralError('');
    setGeneralMessage('');
    try {
      const formData = new FormData();
      formData.append('file', file);
      const uploaded = await api<{ id: string }>('/api/community/images', {
        method: 'POST',
        body: formData,
      });
      const fallbackName = displayName.trim() || session.email?.split('@')[0] || 'Thành viên';
      await saveProfile({
        displayName: fallbackName,
        avatarImageId: uploaded.id,
        notifyRecruitment,
        notifyCommunity,
      }, 'Đã cập nhật ảnh đại diện.');
    } catch (err) {
      setGeneralError((err as Error).message);
    } finally {
      setAvatarBusy(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }

  async function handleRemoveAvatar() {
    const fallbackName = displayName.trim() || session.email?.split('@')[0] || 'Thành viên';
    await saveProfile({
      displayName: fallbackName,
      avatarImageId: null,
      notifyRecruitment,
      notifyCommunity,
    }, 'Đã gỡ ảnh đại diện về chữ viết tắt mặc định.');
  }

  async function handleToggle(field: 'recruitment' | 'community', nextValue: boolean) {
    const prevRec = notifyRecruitment;
    const prevCom = notifyCommunity;
    const nextRec = field === 'recruitment' ? nextValue : notifyRecruitment;
    const nextCom = field === 'community' ? nextValue : notifyCommunity;
    setNotifyRecruitment(nextRec);
    setNotifyCommunity(nextCom);
    const fallbackName = displayName.trim() || session.email?.split('@')[0] || 'Thành viên';
    try {
      await saveProfile({
        displayName: fallbackName,
        avatarImageId,
        notifyRecruitment: nextRec,
        notifyCommunity: nextCom,
      }, 'Đã lưu tùy chọn thông báo.');
    } catch {
      setNotifyRecruitment(prevRec);
      setNotifyCommunity(prevCom);
    }
  }

  async function handleHidePublicResume() {
    if (!publicResume) return;
    setPublicResumeBusy(true);
    setGeneralError('');
    setGeneralMessage('');
    try {
      await api(`/api/resumes/public/me?rowVersion=${publicResume.rowVersion}`, { method: 'DELETE' });
      setPublicResume(null);
      setGeneralMessage('Đã tắt hiển thị CV trong Kho CV công khai.');
    } catch (err) {
      setGeneralError((err as Error).message);
    } finally {
      setPublicResumeBusy(false);
    }
  }

  const severity = quota ? quotaSeverity(quota.usedPercent, quota.systemExhausted || quota.usedAmount + quota.reservedAmount >= quota.limitAmount) : 'normal';

  return (
    <div className="account-shell">
      <div className="account-tabs" role="tablist" aria-label="Mục cài đặt tài khoản">
        <button
          type="button"
          role="tab"
          id="account-tab-general"
          aria-selected={tab === 'general'}
          aria-controls="account-panel-general"
          className="account-tab"
          onClick={() => setTab('general')}
        >
          Thông tin chung
        </button>
        <button
          type="button"
          role="tab"
          id="account-tab-quota"
          aria-selected={tab === 'quota'}
          aria-controls="account-panel-quota"
          className="account-tab"
          onClick={() => setTab('quota')}
        >
          Hạn mức AI
        </button>
        <button
          type="button"
          role="tab"
          id="account-tab-security"
          aria-selected={tab === 'security'}
          aria-controls="account-panel-security"
          className="account-tab"
          onClick={() => setTab('security')}
        >
          Bảo mật
        </button>
      </div>

      {tab === 'general' && (
        <div role="tabpanel" id="account-panel-general" aria-labelledby="account-tab-general" className="account-panel">
          <section className="card">
            <div className="section-heading">
              <div>
                <p className="eyebrow">ĐỊNH DANH TÀI KHOẢN</p>
                <h2>Thông tin cá nhân</h2>
              </div>
              <span className="badge">{roleDisplayLabel(session.roles)}</span>
            </div>

            <div className="account-avatar-row">
              {avatarUrl ? (
                <img src={avatarUrl} alt="Ảnh đại diện hiện tại" className="account-avatar-img" />
              ) : (
                <span className="account-avatar-fallback" aria-hidden="true">
                  {accountInitials(displayName || session.displayName, session.email)}
                </span>
              )}
              <div className="account-avatar-actions">
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/png,image/jpeg"
                  className="sr-only"
                  aria-label="Chọn ảnh đại diện mới"
                  onChange={e => {
                    const file = e.target.files?.[0];
                    if (file) void handleAvatarUpload(file);
                  }}
                />
                <div className="account-avatar-buttons">
                  <button
                    type="button"
                    className="secondary"
                    disabled={avatarBusy || generalBusy}
                    onClick={() => fileInputRef.current?.click()}
                  >
                    <UserCircle size={17} aria-hidden="true" />
                    {avatarBusy ? 'Đang tải ảnh…' : 'Đổi ảnh'}
                  </button>
                  {avatarUrl && (
                    <button
                      type="button"
                      className="link-button"
                      disabled={avatarBusy || generalBusy}
                      onClick={() => void handleRemoveAvatar()}
                    >
                      Xóa ảnh
                    </button>
                  )}
                </div>
                <p className="hint m-0">PNG hoặc JPEG tối đa 2 MB. Nếu chưa tải ảnh, hệ thống dùng chữ viết tắt.</p>
              </div>
            </div>

            <ValidatedForm
              className="account-form-narrow"
              onSubmit={e => {
                e.preventDefault();
                void saveProfile({
                  displayName: displayName.trim(),
                  avatarImageId,
                  notifyRecruitment,
                  notifyCommunity,
                }, 'Đã cập nhật thông tin tài khoản.');
              }}
            >
              <label>
                Họ tên hiển thị
                <input
                  type="text"
                  name="displayName"
                  value={displayName}
                  onChange={e => setDisplayName(e.target.value)}
                  maxLength={150}
                  required
                  placeholder="Nhập tên hiển thị của bạn"
                />
              </label>

              <div className="account-meta-grid">
                <div className="account-meta-item">
                  <span className="account-meta-label">Email đăng nhập</span>
                  <div className="account-meta-value">
                    <strong>{session.email || 'Chưa có email'}</strong>
                    <span className={`badge ${session.emailConfirmed ? 'is-verified' : 'is-unverified'}`}>
                      {session.emailConfirmed ? (
                        <>
                          <CheckCircle size={14} aria-hidden="true" /> Đã xác thực
                        </>
                      ) : (
                        'Chưa xác thực'
                      )}
                    </span>
                  </div>
                  {isRecruiter && !session.emailConfirmed && (
                    <a href="#recruiter-company" className="account-inline-link">
                      Xác minh email doanh nghiệp <ArrowUpRight size={14} aria-hidden="true" />
                    </a>
                  )}
                </div>

                <div className="account-meta-item">
                  <span className="account-meta-label">Vai trò hệ thống</span>
                  <div className="account-meta-value">
                    <strong>{roleDisplayLabel(session.roles)}</strong>
                  </div>
                </div>
              </div>

              <button type="submit" disabled={generalBusy || avatarBusy}>
                {generalBusy ? 'Đang lưu…' : 'Lưu thay đổi'}
              </button>
            </ValidatedForm>

            {generalError && <p role="alert" className="error mt-4">{generalError}</p>}
            {generalMessage && <p role="status" className="mt-4">{generalMessage}</p>}
          </section>

          <section className="card">
            <div className="section-heading">
              <div>
                <p className="eyebrow">THÔNG BÁO &amp; QUYỀN RIÊNG TƯ</p>
                <h2>Tùy chọn hiển thị &amp; thông báo</h2>
              </div>
            </div>

            <div className="toggle-list">
              <div className="toggle-row">
                <div className="toggle-copy">
                  <strong>Cập nhật đơn ứng tuyển trong ứng dụng</strong>
                  <p className="hint m-0">Nhận thông báo khi trạng thái hồ sơ ứng tuyển hoặc tin tuyển dụng có thay đổi.</p>
                </div>
                <button
                  type="button"
                  role="switch"
                  aria-checked={notifyRecruitment}
                  aria-label="Bật hoặc tắt thông báo cập nhật đơn ứng tuyển"
                  className={`toggle-switch ${notifyRecruitment ? 'is-on' : ''}`}
                  disabled={generalBusy}
                  onClick={() => void handleToggle('recruitment', !notifyRecruitment)}
                >
                  <span className="toggle-thumb" />
                </button>
              </div>

              <div className="toggle-row">
                <div className="toggle-copy">
                  <strong>Tương tác cộng đồng nghề nghiệp</strong>
                  <p className="hint m-0">Nhận thông báo khi có bình luận, trả lời hoặc người bạn theo dõi đăng bài mới.</p>
                </div>
                <button
                  type="button"
                  role="switch"
                  aria-checked={notifyCommunity}
                  aria-label="Bật hoặc tắt thông báo tương tác cộng đồng"
                  className={`toggle-switch ${notifyCommunity ? 'is-on' : ''}`}
                  disabled={generalBusy}
                  onClick={() => void handleToggle('community', !notifyCommunity)}
                >
                  <span className="toggle-thumb" />
                </button>
              </div>

              {isCandidate && (
                <div className="toggle-row">
                  <div className="toggle-copy">
                    <strong>Hiển thị trong Kho CV công khai</strong>
                    <p className="hint m-0">
                      {publicResume === undefined
                        ? 'Đang kiểm tra trạng thái CV công khai…'
                        : publicResume
                          ? `Đang công khai: Phiên bản #${publicResume.versionNo} (đã ẩn thông tin liên hệ).`
                          : 'Hiện tại chưa có phiên bản CV nào được đặt ở chế độ công khai.'}
                    </p>
                  </div>
                  <div className="account-privacy-actions">
                    {publicResume && (
                      <button
                        type="button"
                        className="secondary"
                        disabled={publicResumeBusy}
                        onClick={() => void handleHidePublicResume()}
                      >
                        {publicResumeBusy ? 'Đang tắt…' : 'Tắt công khai'}
                      </button>
                    )}
                    <a href="#private-resumes" className="account-inline-link">
                      Chọn CV công khai <ArrowUpRight size={14} aria-hidden="true" />
                    </a>
                  </div>
                </div>
              )}
            </div>
          </section>
        </div>
      )}

      {tab === 'quota' && (
        <div role="tabpanel" id="account-panel-quota" aria-labelledby="account-tab-quota" className="account-panel">
          <section className="card">
            <div className="section-heading">
              <div>
                <p className="eyebrow">HẠN MỨC SỬ DỤNG AI</p>
                <h2>Mức dùng trong kỳ</h2>
              </div>
              <span className="badge">
                <Sparkle size={14} aria-hidden="true" /> {quota?.planLabel ?? 'Gói Cơ bản'}
              </span>
            </div>

            {quotaLoading && <p className="hint">Đang tải số liệu hạn mức AI…</p>}
            {quotaError && <p role="alert" className="error">{quotaError}</p>}

            {quota && (
              <div className="quota-summary">
                {!quota.enabled && (
                  <Alert tone="warning" title="AI đang tạm tắt">
                    Chức năng phân tích AI hiện đang tắt trong cấu hình môi trường này. Bạn vẫn có thể xem lại kết quả cũ và thao tác thủ công.
                  </Alert>
                )}
                {quota.enabled && quota.systemExhausted && (
                  <Alert tone="error" title="Đã chạm ngưỡng ngân sách kỳ">
                    Hạn mức phân tích AI trong kỳ này đã hết. Các tác vụ AI mới sẽ tạm dừng cho đến kỳ làm mới tiếp theo.
                  </Alert>
                )}

                <div className="quota-header-row">
                  <div>
                    <strong className="quota-headline">
                      {quota.provider === 'Fake'
                        ? `Đã dùng ${quota.usedAmount} / ${quota.limitAmount} lượt`
                        : `${quota.usedPercent}% hạn mức kỳ đã dùng`}
                    </strong>
                    <p className="hint m-0">
                      {quota.provider === 'Fake'
                        ? (quota.reservedAmount > 0 ? `Đang giữ chỗ ${quota.reservedAmount} lượt cho tác vụ chờ xử lý.` : 'Chế độ mô phỏng nội bộ.')
                        : `Đã dùng ${formatUsd(quota.usedAmount)} / ${formatUsd(quota.limitAmount)} · ${quota.jobsCountInPeriod} lượt phân tích trong kỳ.`}
                    </p>
                  </div>
                  <span className={`badge ${severity === 'danger' ? 'is-danger' : severity === 'warning' ? 'is-warning' : ''}`}>
                    Còn {Math.max(0, Number((100 - quota.usedPercent).toFixed(1)))}%
                  </span>
                </div>

                <div
                  className="quota-bar"
                  role="progressbar"
                  aria-valuenow={Math.min(100, Math.round(quota.usedPercent))}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-label="Tỷ lệ hạn mức AI đã sử dụng"
                >
                  <div
                    className={`quota-bar-fill is-${severity}`}
                    style={{ width: `${Math.min(100, Math.max(0, quota.usedPercent))}%` }}
                  />
                </div>

                <div className="quota-meta-grid">
                  <div>
                    <span className="account-meta-label">Tác vụ đang chạy đồng thời</span>
                    <strong>{quota.activeJobs} / {quota.maxActiveJobs} tác vụ</strong>
                  </div>
                  <div>
                    <span className="account-meta-label">Chu kỳ làm mới tiếp theo</span>
                    <strong>{formatDateOnly(quota.periodEnd)}</strong>
                  </div>
                </div>
              </div>
            )}
          </section>

          <section className="card">
            <div className="section-heading">
              <div>
                <p className="eyebrow">QUY TẮC TÍNH LƯỢT</p>
                <h2>Các tác vụ tính vào hạn mức</h2>
              </div>
              <a href="#analysis-history" className="account-inline-link">
                Mở Lịch sử phân tích <ArrowUpRight size={14} aria-hidden="true" />
              </a>
            </div>
            <ul className="quota-rules-list">
              <li><strong>Trích xuất dữ liệu CV:</strong> phân tích tệp PDF/DOCX thành kỹ năng và kinh nghiệm có trích dẫn.</li>
              <li><strong>Đối chiếu CV–JD:</strong> chấm điểm phù hợp theo 6 nhóm tiêu chí và gợi ý điểm thiếu hụt.</li>
              <li><strong>Gợi ý viết lại nội dung:</strong> đề xuất chỉnh sửa câu chữ CV hoặc mô tả công việc.</li>
              <li><strong>Xem lại kết quả đã lưu:</strong> mở lại cùng một cặp CV–JD đã phân tích thành công trước đó không bị trừ thêm hạn mức.</li>
            </ul>
          </section>
        </div>
      )}

      {tab === 'security' && (
        <div role="tabpanel" id="account-panel-security" aria-labelledby="account-tab-security" className="account-panel">
          <section className="card">
            <div className="section-heading">
              <div>
                <p className="eyebrow">MẬT KHẨU ĐĂNG NHẬP</p>
                <h2>Đổi mật khẩu</h2>
              </div>
            </div>

            <ValidatedForm
              className="account-form-narrow"
              onSubmit={e => {
                e.preventDefault();
                const mismatch = validateNewPasswordMatch(newPassword, confirmPassword);
                if (mismatch) {
                  setPasswordError(mismatch);
                  setPasswordMessage('');
                  return;
                }
                void (async () => {
                  setPasswordBusy(true);
                  setPasswordError('');
                  setPasswordMessage('');
                  try {
                    await api('/api/auth/password/change', {
                      method: 'POST',
                      headers: { 'Content-Type': 'application/json' },
                      body: JSON.stringify({ currentPassword, newPassword }),
                    });
                    setCurrentPassword('');
                    setNewPassword('');
                    setConfirmPassword('');
                    setPasswordMessage('Đã đổi mật khẩu thành công.');
                  } catch (err) {
                    setPasswordError((err as Error).message);
                  } finally {
                    setPasswordBusy(false);
                  }
                })();
              }}
            >
              <PasswordInput
                label="Mật khẩu hiện tại"
                name="currentPassword"
                autoComplete="current-password"
                value={currentPassword}
                onChange={setCurrentPassword}
              />
              <PasswordInput
                label="Mật khẩu mới"
                name="newPassword"
                autoComplete="new-password"
                value={newPassword}
                onChange={setNewPassword}
                minLength={12}
                hint="Tối thiểu 12 ký tự, gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt."
              />
              <PasswordInput
                label="Xác nhận mật khẩu mới"
                name="confirmPassword"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={setConfirmPassword}
                minLength={12}
              />

              <div className="account-form-actions">
                <button type="submit" disabled={passwordBusy}>
                  {passwordBusy ? 'Đang lưu…' : 'Đổi mật khẩu'}
                </button>
                <button
                  type="button"
                  className="link-button"
                  onClick={() => {
                    setForgotStatus('');
                    setForgotError('');
                    setForgotOpen(true);
                  }}
                >
                  Quên mật khẩu?
                </button>
              </div>
            </ValidatedForm>

            {passwordError && <p role="alert" className="error mt-4">{passwordError}</p>}
            {passwordMessage && <p role="status" className="mt-4">{passwordMessage}</p>}
          </section>

          <section className="card">
            <div className="section-heading">
              <div>
                <p className="eyebrow">PHIÊN HOẠT ĐỘNG</p>
                <h2>Phương thức đăng nhập &amp; thiết bị</h2>
              </div>
              <span className="badge">
                <ShieldCheck size={14} aria-hidden="true" /> Email &amp; Mật khẩu
              </span>
            </div>

            <div className="account-session-row">
              <div>
                <p className="m-0">
                  <strong>Đăng nhập gần nhất:</strong> {formatDateTime(session.lastLoginAt)}
                </p>
                <p className="hint m-0 mt-1">
                  Thu hồi toàn bộ phiên đăng nhập trên các trình duyệt hoặc thiết bị khác mà không làm gián đoạn phiên hiện tại.
                </p>
              </div>
              <button
                type="button"
                className="secondary"
                onClick={() => {
                  setLogoutOthersError('');
                  setLogoutOthersStatus('');
                  setLogoutOthersOpen(true);
                }}
              >
                Đăng xuất khỏi tất cả thiết bị khác
              </button>
            </div>
            {logoutOthersStatus && <p role="status" className="mt-4">{logoutOthersStatus}</p>}
          </section>
        </div>
      )}

      {deletionSlot && <div className="mt-6">{deletionSlot}</div>}

      <Dialog
        open={forgotOpen}
        title="Gửi liên kết đặt lại mật khẩu"
        busy={forgotBusy}
        onClose={() => setForgotOpen(false)}
      >
        <p>
          Hệ thống sẽ gửi hướng dẫn đặt lại mật khẩu tới email đang đăng nhập: <strong>{session.email}</strong>.
        </p>
        {forgotError && <p role="alert" className="error">{forgotError}</p>}
        {forgotStatus && <p role="status">{forgotStatus}</p>}
        <div className="dialog-actions">
          <button type="button" className="secondary" onClick={() => setForgotOpen(false)} disabled={forgotBusy}>
            Đóng
          </button>
          <button
            type="button"
            disabled={forgotBusy || !session.email}
            onClick={() => {
              void (async () => {
                setForgotBusy(true);
                setForgotError('');
                setForgotStatus('');
                try {
                  await api('/api/auth/password/forgot', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ email: session.email }),
                  });
                  setForgotStatus('Đã gửi yêu cầu khôi phục mật khẩu tới email của bạn.');
                } catch (err) {
                  setForgotError((err as Error).message);
                } finally {
                  setForgotBusy(false);
                }
              })();
            }}
          >
            {forgotBusy ? 'Đang gửi…' : 'Gửi liên kết'}
          </button>
        </div>
      </Dialog>

      <Dialog
        open={logoutOthersOpen}
        title="Đăng xuất khỏi tất cả thiết bị khác"
        busy={logoutOthersBusy}
        onClose={() => setLogoutOthersOpen(false)}
      >
        <p>
          Toàn bộ phiên đăng nhập ở các trình duyệt hoặc thiết bị khác sẽ bị thu hồi ngay lập tức. Phiên hiện tại của bạn vẫn được giữ nguyên.
        </p>
        {logoutOthersError && (
          <p role="alert" className="error">
            <WarningCircle size={16} aria-hidden="true" /> {logoutOthersError}
          </p>
        )}
        <div className="dialog-actions">
          <button type="button" className="secondary" onClick={() => setLogoutOthersOpen(false)} disabled={logoutOthersBusy}>
            Hủy
          </button>
          <button
            type="button"
            disabled={logoutOthersBusy}
            onClick={() => {
              void (async () => {
                setLogoutOthersBusy(true);
                setLogoutOthersError('');
                try {
                  await api('/api/auth/logout-other-devices', { method: 'POST' });
                  setLogoutOthersOpen(false);
                  setLogoutOthersStatus('Đã đăng xuất khỏi tất cả thiết bị khác.');
                } catch (err) {
                  setLogoutOthersError((err as Error).message);
                } finally {
                  setLogoutOthersBusy(false);
                }
              })();
            }}
          >
            {logoutOthersBusy ? 'Đang xử lý…' : 'Xác nhận đăng xuất'}
          </button>
        </div>
      </Dialog>
    </div>
  );
}
