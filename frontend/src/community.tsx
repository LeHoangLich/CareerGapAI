import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { api } from './api';
import { Alert, ConfirmDialog, Dialog, EmptyState, PageHeader, Pagination, StaticLoading, ValidatedForm } from './ui';
import { CommunityMarkdown } from './communityMarkdown';
import { Bell, BookmarkSimple, ChatCircle, Heart, MagnifyingGlass, PencilSimple, Users, ArrowLeft, Flag, FileText, CheckCircle, CloudArrowUp, Code, CodeBlock, ImageSquare, LinkSimple, ListBullets, ListNumbers, Quotes, TextB, TextH, TextItalic, TextStrikethrough, TextUnderline, X } from '@phosphor-icons/react';
import { Spinner } from './motion';
import { CommunityBadge } from './communityBadge';
export { CommunityBadge };
import {communityTagMatch} from './communityTagMatch';
import {CommunityTagFilter} from './communityTagFilter';
import {communityFeedParams,communityFilterTags,communityFeedPage,communityFeedHref,communityTagHref} from './communityFeedFilters';
import { CommunityReportModeration } from './communityReports';
import { CareerCard, type Career } from './communityCareer';
import { signInReturnHref } from './jobLinks';

type Session = { authenticated: boolean; userId?: string; roles: string[]; deletionPending?: boolean };
type Profile = { userId: string; handle: string; displayName: string; bio: string; avatarId: string | null; allowMentions: boolean; rowVersion: number; followers: number; following: number; posts: number; isFollowing: boolean; showCareer: boolean; career: Career | null };
type Person = Pick<Profile, 'userId'|'handle'|'displayName'|'avatarId'>;
type Post = { id: string; authorId: string; author: string; handle: string; avatarId: string | null; title: string; slug: string; summary: string; body: string | null; category: string; tags: string[]; thumbnailId: string | null; status: string; rowVersion: number; createdAt: string; publishedAt: string | null; votes: number; comments: number; bookmarks: number; views: number; voted: boolean; bookmarked: boolean };
type Comment = { id: string; authorId: string; author: string; handle: string; avatarId: string | null; parentId: string | null; body: string; isDeleted: boolean; rowVersion: number; createdAt: string };
type Page<T> = { items: T[]; page: number; pageSize: number; total: number };
type Notice = { id: string; kind: string; message: string; postId: string | null; actorId: string; createdAt: string; readAt: string | null; available: boolean };

const base = '/api/community';
const categories: Record<string,string> = { career:'Chuyện nghề nghiệp', technical:'Chia sẻ kỹ thuật', interview:'Ứng tuyển & Phỏng vấn', questions:'Hỏi đáp' };
const states: Record<string,string> = { draft:'Bản nháp', pending:'Chờ duyệt', published:'Đã đăng', hidden:'Đã ẩn', deleted:'Thùng rác' };
const date = (value: string) => new Date(value).toLocaleString('vi-VN', {dateStyle:'medium',timeStyle:'short'});
const json = (value: unknown) => ({ headers:{'Content-Type':'application/json'}, body:JSON.stringify(value) });
const link = (key: string, value: string) => `#community?${key}=${encodeURIComponent(value)}`;
function Avatar({ person }: { person: { avatarId: string | null; displayName?: string; author?: string } }) { return person.avatarId ? <img className="social-avatar" src={`${base}/images/${person.avatarId}`} alt="" loading="lazy"/> : <span className="social-avatar" aria-hidden="true">{(person.displayName ?? person.author ?? '?').slice(0,1).toUpperCase()}</span>; }

