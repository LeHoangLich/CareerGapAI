import { useEffect, useState } from 'react';
import { ArrowRight, Briefcase, GlobeHemisphereWest, MagnifyingGlass, Sparkle } from '@phosphor-icons/react';
import { api } from './api';
import { CompanyLogo } from './companyLogo';
import { jobHref } from './jobLinks';
import { LocationInput } from './locationInput';
import { salaryLabel } from './salary';
import { ValidatedForm } from './ui';

type HomeJob = {
  id: string;
  tags?: string[];
  viewCount?: number;
  company: { name: string; location: string | null; logoUrl?: string | null };
  version: {
    title: string;
    location: string | null;
    workMode: string;
    salaryMin: number | null;
    salaryMax: number | null;
    salaryCurrency: string | null;
    salaryPeriod: string | null;
  };
};

type HomePost = {
  id: string;
  title: string;
  summary: string;
  author: string;
  handle: string;
  views: number;
  votes: number;
  tags: string[];
};

const featuredTags = ['React', 'TypeScript', '.NET', 'C#', 'PostgreSQL', 'Docker', 'Python', 'SQL'];
const featuredLocations = ['Hà Nội', 'Hồ Chí Minh', 'Đà Nẵng', 'Cần Thơ'];

export function PublicHome({ active }: { active: boolean }) {
  const [q, setQ] = useState('');
  const [location, setLocation] = useState('');
  const [jobs, setJobs] = useState<HomeJob[]>([]);
  const [posts, setPosts] = useState<HomePost[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!active) return;
    let disposed = false;
    setLoading(true);
    void Promise.all([
      api<{ items: HomeJob[] }>('/api/jobs?page=1&pageSize=6').catch(() => ({ items: [] })),
      api<{ items: HomePost[] }>('/api/community/posts?mode=discover&sort=trending&page=1').catch(() => ({ items: [] }))
    ]).then(([jobPage, postPage]) => {
      if (disposed) return;
      setJobs(jobPage.items);
      setPosts(postPage.items.slice(0, 4));
    }).finally(() => { if (!disposed) setLoading(false); });
    return () => { disposed = true; };
  }, [active]);

  function searchJobs(event: React.FormEvent) {
    event.preventDefault();
    const params = new URLSearchParams();
    if (q.trim()) params.set('q', q.trim());
    if (location.trim()) params.set('location', location.trim());
    const suffix = params.toString();
    window.location.hash = suffix ? `jobs?${suffix}` : 'jobs';
  }

  return <section className="public-home" aria-label="Trang chủ tổng hợp CareerGap AI">
    <section className="card">
      <p className="eyebrow">CAREERGAP AI · TRANG CHỦ KHÁM PHÁ</p>
      <h1>Kết nối hồ sơ minh bạch, đối chiếu bằng chứng CV–JD.</h1>
      <p>Tìm việc làm theo tag &amp; địa điểm chuẩn, xem đánh giá khoảng trống kỹ năng và trao đổi cùng cộng đồng nghề nghiệp.</p>
      <ValidatedForm className="job-filters job-search-bar" onSubmit={searchJobs}>
        <label>Từ khóa việc làm<input value={q} maxLength={120} onChange={e => setQ(e.target.value)} placeholder="Vị trí, kỹ năng hoặc công ty…"/></label>
        <label>Địa điểm<LocationInput value={location} onChange={setLocation} placeholder="Tỉnh, thành phố"/></label>
        <button><MagnifyingGlass size={17} aria-hidden="true"/>Tìm việc ngay</button>
      </ValidatedForm>
      <div className="row" aria-label="Tag việc làm phổ biến">
        <strong>Tag phổ biến:</strong>
        {featuredTags.map(tag => <a key={tag} className="badge" href={`#jobs?tags=${encodeURIComponent(tag)}`}>{tag}</a>)}
      </div>
      <div className="row" aria-label="Địa điểm phổ biến">
        <strong>Địa điểm:</strong>
        {featuredLocations.map(loc => <a key={loc} className="badge" href={`#jobs?location=${encodeURIComponent(loc)}`}>{loc}</a>)}
      </div>
    </section>

    <section className="card" aria-label="Việc làm mới nhất">
      <div className="section-heading">
        <div><p className="eyebrow">CƠ HỘI MỚI NHẤT</p><h2>Tin tuyển dụng đang mở</h2></div>
        <a href="#jobs">Xem tất cả việc làm <ArrowRight size={15} aria-hidden="true"/></a>
      </div>
      {loading && jobs.length === 0 && <p role="status">Đang tải việc làm nổi bật…</p>}
      {!loading && jobs.length === 0 && <p className="hint">Chưa có tin tuyển dụng công khai nào.</p>}
      <div className="form-columns">
        {jobs.map(job => <article className="job-card" key={job.id}>
          <div className="job-title-row">
            <div className="job-identity">
              <CompanyLogo name={job.company.name} url={job.company.logoUrl}/>
              <div><h3><a href={jobHref(job.id)}>{job.version.title}</a></h3><p className="company-name">{job.company.name}</p></div>
            </div>
          </div>
          <p className="job-salary"><strong>{salaryLabel(job.version)}</strong></p>
          <p className="hint">{job.version.location || job.company.location || 'Chưa có địa điểm'} · {(job.viewCount ?? 0).toLocaleString('vi-VN')} lượt xem</p>
          {!!job.tags?.length && <div className="row job-tags">{job.tags.slice(0, 4).map(t => <span className="badge" key={t}>{t}</span>)}</div>}
        </article>)}
      </div>
    </section>

    <section className="card" aria-label="Giới thiệu quy trình Skill Gap">
      <div className="section-heading">
        <div><p className="eyebrow">CÔNG NGHỆ ĐỐI CHIẾU</p><h2>Skill Gap &amp; Lộ trình học theo bằng chứng</h2></div>
        <Sparkle size={22} aria-hidden="true"/>
      </div>
      <div className="form-columns">
        <article className="card"><h3><Briefcase size={18} aria-hidden="true"/> 1. Đối chiếu đa nguồn</h3><p>Chọn CV đã xác nhận hoặc dán văn bản CV để đối chiếu với JD trên sàn, dán tay hoặc nhập từ URL.</p></article>
        <article className="card"><h3>2. 6 nhóm tiêu chí rõ ràng</h3><p>Tổng hợp kỹ năng, kinh nghiệm, thành tích, portfolio, học vấn và độ phủ từ khóa ATS có trích dẫn nguyên văn.</p></article>
        <article className="card"><h3>3. Lộ trình tuần theo bối cảnh</h3><p>Tự động sắp xếp kỹ năng thiếu theo tuần và lưu tiến độ cho mục tiêu bắt đầu nghề, chuyển ngành hoặc thăng tiến.</p></article>
      </div>
      <div className="row"><a href="#jobs">Thử đối chiếu CV–JD</a><a href="#private-resumes">Quản lý CV của bạn</a></div>
    </section>

    <section className="card" aria-label="Bài viết xu hướng cộng đồng">
      <div className="section-heading">
        <div><p className="eyebrow">CỘNG ĐỒNG NGHỀ NGHIỆP</p><h2>Bài viết được quan tâm</h2></div>
        <a href="#community"><GlobeHemisphereWest size={15} aria-hidden="true"/> Vào cộng đồng</a>
      </div>
      {posts.length === 0 ? <p className="hint">Chưa có bài viết công khai nào.</p> : <div className="form-columns">
        {posts.map(post => <article className="card" key={post.id}>
          <h3><a href={`#community?post=${post.id}`}>{post.title}</a></h3>
          <p>{post.summary || post.title}</p>
          <small>@{post.handle} · {post.votes} thích · {post.views} lượt xem</small>
        </article>)}
      </div>}
    </section>
  </section>;
}