export function Community({ session, active }: {session?:Session;active:boolean}) {
  const [route,setRoute]=useState(()=>new URLSearchParams(location.hash.split('?')[1]??''));
  const [mine,setMine]=useState<Profile|null>(null);const [profileReady,setProfileReady]=useState(false);
  const [page,setPage]=useState(()=>communityFeedPage(communityFeedParams()));const [feed,setFeed]=useState<Page<Post>>();const [post,setPost]=useState<Post>();const [person,setPerson]=useState<Profile>();
  const [q,setQ]=useState(()=>communityFeedParams().get('q')??'');const [query,setQuery]=useState(()=>communityFeedParams().get('q')??'');const [sort,setSort]=useState(()=>communityFeedParams().get('sort')??'newest');const [category,setCategory]=useState(()=>communityFeedParams().get('category')??'');const [suggestions,setSuggestions]=useState<string[]>([]);const [trendingSearches,setTrendingSearches]=useState<{term:string;count:number}[]>([]);
  const [liveUpdate,setLiveUpdate]=useState(false);const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [message,setMessage]=useState('');const [editor,setEditor]=useState<Post|null|undefined>();const [editProfile,setEditProfile]=useState(false);
  const [reportTarget,setReportTarget]=useState<{postId:string;commentId?:string}|null>(null);const [reportReason,setReportReason]=useState('');
  const signed=!!session?.authenticated&&!session.deletionPending;const admin=!!session?.roles.includes('Admin');
  const tab=route.get('tab')??'discover';const postId=route.get('post');const personId=route.get('person');const handle=route.get('handle');
  useEffect(()=>{const update=()=>{const next=communityFeedParams();setRoute(next);setPage(communityFeedPage(next));setQ(next.get('q')??'');setQuery(next.get('q')??'');setSort(next.get('sort')??'newest');setCategory(next.get('category')??'');setError('');setPost(undefined);setPerson(undefined);};window.addEventListener('hashchange',update);return()=>window.removeEventListener('hashchange',update);},[]);
  useEffect(()=>{if(!active)return;if(!signed){setMine(null);setProfileReady(true);return;}let disposed=false;void api<{profile:Profile|null}>(`${base}/me`).then(x=>{if(!disposed){setMine(x.profile);setProfileReady(true);}}).catch(e=>{if(!disposed)setError((e as Error).message);});return()=>{disposed=true};},[active,signed,session?.userId]);
  useEffect(()=>{if(!active)return;let disposed=false;const timer=setTimeout(()=>{void api<string[]>(`${base}/suggestions?q=${encodeURIComponent(q)}`).then(x=>{if(!disposed)setSuggestions(x)}).catch(()=>{});},250);return()=>{disposed=true;clearTimeout(timer)};},[active,q]);
  useEffect(()=>{if(!active)return;let disposed=false;void api<{term:string;count:number}[]>(`${base}/trending-searches`).then(x=>{if(!disposed)setTrendingSearches(x)}).catch(()=>{});return()=>{disposed=true};},[active,query]);
  const loadSequence=useRef(0);
  const load=useCallback(async()=>{
    const ticket=++loadSequence.current;
    if(!active)return;
    if(postId){const next=await api<Post>(`${base}/posts/${postId}`);if(ticket===loadSequence.current)setPost(next);return;}
    let owner=personId;
    if(handle){const users=await api<Person[]>(`${base}/people?q=${encodeURIComponent(handle)}`);owner=users.find(x=>x.handle===handle.toLowerCase())?.userId??null;if(!owner)throw new Error('Không tìm thấy hồ sơ cộng đồng.');}
    if(owner){const next=await api<Profile>(`${base}/profiles/${owner}`);if(ticket===loadSequence.current)setPerson(next);}
    if(tab==='notifications'||tab==='moderation')return;
    if(!signed&&['following','saved','mine'].includes(tab)){setFeed(undefined);return;}
    const params=new URLSearchParams({mode:owner?'discover':tab,q:query,sort,page:String(page)});
    if(owner)params.set('author',owner);if(category)params.set('category',category);if(route.get('tag'))params.set('tag',route.get('tag')!);if(route.get('tags'))params.set('tags',route.get('tags')!);
    const next=await api<Page<Post>>(`${base}/posts?${params}`);if(ticket===loadSequence.current){const last=Math.max(1,Math.ceil(next.total/12));if(page>last){location.hash=communityFeedHref(route,{page:last});return}setFeed(next);}
  },[active,postId,personId,handle,tab,signed,query,sort,page,category,route]);
  useEffect(()=>{let disposed=false;setBusy(true);setError('');void load().catch(e=>{if(!disposed){setPost(undefined);setFeed(undefined);setError((e as Error).message)}}).finally(()=>{if(!disposed)setBusy(false)});return()=>{disposed=true;loadSequence.current++};},[load]);
  useEffect(()=>{if(!active)return;const refresh=()=>{if(editor!==undefined||tab==='notifications'||tab==='moderation')return;if(!postId){setLiveUpdate(true);return;}void load().catch(e=>{setPost(undefined);setError((e as Error).message)});};window.addEventListener('community:update',refresh);return()=>window.removeEventListener('community:update',refresh);},[active,load,editor,postId,tab]);
  async function act(work:()=>Promise<unknown>,success:string){setBusy(true);setError('');try{await work();setMessage(success);await load();window.dispatchEvent(new CustomEvent('community:update'));}catch(e){setError((e as Error).message)}finally{setBusy(false)}}
  function needProfile(){if(!signed){location.hash=signInReturnHref(location.hash);return false}if(!mine){setEditProfile(true);return false}return true}
  function compose(p:Post|null){if(needProfile())setEditor(p)}
  const canAuthor=signed&&session?.roles.some(x=>x==='Candidate'||x==='Recruiter');
  async function toggle(p:Post,kind:'vote'|'bookmark'){if(!needProfile())return;const enabled=kind==='vote'?p.voted:p.bookmarked;await act(()=>api(`${base}/posts/${p.id}/${kind}`,{method:enabled?'DELETE':'PUT'}),enabled?'Đã bỏ tương tác.':'Đã lưu tương tác.');}
  return <div className="social-workspace">
    <PageHeader
      eyebrow="CỘNG ĐỒNG NGHỀ NGHIỆP"
      title="Chia sẻ để cùng tiến xa."
      description="Góc nhìn, kinh nghiệm và những cuộc trò chuyện có ích cho hành trình nghề nghiệp."
      actions={canAuthor && <button type="button" onClick={()=>compose(null)}><PencilSimple size={18} aria-hidden="true"/>Viết bài</button>}
    />
    <nav className="sub-tabs" aria-label="Không gian cộng đồng">
      {[['discover','Khám phá',FileText],['following','Đang theo dõi',Users],['saved','Bài đã lưu',BookmarkSimple],['mine','Bài của tôi',PencilSimple],['notifications','Thông báo',Bell],...(admin?[['moderation','Kiểm duyệt',CheckCircle]]:[])].map(([key,label,Icon])=>{
        const I=Icon as typeof Bell;
        const activeTab=tab===key&&!postId&&!personId&&!handle;
        return <a key={key as string} className={activeTab?'active':''} aria-current={activeTab?'page':undefined} href={link('tab',key as string)}><I size={16} aria-hidden="true"/>{label as string}</a>;
      })}
    </nav>
    <div className="social-layout"><section className="social-main" aria-label="Nội dung cộng đồng">
      {error&&<Alert tone="error">{error}</Alert>}{message&&<Alert tone="success">{message}</Alert>}
      {tab==='notifications'?signed?<CommunityNotifications onError={setError}/>:<LoginPrompt/>:tab==='moderation'?admin?<CommunityModeration onError={setError} onChanged={()=>void load()}/>:<LoginPrompt/>:postId?<>{post?<>
        <a className="social-back" href="#community"><ArrowLeft size={16} aria-hidden="true"/>Về cộng đồng</a>
        <PostCard post={post} detail viewer={session?.userId} admin={admin} busy={busy} onToggle={toggle} onEdit={compose} onReport={()=>signed?setReportTarget({postId:post.id}):location.hash=signInReturnHref(location.hash)} onState={(action,reason)=>act(()=>api(`${base}/posts/${post.id}/state`,{method:'POST',...json({action,reason,rowVersion:post.rowVersion})}),'Đã cập nhật trạng thái bài viết.')}/>
        {post.status==='published'&&<Comments postId={post.id} viewer={session?.userId} admin={admin} ensureProfile={needProfile} onError={setError} onReport={commentId=>setReportTarget({postId:post.id,commentId})}/>}
      </>:busy?<StaticLoading message="Đang tải hoặc lưu…"/>:!error&&<EmptyState title="Không tìm thấy bài viết."/>}</>:<>
        {person&&<section className="card social-person"><Avatar person={person}/><div><h2>{person.displayName}</h2><p>@{person.handle}</p><p>{person.bio}</p><p>{person.followers} người theo dõi · {person.following} đang theo dõi · {person.posts} bài viết</p><Connections person={person}/></div>{person.userId!==session?.userId&&<button type="button" className="secondary" disabled={busy} onClick={()=>{if(needProfile())void act(async()=>{await api(`${base}/profiles/${person.userId}/follow`,{method:person.isFollowing?'DELETE':'PUT'});setPerson(await api<Profile>(`${base}/profiles/${person.userId}`));},person.isFollowing?'Đã bỏ theo dõi.':'Đã theo dõi.')}}>{person.isFollowing?'Bỏ theo dõi':'Theo dõi'}</button>}</section>}
        {person&&<CareerCard career={person.career} visible={person.showCareer} own={person.userId===session?.userId}/>} 
        {!['following','saved','mine'].includes(tab)||signed?<>
          <ValidatedForm className="social-filters" onSubmit={e=>{e.preventDefault();location.hash=communityFeedHref(route,{q,page:1})}}><label>Tìm bài viết<input name="q" value={q} list="community-tags" maxLength={120} onChange={e=>setQ(e.target.value)} placeholder="Từ khóa, kinh nghiệm…"/></label><datalist id="community-tags">{suggestions.map(t=><option key={t} value={t}/>)}</datalist><label>Chủ đề<select name="category" value={category} onChange={e=>{location.hash=communityFeedHref(route,{category:e.target.value,page:1})}}><option value="">Tất cả chủ đề</option>{Object.entries(categories).map(([v,t])=><option key={v} value={v}>{t}</option>)}</select></label><label>Sắp xếp<select name="sort" value={sort} onChange={e=>{location.hash=communityFeedHref(route,{sort:e.target.value,page:1})}}><option value="newest">Mới nhất</option><option value="trending">Nhiều tương tác</option><option value="views">Nhiều lượt xem</option></select></label><button type="submit" disabled={busy}><MagnifyingGlass size={17} aria-hidden="true"/>Tìm</button></ValidatedForm>
          {trendingSearches.length>0&&<div className="chip-row social-trending-searches" role="group" aria-label="Từ khóa được tìm nhiều"><small>Tìm nhiều:</small>{trendingSearches.map(item=><button key={item.term} type="button" className="secondary" disabled={busy} onClick={()=>{setQ(item.term);location.hash=communityFeedHref(route,{q:item.term,page:1})}}>{item.term} ({item.count})</button>)}</div>}
          <CommunityTagFilter active={active} tags={communityFilterTags(route)} disabled={busy} onChange={tags=>{location.hash=communityFeedHref(route,{tags,page:1})}}/>
          {busy&&!feed&&<StaticLoading message="Đang tải hoặc lưu…"/>}
          {feed?.items.map(p=><PostCard key={p.id} post={p} selectedTags={communityFilterTags(route)} viewer={session?.userId} admin={admin} busy={busy} onToggle={toggle} onEdit={compose} onReport={()=>signed?setReportTarget({postId:p.id}):location.hash=signInReturnHref(location.hash)} onState={(action,reason)=>act(()=>api(`${base}/posts/${p.id}/state`,{method:'POST',...json({action,reason,rowVersion:p.rowVersion})}),'Đã cập nhật trạng thái bài viết.')}/>)}
          {!busy&&feed?.items.length===0&&<EmptyState icon={<Users size={32} aria-hidden="true"/>} title={tab==='following'?'Chưa có bài viết trong feed theo dõi':'Chưa có bài viết'} description={tab==='following'?'Tìm và theo dõi thành viên để xem bài của họ tại đây.':'Thử chủ đề khác hoặc bắt đầu chia sẻ trải nghiệm của bạn.'}/>}
          {feed&&<Pagination page={page} totalItems={feed.total} pageSize={12} busy={busy} ariaLabel="Phân trang cộng đồng" onPageChange={nextPage=>{location.hash=communityFeedHref(route,{page:nextPage})}}/>}
        </>:<LoginPrompt/>}
      </>}
      {liveUpdate&&!postId&&<button type="button" className="secondary social-refresh" onClick={()=>{setLiveUpdate(false);void load().catch(e=>setError((e as Error).message))}}>Có cập nhật mới · Làm mới bảng tin</button>}
    </section><aside className="social-right">
      <section className="card">
        {signed?<div className="social-identity" style={{borderTop:0,marginTop:0,paddingTop:0}}>{mine?<><Avatar person={mine}/><a href={link('person',mine.userId)}><strong>{mine.displayName}</strong><small>@{mine.handle}</small></a><button type="button" className="link-button" onClick={()=>setEditProfile(true)}>Sửa hồ sơ cộng đồng</button></>:profileReady&&<><p>Tạo hồ sơ cộng đồng riêng để viết bài và kết nối.</p><button type="button" className="secondary" onClick={()=>setEditProfile(true)}>Tạo hồ sơ cộng đồng</button></>}</div>:<p className="hint" style={{margin:0}}>Bạn có thể đọc bài đã duyệt. <a href={signInReturnHref(location.hash)}>Đăng nhập</a> để tương tác.</p>}
      </section>
      <People active={active} onError={setError}/><section className="card"><h2>Chia sẻ có trách nhiệm</h2><p>Viết từ trải nghiệm thật, tôn trọng người khác và giữ kín thông tin cá nhân trong CV.</p><p>Bài mới và nội dung sửa được quản trị viên duyệt trước khi công khai.</p></section></aside></div>
    <Dialog className="community-post-dialog" open={editor!==undefined} title={editor?'Chỉnh sửa bài viết':'Chia sẻ trải nghiệm'} busy={busy} onClose={()=>setEditor(undefined)}><PostEditor key={editor?.id??'new'} initial={editor??null} onSaving={setBusy} onCancel={()=>setEditor(undefined)} onSaved={p=>{setEditor(undefined);setMessage(p.status==='pending'?'Đã gửi bài chờ admin duyệt.':'Đã lưu bản nháp.');location.hash=link('tab','mine').slice(1);void load();}}/></Dialog>
    <Dialog open={editProfile} title={mine?'Hồ sơ cộng đồng':'Tạo hồ sơ cộng đồng'} busy={busy} onClose={()=>setEditProfile(false)}><ProfileEditor initial={mine} canShareCareer={!!canAuthor&&!admin} onSaving={setBusy} onSaved={p=>{setMine(p);setPerson(current=>current?.userId===p.userId?p:current);setEditProfile(false);setMessage('Đã lưu hồ sơ cộng đồng công khai.')}}/></Dialog>
    <Dialog open={!!reportTarget} title="Báo cáo nội dung cộng đồng" busy={busy} onClose={()=>setReportTarget(null)}><ValidatedForm onSubmit={e=>{e.preventDefault();void act(async()=>{await api(`${base}/reports`,{method:'POST',...json({...reportTarget,reason:reportReason})});setReportTarget(null);setReportReason('');},'Đã gửi báo cáo tới quản trị viên.')}}><label>Lý do báo cáo<textarea required minLength={3} maxLength={1000} value={reportReason} onChange={e=>setReportReason(e.target.value)}/></label><button type="submit" disabled={busy}>Gửi báo cáo</button></ValidatedForm></Dialog>
  </div>;
}
function LoginPrompt(){return <EmptyState title="Không gian dành cho thành viên" action={<a className="empty-state-cta" href={signInReturnHref(location.hash)}>Đăng nhập</a>} description="Đăng nhập để theo dõi, lưu bài, xem thông báo và quản lý bài viết."/>}
function PostCard({post:p,detail=false,moderating=false,selectedTags=[],viewer,admin,busy,onToggle,onEdit,onReport,onState}:{post:Post;detail?:boolean;moderating?:boolean;selectedTags?:string[];viewer?:string;admin:boolean;busy:boolean;onToggle:(p:Post,k:'vote'|'bookmark')=>Promise<void>;onEdit:(p:Post)=>void;onReport:()=>void;onState:(action:string,reason?:string)=>Promise<void>}){
  const [reason,setReason]=useState('');const [confirmDelete,setConfirmDelete]=useState(false);const owner=p.authorId===viewer;const match=communityTagMatch(p.tags,selectedTags);
  return <article className="card social-post"><header><a className="social-author" href={link('person',p.authorId)}><Avatar person={p}/><span><strong>{p.author}</strong><small>@{p.handle} · {date(p.publishedAt??p.createdAt)}</small></span></a><span className="badge">{categories[p.category]}</span></header>
    {p.status!=='published'&&<p className="badge">{states[p.status]}</p>}
    <h2>{detail?p.title:<a href={link('post',p.id)}>{p.title}</a>}</h2>{p.thumbnailId&&<img className="social-thumbnail" src={`${base}/images/${p.thumbnailId}`} loading="lazy" alt={`Ảnh minh họa cho ${p.title}`}/>}
    {detail?<CommunityMarkdown text={p.body??''}/>:<p className="social-summary">{p.summary||p.title}</p>}
    <div className="social-tags">{p.tags.map(t=><a key={t} className={match.tags.includes(t)?'is-tag-match':undefined} href={communityTagHref(t)}>#{t}</a>)}</div>
    {match.total>0&&<p className="social-tag-match-label">Khớp {match.percent}% ({match.tags.length}/{match.total} tag)</p>}
    {!moderating&&<footer className="social-actions"><button type="button" className="link-button" aria-pressed={p.voted} disabled={busy||p.status!=='published'} onClick={()=>void onToggle(p,'vote')}><Heart size={18} aria-hidden="true"/>{p.votes} thích</button><a href={link('post',p.id)}><ChatCircle size={18} aria-hidden="true"/>{p.comments} bình luận</a><button type="button" className="link-button" aria-pressed={p.bookmarked} disabled={busy||p.status!=='published'} onClick={()=>void onToggle(p,'bookmark')}><BookmarkSimple size={18} aria-hidden="true"/>{p.bookmarked?'Đã lưu':'Lưu bài'}</button><span>{p.views} lượt xem</span>{viewer&&p.status==='published'&&<button type="button" className="link-button" onClick={onReport} aria-label={`Báo cáo bài ${p.title}`}><Flag size={16} aria-hidden="true"/>Báo cáo</button>}</footer>}
    {owner&&p.status!=='deleted'&&<div className="row"><button type="button" className="secondary" onClick={()=>onEdit(p)}>Sửa bài</button>{['draft','hidden'].includes(p.status)&&<button type="button" disabled={busy} onClick={()=>void onState('submit')}>Gửi duyệt</button>}<button type="button" className="secondary" disabled={busy} onClick={()=>setConfirmDelete(true)}>Xóa bài</button></div>}
    {admin&&p.status!=='draft'&&<div className="social-moderation"><label>Lý do kiểm duyệt<input value={reason} maxLength={500} onChange={e=>setReason(e.target.value)}/></label><div className="row">{(p.status==='pending'?['approve','hide','delete']:p.status==='deleted'?['restore']:p.status==='published'?['hide','delete']:['delete']).map(action=><button type="button" className="secondary" key={action} disabled={busy||reason.trim().length<3} onClick={()=>void onState(action,reason)}>{{approve:'Duyệt đăng',hide:'Ẩn bài',delete:'Xóa bài',restore:'Khôi phục về đã ẩn'}[action]}</button>)}</div></div>}
    <ConfirmDialog open={confirmDelete} title="Xóa bài viết" message="Chuyển bài viết này vào thùng rác?" confirmLabel="Xóa bài" danger busy={busy} onClose={()=>setConfirmDelete(false)} onConfirm={async ()=>{setConfirmDelete(false);await onState('delete');}}/>
  </article>;
}
function People({active,onError}:{active:boolean;onError:(s:string)=>void}){
  const [q,setQ]=useState('');const [people,setPeople]=useState<Person[]>([]);
  useEffect(()=>{if(!active)return;let disposed=false;const timer=setTimeout(()=>{void api<Person[]>(`${base}/people?q=${encodeURIComponent(q)}`).then(x=>{if(!disposed)setPeople(x)}).catch(e=>{if(!disposed)onError((e as Error).message)});},300);return()=>{disposed=true;clearTimeout(timer)};},[active,q,onError]);
  return <section className="card social-people"><h2>Kết nối đồng nghiệp</h2><label>Tìm thành viên<input name="memberQuery" maxLength={80} value={q} onChange={e=>setQ(e.target.value)} placeholder="Tên hoặc @handle"/></label>{people.map(p=><a className="social-author" key={p.userId} href={link('person',p.userId)}><Avatar person={p}/><span><strong>{p.displayName}</strong><small>@{p.handle}</small></span></a>)}{people.length===0&&<p className="hint">Chưa có thành viên phù hợp.</p>}</section>
}
function Connections({person}:{person:Profile}){
  const [open,setOpen]=useState(false);const [direction,setDirection]=useState('followers');const [page,setPage]=useState(1);const [value,setValue]=useState<{items:Person[];total:number}>();const [error,setError]=useState('');
  useEffect(()=>{if(!open)return;let disposed=false;void api<{items:Person[];total:number}>(`${base}/profiles/${person.userId}/connections?direction=${direction}&page=${page}`).then(x=>{if(!disposed)setValue(x)}).catch(e=>setError((e as Error).message));return()=>{disposed=true};},[open,direction,page,person.userId]);
  return <><button type="button" className="link-button" onClick={()=>setOpen(true)}>Xem kết nối</button><Dialog open={open} title={`Kết nối của ${person.displayName}`} onClose={()=>setOpen(false)}><label>Danh sách<select value={direction} onChange={e=>{setDirection(e.target.value);setPage(1)}}><option value="followers">Người theo dõi</option><option value="following">Đang theo dõi</option></select></label>{error&&<Alert tone="error">{error}</Alert>}{value?.items.map(p=><a className="social-author" href={link('person',p.userId)} key={p.userId}><Avatar person={p}/><span>{p.displayName} · @{p.handle}</span></a>)}{value&&<Pagination page={page} totalItems={value.total} pageSize={20} onPageChange={setPage}/>}</Dialog></>;
}
function ImageInput({
  value,
  onChange,
  onError,
  label = 'Ảnh đại diện',
  optional = false,
}: {
  value: string | null;
  onChange: (v: string | null) => void;
  onError: (v: string) => void;
  label?: string;
  optional?: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const [dragging, setDragging] = useState(false);
  const [localError, setLocalError] = useState('');

  function fail(msg: string) {
    setLocalError(msg);
  }

  function uploadFile(file: File | undefined) {
    if (!file) return;
    const validType = ['image/png', 'image/jpeg'].includes(file.type)
      || (!file.type && /\.(png|jpe?g)$/i.test(file.name));
    if (!validType) {
      fail('Chỉ hỗ trợ ảnh định dạng PNG hoặc JPG.');
      return;
    }
    if (file.size > 2 * 1024 * 1024) {
      fail('Ảnh vượt quá dung lượng tối đa 2 MB.');
      return;
    }
    const form = new FormData();
    form.append('file', file);
    setBusy(true);
    setLocalError('');
    onError('');
    void api<{ id: string }>(`${base}/images`, { method: 'POST', body: form })
      .then(x => {
        if (!x?.id) {
          fail('Máy chủ không trả về mã ảnh hợp lệ.');
          return;
        }
        onChange(x.id);
      })
      .catch(x => fail((x as Error).message))
      .finally(() => setBusy(false));
  }

  return (
    <div className="post-field">
      <div className="post-field-head">
        <span>
          {label}
          {optional && <span className="field-optional"> (Không bắt buộc)</span>}
        </span>
      </div>
      {value ? (
        <div className="social-cover-preview">
          <div className="social-cover-preview-info">
            <img src={`${base}/images/${value}`} alt={`${label} đang chọn`} />
            <span className="post-field-hint">Đã tải lên {label.toLowerCase()} (PNG/JPEG, ≤ 2 MB)</span>
          </div>
          <div className="social-cover-preview-actions">
            <label>
              {busy ? 'Đang tải…' : 'Đổi ảnh'}
              <input
                type="file"
                accept="image/png,image/jpeg"
                disabled={busy}
                onChange={e => {
                  const file = e.currentTarget.files?.[0];
                  e.currentTarget.value = '';
                  uploadFile(file);
                }}
              />
            </label>
            <button
              type="button"
              className="secondary"
              disabled={busy}
              onClick={() => {
                setLocalError('');
                onChange(null);
              }}
            >
              Xóa ảnh
            </button>
          </div>
        </div>
      ) : (
        <div
          className={`social-cover-dropzone${dragging ? ' is-dragging' : ''}`}
          onDragOver={e => { if (!busy) { e.preventDefault(); setDragging(true); } }}
          onDragLeave={e => { if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setDragging(false); }}
          onDrop={e => { e.preventDefault(); setDragging(false); if (!busy) uploadFile(e.dataTransfer.files?.[0]); }}
        >
          <input
            type="file"
            accept="image/png,image/jpeg"
            disabled={busy}
            aria-label={label}
            onChange={e => {
              const file = e.currentTarget.files?.[0];
              e.currentTarget.value = '';
              uploadFile(file);
            }}
          />
          <CloudArrowUp size={22} className="social-cover-icon" aria-hidden="true" />
          <div className="social-cover-text">
            <strong>{busy ? 'Đang tải ảnh lên…' : 'Nhấn để chọn ảnh hoặc kéo thả vào đây'}</strong>
            <small>Định dạng PNG, JPG · Tối đa 2 MB</small>
          </div>
        </div>
      )}
      {localError && <p role="alert" className="error post-inline-error">{localError}</p>}
    </div>
  );
}
function ProfileEditor({initial,canShareCareer,onSaved,onSaving}:{initial:Profile|null;canShareCareer:boolean;onSaving:(v:boolean)=>void;onSaved:(p:Profile)=>void}){
  const [showCareer,setShowCareer]=useState(initial?.showCareer??false);const [handle,setHandle]=useState(initial?.handle??'');const [displayName,setDisplayName]=useState(initial?.displayName??'');const [bio,setBio]=useState(initial?.bio??'');const [avatarId,setAvatarId]=useState<string|null>(initial?.avatarId??null);const [mentions,setMentions]=useState(initial?.allowMentions??true);const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [images,setImages]=useState<{id:string;createdAt:string}[]>([]);
  async function save(e:FormEvent){e.preventDefault();setBusy(true);onSaving(true);setError('');try{onSaved(await api<Profile>(`${base}/me`,{method:'PUT',...json({handle,displayName,bio,avatarId,allowMentions:mentions,showCareer,rowVersion:initial?.rowVersion??0})}));}catch(x){setError((x as Error).message)}finally{setBusy(false);onSaving(false)}}
  return <><p>Đây là hồ sơ công khai của cộng đồng. Thông tin liên hệ, CV và quyền chia sẻ hồ sơ tuyển dụng được quản lý riêng.</p><ValidatedForm onSubmit={e=>void save(e)}><label>Handle công khai<input required pattern="[a-z][a-z0-9_]{2,23}" minLength={3} maxLength={24} value={handle} disabled={!!initial} onChange={e=>setHandle(e.target.value.toLowerCase())}/><small>3–24 ký tự: chữ thường, số, dấu gạch dưới; bắt đầu bằng chữ. Handle giữ ổn định sau khi tạo.</small></label><label>Tên hiển thị<input required minLength={2} maxLength={100} value={displayName} onChange={e=>setDisplayName(e.target.value)}/></label><label>Giới thiệu<textarea maxLength={1000} value={bio} onChange={e=>setBio(e.target.value)}/></label><ImageInput value={avatarId} onChange={setAvatarId} onError={setError}/>{canShareCareer&&<div className="social-career-consent"><label className="check-option"><input type="checkbox" checked={showCareer} onChange={e=>setShowCareer(e.target.checked)}/>Công khai thẻ nghề nghiệp trên hồ sơ tác giả</label><p className="hint">Mọi người, kể cả khách, có thể xem. Ứng viên: chức danh, vị trí mong muốn, địa điểm, số năm kinh nghiệm và kỹ năng từ Hồ sơ &amp; Quyền chia sẻ. Nhà tuyển dụng: thông tin công ty chính đã xác minh. Không công khai thông tin liên hệ hoặc CV. Bạn có thể tắt bất cứ lúc nào.</p></div>}<label className="check-option"><input type="checkbox" checked={mentions} onChange={e=>setMentions(e.target.checked)}/>Nhận thông báo khi được @nhắc tên</label>{error&&<p role="alert" className="error">{error}</p>}<button type="submit" disabled={busy}>{busy?'Đang lưu…':'Lưu hồ sơ cộng đồng'}</button></ValidatedForm><details><summary>Quản lý ảnh đã tải</summary><button type="button" className="secondary" onClick={()=>void api<typeof images>(`${base}/images/mine`).then(setImages).catch(e=>setError((e as Error).message))}>Tải danh sách ảnh</button><div className="social-media-grid">{images.map(x=><div key={x.id}><img src={`${base}/images/${x.id}`} alt="Ảnh của bạn" loading="lazy"/><button type="button" className="secondary" onClick={()=>void api(`${base}/images/${x.id}`,{method:'DELETE'}).then(()=>setImages(current=>current.filter(i=>i.id!==x.id))).catch(e=>setError((e as Error).message))}>Xóa ảnh chưa dùng</button></div>)}</div></details></>;
}
function MentionSuggestions({text,onPick}:{text:string;onPick:(handle:string)=>void}){
  const term=text.match(/(?:^|\s)@([a-z0-9_]{0,23})$/i)?.[1];const [people,setPeople]=useState<Person[]>([]);
  useEffect(()=>{if(term===undefined)return;let disposed=false;const timer=setTimeout(()=>{void api<Person[]>(`${base}/people?q=${encodeURIComponent(term)}`).then(x=>{if(!disposed)setPeople(x)}).catch(()=>{});},200);return()=>{disposed=true;clearTimeout(timer)};},[term]);
  return term!==undefined?<div className="social-mention-options" role="group" aria-label="Gợi ý nhắc tên">{people.slice(0,5).map(p=><button type="button" className="secondary" key={p.userId} onClick={()=>onPick(p.handle)}>@{p.handle} · {p.displayName}</button>)}</div>:null;
}
function PostEditor({
  initial,
  onSaved,
  onSaving,
  onCancel,
}: {
  initial: Post | null;
  onSaving: (v: boolean) => void;
  onSaved: (p: Post) => void;
  onCancel: () => void;
}) {
  const [title, setTitle] = useState(initial?.title ?? '');
  const [summary, setSummary] = useState(initial?.summary ?? '');
  const [body, setBody] = useState(initial?.body ?? '');
  const [category, setCategory] = useState(initial?.category ?? 'career');
  const [tagList, setTagList] = useState<string[]>(initial?.tags ?? []);
  const [tagDraft, setTagDraft] = useState('');
  const [thumbnailId, setThumbnailId] = useState(initial?.thumbnailId ?? null);
  const [version, setVersion] = useState(initial?.rowVersion ?? 0);
  const [preview, setPreview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [ready, setReady] = useState(!initial || initial.body !== null);
  const [error, setError] = useState('');
  const textRef = useRef<HTMLTextAreaElement>(null);
  const inlineImageRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!initial || initial.body !== null) return;
    let disposed = false;
    void api<Post>(`${base}/posts/${initial.id}`)
      .then(p => {
        if (!disposed) {
          setBody(p.body ?? '');
          setVersion(p.rowVersion);
          setReady(true);
        }
      })
      .catch(e => setError((e as Error).message));
    return () => { disposed = true; };
  }, [initial]);

  function addTagsFromRaw(raw: string) {
    const parts = raw.split(',').map(t => t.trim().slice(0, 40)).filter(Boolean);
    if (!parts.length) return;
    setTagList(current => {
      const next = [...current];
      for (const p of parts) {
        if (next.length >= 8) break;
        if (!next.some(t => t.toLowerCase() === p.toLowerCase())) next.push(p);
      }
      return next;
    });
  }

  function format(before: string, after = '') {
    const el = textRef.current;
    if (!el) return;
    const start = el.selectionStart;
    const end = el.selectionEnd;
    setBody(body.slice(0, start) + before + body.slice(start, end) + after + body.slice(end));
    requestAnimationFrame(() => {
      el.focus();
      el.setSelectionRange(start + before.length, end + before.length);
    });
  }

  async function insertInlineImage(file: File | undefined) {
    if (!file) return;
    if (file.size > 2 * 1024 * 1024 || !['image/png', 'image/jpeg'].includes(file.type)) {
      setError('Ảnh chèn vào bài phải là PNG hoặc JPEG, tối đa 2 MB.');
      return;
    }
    setUploadingImage(true);
    setError('');
    try {
      const form = new FormData();
      form.append('file', file);
      const res = await api<{ id: string }>(`${base}/images`, { method: 'POST', body: form });
      format(`\n![Ảnh minh họa](${base}/images/${res.id})\n`);
    } catch (x) {
      setError((x as Error).message);
    } finally {
      setUploadingImage(false);
      if (inlineImageRef.current) inlineImageRef.current.value = '';
    }
  }

  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const submitter = (e.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null;
    const pendingTags = tagDraft.split(',').map(t => t.trim().slice(0, 40)).filter(Boolean);
    const mergedTags = [...tagList];
    for (const p of pendingTags) {
      if (mergedTags.length >= 8) break;
      if (!mergedTags.some(t => t.toLowerCase() === p.toLowerCase())) mergedTags.push(p);
    }
    setBusy(true);
    onSaving(true);
    setError('');
    try {
      onSaved(
        await api<Post>(`${base}/posts${initial ? '/' + initial.id : ''}`, {
          method: initial ? 'PUT' : 'POST',
          ...json({
            title,
            summary,
            body,
            category,
            tags: mergedTags,
            thumbnailId,
            submit: submitter?.value !== 'draft',
            rowVersion: version,
          }),
        })
      );
    } catch (x) {
      setError((x as Error).message);
    } finally {
      setBusy(false);
      onSaving(false);
    }
  }

  return (
    <ValidatedForm className="post-editor-form" onSubmit={e => void save(e)}>
      <div className="post-editor-body">
        {initial?.status === 'published' && (
          <p className="hint m-0">Lưu thay đổi sẽ đưa bài về nháp hoặc chờ duyệt; bản sửa chưa được công khai ngay.</p>
        )}

        <label className="post-field">
          <span className="post-field-head">
            <span>Tiêu đề<span className="field-required" aria-hidden="true">*</span></span>
          </span>
          <input
            required
            minLength={5}
            maxLength={200}
            value={title}
            placeholder="VD: Kinh nghiệm phỏng vấn Frontend Fresher sau 3 tháng ôn..."
            onChange={e => setTitle(e.target.value)}
          />
        </label>

        <label className="post-field">
          <span className="post-field-head">
            <span>Tóm tắt<span className="field-optional"> (Không bắt buộc)</span></span>
            <span className="field-counter">{summary.length}/500</span>
          </span>
          <textarea
            className="post-summary-input"
            rows={2}
            maxLength={500}
            value={summary}
            placeholder="Mô tả ngắn 1–2 câu về nội dung bài viết của bạn..."
            onChange={e => setSummary(e.target.value)}
          />
        </label>

        <div className="post-meta-grid">
          <label className="post-field">
            <span className="post-field-head">
              <span>Chủ đề<span className="field-required" aria-hidden="true">*</span></span>
            </span>
            <select value={category} onChange={e => setCategory(e.target.value)}>
              {Object.entries(categories).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
            </select>
          </label>

          <div className="post-field">
            <label className="post-field-head" htmlFor="post-tag-input">
              <span>Thẻ (Tags)</span>
              <span className="field-counter">{tagList.length}/8</span>
            </label>
            <div className="post-tag-box">
              {tagList.map(tag => (
                <span className="post-tag-chip" key={tag}>
                  <span>{tag}</span>
                  <button
                    type="button"
                    aria-label={`Xóa thẻ ${tag}`}
                    onClick={() => setTagList(current => current.filter(t => t !== tag))}
                  >
                    <X size={12} aria-hidden="true" />
                  </button>
                </span>
              ))}
              <input
                id="post-tag-input"
                value={tagDraft}
                maxLength={40}
                disabled={tagList.length >= 8}
                placeholder={tagList.length >= 8 ? 'Đã đủ 8 thẻ' : 'React, phỏng vấn…'}
                onChange={e => {
                  const val = e.target.value;
                  if (val.includes(',')) {
                    addTagsFromRaw(val);
                    setTagDraft('');
                  } else {
                    setTagDraft(val);
                  }
                }}
                onKeyDown={e => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    addTagsFromRaw(tagDraft);
                    setTagDraft('');
                  } else if (e.key === 'Backspace' && !tagDraft && tagList.length) {
                    setTagList(current => current.slice(0, -1));
                  }
                }}
              />
            </div>
            <span className="post-field-hint">Nhấn Enter hoặc dấu phẩy để thêm thẻ (tối đa 8 thẻ).</span>
          </div>
        </div>

        <ImageInput label="Ảnh bìa" optional value={thumbnailId} onChange={setThumbnailId} onError={setError} />

        <div className="post-field">
          <div className="post-field-head">
            <span>Nội dung<span className="field-required" aria-hidden="true">*</span></span>
          </div>
          <div className="post-markdown-box">
            <div className="post-markdown-toolbar">
              <div className="post-mode-tabs" role="group" aria-label="Chế độ soạn thảo">
                <button type="button" aria-pressed={!preview} onClick={() => setPreview(false)}>Viết</button>
                <button type="button" aria-pressed={preview} onClick={() => setPreview(true)}>Xem trước</button>
              </div>
              {!preview && (
                <div className="post-toolbar-icons" role="group" aria-label="Định dạng bài viết">
                  <button type="button" title="In đậm" aria-label="In đậm" onClick={() => format('**', '**')}><TextB size={16} aria-hidden="true" /></button>
                  <button type="button" title="In nghiêng" aria-label="In nghiêng" onClick={() => format('*', '*')}><TextItalic size={16} aria-hidden="true" /></button>
                  <button type="button" title="Gạch chân" aria-label="Gạch chân" onClick={() => format('++', '++')}><TextUnderline size={16} aria-hidden="true" /></button>
                  <button type="button" title="Gạch ngang" aria-label="Gạch ngang" onClick={() => format('~~', '~~')}><TextStrikethrough size={16} aria-hidden="true" /></button>
                  <span className="post-toolbar-sep" aria-hidden="true" />
                  <button type="button" title="Tiêu đề mục" aria-label="Tiêu đề mục" onClick={() => format('\n## ')}><TextH size={16} aria-hidden="true" /></button>
                  <button type="button" title="Danh sách" aria-label="Danh sách" onClick={() => format('\n- ')}><ListBullets size={16} aria-hidden="true" /></button>
                  <button type="button" title="Danh sách số" aria-label="Danh sách số" onClick={() => format('\n1. ')}><ListNumbers size={16} aria-hidden="true" /></button>
                  <button type="button" title="Trích dẫn" aria-label="Trích dẫn" onClick={() => format('\n> ')}><Quotes size={16} aria-hidden="true" /></button>
                  <span className="post-toolbar-sep" aria-hidden="true" />
                  <button type="button" title="Code nội dòng" aria-label="Code" onClick={() => format('`', '`')}><Code size={16} aria-hidden="true" /></button>
                  <button type="button" title="Khối code" aria-label="Code block" onClick={() => format('\n```\n', '\n```\n')}><CodeBlock size={16} aria-hidden="true" /></button>
                  <button type="button" title="Chèn liên kết" aria-label="Liên kết" onClick={() => format('[', '](https://example.com)')}><LinkSimple size={16} aria-hidden="true" /></button>
                  <button type="button" title="Chèn ảnh" aria-label="Chèn ảnh" disabled={uploadingImage || !ready} onClick={() => inlineImageRef.current?.click()}><ImageSquare size={16} aria-hidden="true" /></button>
                </div>
              )}
              <input ref={inlineImageRef} type="file" accept="image/png,image/jpeg" hidden onChange={e => void insertInlineImage(e.target.files?.[0])} />
            </div>
            {preview ? (
              <div className="post-markdown-preview">
                {body.trim() ? <CommunityMarkdown text={body} /> : <p className="hint m-0">Chưa có nội dung để xem trước.</p>}
              </div>
            ) : (
              <textarea
                className="post-markdown-textarea"
                ref={textRef}
                required
                minLength={20}
                maxLength={30000}
                value={body}
                disabled={!ready}
                placeholder="Chia sẻ câu chuyện, bài học hoặc góc nhìn của bạn..."
                onChange={e => setBody(e.target.value)}
              />
            )}
            <div className="post-markdown-footer">
              Gõ @handle để nhắc tên • Hỗ trợ định dạng cơ bản • HTML được hiển thị như văn bản
            </div>
          </div>
          <MentionSuggestions text={body} onPick={h => setBody(body.replace(/@([a-z0-9_]{0,23})$/i, '@' + h + ' '))} />
        </div>
      </div>

      {error && <p role="alert" className="error post-editor-error">{error}</p>}

      <div className="post-editor-footer">
        <button type="button" className="secondary" disabled={busy} onClick={onCancel}>Hủy</button>
        <button type="submit" value="draft" className="secondary" disabled={busy || !ready}>Lưu nháp</button>
        <button type="submit" value="submit" disabled={busy || !ready}>
          {busy && <Spinner size={15} />}
          {busy ? 'Đang gửi…' : 'Đăng bài viết'}
        </button>
      </div>
    </ValidatedForm>
  );
}
function Comments({postId,viewer,admin,ensureProfile,onError,onReport}:{postId:string;viewer?:string;admin:boolean;ensureProfile:()=>boolean;onError:(s:string)=>void;onReport:(id:string)=>void}){
  const [page,setPage]=useState(1);const [data,setData]=useState<Page<Comment>>();const [body,setBody]=useState('');const [reply,setReply]=useState<Comment|null>(null);const [editing,setEditing]=useState<Comment|null>(null);const [confirmDeleteComment,setConfirmDeleteComment]=useState<Comment|null>(null);const [busy,setBusy]=useState(false);const [refresh,setRefresh]=useState(0);
  const load=useCallback(async()=>setData(await api<Page<Comment>>(`${base}/posts/${postId}/comments?page=${page}`)),[postId,page]);
  useEffect(()=>{let disposed=false;void api<Page<Comment>>(`${base}/posts/${postId}/comments?page=${page}`).then(x=>{if(!disposed)setData(x)}).catch(e=>onError((e as Error).message));return()=>{disposed=true};},[postId,page,refresh,onError]);
  useEffect(()=>{const update=()=>void load().catch(()=>{});window.addEventListener('community:update',update);return()=>window.removeEventListener('community:update',update);},[load]);
  async function submit(e:FormEvent){e.preventDefault();if(!ensureProfile())return;setBusy(true);try{await api(`${base}/posts/${postId}/comments${editing?'/'+editing.id:''}`,{method:editing?'PUT':'POST',...json({body,parentId:editing?.parentId??reply?.id??null,rowVersion:editing?.rowVersion??0})});setBody('');setReply(null);setEditing(null);setRefresh(n=>n+1);window.dispatchEvent(new CustomEvent('community:update'));}catch(x){onError((x as Error).message)}finally{setBusy(false)}}
  return <section className="card social-comments"><h2>Bình luận</h2>{viewer?<ValidatedForm onSubmit={e=>void submit(e)}>{(reply||editing)&&<div className="row"><p>{editing?'Đang sửa bình luận':'Trả lời @'+reply?.handle}</p><button type="button" className="link-button" onClick={()=>{setEditing(null);setReply(null);setBody('')}}>Hủy</button></div>}<label>{editing?'Chỉnh sửa bình luận':'Viết bình luận'}<textarea required maxLength={4000} value={body} onChange={e=>setBody(e.target.value)}/></label><MentionSuggestions text={body} onPick={h=>setBody(body.replace(/@([a-z0-9_]{0,23})$/i,'@'+h+' '))}/><button type="submit" disabled={busy}>{busy?'Đang gửi…':editing?'Lưu bình luận':'Gửi bình luận'}</button></ValidatedForm>:<p><a href={signInReturnHref(location.hash)}>Đăng nhập</a> để bình luận.</p>}
    {data?.items.map(c=><article key={c.id} className={`social-comment ${c.parentId?'is-reply':''}`}><a className="social-author" href={link('person',c.authorId)}><Avatar person={c}/><span><strong>{c.author}</strong><small>@{c.handle} · {date(c.createdAt)}</small></span></a>{c.parentId&&<small>Phản hồi bình luận trước</small>}<CommunityMarkdown text={c.body}/>{viewer&&!c.isDeleted&&<div className="row">{!c.parentId&&<button type="button" className="link-button" onClick={()=>{if(ensureProfile()){setReply(c);setEditing(null);setBody('');document.querySelector<HTMLTextAreaElement>('.social-comments textarea')?.focus()}}}>Trả lời</button>}{viewer===c.authorId&&<button type="button" className="link-button" onClick={()=>{setEditing(c);setReply(null);setBody(c.body);document.querySelector<HTMLTextAreaElement>('.social-comments textarea')?.focus()}}>Sửa</button>}{(viewer===c.authorId||admin)&&<button type="button" className="link-button" disabled={busy} onClick={()=>setConfirmDeleteComment(c)}>Xóa</button>}<button type="button" className="link-button" onClick={()=>onReport(c.id)}>Báo cáo bình luận</button></div>}</article>)}{data&&<Pagination page={page} totalItems={data.total} pageSize={30} busy={busy} onPageChange={setPage}/>}
    <ConfirmDialog open={!!confirmDeleteComment} title="Xóa bình luận" message="Xóa nội dung bình luận này?" confirmLabel="Xóa" danger busy={busy} onClose={()=>setConfirmDeleteComment(null)} onConfirm={async ()=>{if(!confirmDeleteComment)return;setBusy(true);try{await api(`${base}/posts/${postId}/comments/${confirmDeleteComment.id}?rowVersion=${confirmDeleteComment.rowVersion}`,{method:'DELETE'});setConfirmDeleteComment(null);setRefresh(n=>n+1);}catch(e){onError((e as Error).message)}finally{setBusy(false)}}}/>
  </section>;
}
function CommunityNotifications({onError}:{onError:(s:string)=>void}){
  const [unread,setUnread]=useState(false);const [page,setPage]=useState(1);const [data,setData]=useState<{items:Notice[];total:number;unread:number}>();const [refresh,setRefresh]=useState(0);
  useEffect(()=>{let disposed=false;void api<typeof data>(`${base}/notifications?unread=${unread}&page=${page}`).then(x=>{if(!disposed)setData(x)}).catch(e=>onError((e as Error).message));return()=>{disposed=true};},[unread,page,refresh,onError]);
  useEffect(()=>{const update=()=>setRefresh(n=>n+1);window.addEventListener('community:update',update);return()=>window.removeEventListener('community:update',update);},[]);
  async function read(id?:string){try{await api(`${base}/notifications/${id?`${id}/read`:'read-all'}`,{method:'PUT'});setRefresh(n=>n+1);window.dispatchEvent(new CustomEvent('community:update'));}catch(e){onError((e as Error).message)}}
  return <section className="card"><div className="section-heading"><div><h2>Thông báo cộng đồng</h2><p>{data?.unread??0} chưa đọc</p></div><button type="button" className="secondary" onClick={()=>void read()}>Đánh dấu tất cả đã đọc</button></div><div className="row notification-filters"><label>Trạng thái thông báo<select value={unread?'unread':'all'} onChange={e=>{setUnread(e.target.value==='unread');setPage(1)}}><option value="all">Tất cả</option><option value="unread">Chưa đọc</option></select></label></div>{data?.items.map(n=><article className={`social-notice ${n.readAt?'is-read':'notification-unread'}`} key={n.id}><div><strong>{n.message}</strong><small>{date(n.createdAt)}</small>{n.postId?n.available?<a href={link('post',n.postId)} onClick={()=>void read(n.id)}>Mở bài viết</a>:<p className="hint">Nội dung không còn công khai hoặc đã được gỡ.</p>:n.kind==='follow'&&<a href={link('person',n.actorId)}>Xem người theo dõi</a>}</div>{!n.readAt&&<button type="button" className="secondary" onClick={()=>void read(n.id)}>Đánh dấu đã đọc</button>}</article>)}{data?.items.length===0&&<EmptyState title="Chưa có thông báo trong bộ lọc này."/>}{data&&<Pagination page={page} totalItems={data.total} pageSize={20} onPageChange={setPage}/>}</section>;
}
function CommunityModeration({onError,onChanged}:{onError:(s:string)=>void;onChanged:()=>void}){
  const readTab=()=>new URLSearchParams(location.hash.split('?')[1]??'').get('review')==='reports'?'reports':'posts';
  const [tab,setTab]=useState(readTab);const [status,setStatus]=useState('pending');const [page,setPage]=useState(1);const [posts,setPosts]=useState<{items:Post[];total:number}>();const [stats,setStats]=useState<{posts:number;pending:number;comments:number;members:number;reports:number}>();const [busy,setBusy]=useState(false);const [refresh,setRefresh]=useState(0);
  useEffect(()=>{const update=()=>setTab(readTab());window.addEventListener('hashchange',update);return()=>window.removeEventListener('hashchange',update)},[]);
  useEffect(()=>{let disposed=false;void api<typeof stats>(`${base}/admin/stats`).then(x=>{if(!disposed)setStats(x)}).catch(e=>onError((e as Error).message));return()=>{disposed=true}},[refresh,onError]);
  useEffect(()=>{if(tab!=='posts')return;let disposed=false;void api<{items:Post[];total:number}>(`${base}/admin/posts?status=${status}&page=${page}`).then(x=>{if(!disposed)setPosts(x)}).catch(e=>onError((e as Error).message));return()=>{disposed=true}},[tab,status,page,refresh,onError]);
  useEffect(()=>{const update=()=>setRefresh(n=>n+1);window.addEventListener('community:update',update);return()=>window.removeEventListener('community:update',update);},[]);
  async function act(path:string,action:string,rowVersion:number,reason?:string){setBusy(true);try{await api(path,{method:'POST',...json({action,reason,rowVersion})});setRefresh(n=>n+1);onChanged();}catch(e){onError((e as Error).message)}finally{setBusy(false)}}
  return <section className="social-admin"><h2>Kiểm duyệt cộng đồng</h2>{stats&&<p>{stats.members} thành viên · {stats.posts} bài · {stats.pending} chờ duyệt · {stats.comments} bình luận · {stats.reports} báo cáo cần xử lý</p>}<nav className="row" aria-label="Kiểm duyệt cộng đồng"><button type="button" className={tab==='posts'?'':'secondary'} aria-pressed={tab==='posts'} onClick={()=>{location.hash='community?tab=moderation';setPage(1)}}>Bài viết</button><button type="button" className={tab==='reports'?'':'secondary'} aria-pressed={tab==='reports'} onClick={()=>{location.hash='community?tab=moderation&review=reports'}}>Báo cáo</button><button type="button" className="secondary" disabled={busy} onClick={()=>{setRefresh(n=>n+1);window.dispatchEvent(new CustomEvent('community:reports-refresh'))}}>Làm mới danh sách</button><a href="#administration">Quản lý và khóa tài khoản</a></nav>{tab==='posts'?<><label>Trạng thái<select value={status} onChange={e=>{setStatus(e.target.value);setPage(1)}}>{['pending','published','hidden','deleted'].map(s=><option value={s} key={s}>{states[s]}</option>)}</select></label>{posts?.items.map(p=><PostCard key={p.id} post={p} detail moderating admin busy={busy} onToggle={async()=>{}} onEdit={()=>{}} onReport={()=>{}} onState={(a,r)=>act(`${base}/posts/${p.id}/state`,a,p.rowVersion,r)}/>)}{posts&&<Pagination page={page} totalItems={posts.total} pageSize={20} busy={busy} onPageChange={setPage}/>}</>:<CommunityReportModeration onChanged={onChanged}/>}</section>;
}
