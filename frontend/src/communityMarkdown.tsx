import { Fragment, type ReactNode } from 'react';

const internalImageUrl = /^\/api\/community\/images\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function inline(text: string): ReactNode[] {
  const parts: ReactNode[] = []; let start = 0;
  const tokens = /`([^`\n]+)`|\*\*([^*\n]+)\*\*|\*([^*\n]+)\*|~~([^~\n]+)~~|\+\+([^+\n]+)\+\+|!\[([^\]\n]{0,200})\]\(([^)\s]{1,2048})\)|\[([^\]\n]{1,200})\]\(([^)\s]{1,2048})\)|(?<![\w@])@([a-z][a-z0-9_]{2,23})\b/gi;
  for (const match of text.matchAll(tokens)) {
    const index = match.index; parts.push(text.slice(start, index)); const key = `${index}-${start}`;
    if (match[1]) parts.push(<code key={key}>{match[1]}</code>);
    else if (match[2]) parts.push(<strong key={key}>{match[2]}</strong>);
    else if (match[3]) parts.push(<em key={key}>{match[3]}</em>);
    else if (match[4]) parts.push(<del key={key}>{match[4]}</del>);
    else if (match[5]) parts.push(<u key={key}>{match[5]}</u>);
    else if (match[6] !== undefined) {
      parts.push(internalImageUrl.test(match[7])
        ? <img key={key} className="community-inline-image" src={match[7]} alt={match[6] || 'Ảnh minh họa bài viết'} loading="lazy"/>
        : match[0]);
    } else if (match[8]) {
      let safe = false; try { safe = ['https:', 'http:'].includes(new URL(match[9]).protocol); } catch { /* Render invalid links as text. */ }
      parts.push(safe ? <a key={key} href={match[9]} target="_blank" rel="noopener noreferrer">{match[8]}</a> : match[0]);
    } else parts.push(<a key={key} href={`#community?handle=${encodeURIComponent(match[10].toLowerCase())}`}>@{match[10]}</a>);
    start = index + match[0].length;
  }
  parts.push(text.slice(start)); return parts;
}
/** Raw HTML, external images and executable URLs are always rendered as text. */
export function CommunityMarkdown({ text }: { text: string }) {
  const lines = text.split('\n');
  const nodes: ReactNode[] = [];
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line.startsWith('```')) {
      const lang = line.slice(3).trim().replace(/[^a-z0-9_-]/gi, '');
      const codeLines: string[] = [];
      let closed = false;
      for (let j = i + 1; j < lines.length; j++) {
        if (lines[j].startsWith('```')) { i = j; closed = true; break; }
        codeLines.push(lines[j]);
      }
      if (!closed) i = lines.length - 1;
      nodes.push(<pre key={`code-${i}`} className="community-code-block"><code className={lang ? `language-${lang}` : undefined}>{codeLines.join('\n')}</code></pre>);
      continue;
    }
    const ordered = line.match(/^(\d+)\.\s+(.*)$/);
    if (ordered) {
      const items: { num: string; body: string }[] = [{ num: ordered[1], body: ordered[2] }];
      while (i + 1 < lines.length) {
        const next = lines[i + 1].match(/^(\d+)\.\s+(.*)$/);
        if (!next) break;
        items.push({ num: next[1], body: next[2] });
        i++;
      }
      nodes.push(<ol key={`ol-${i}`} start={Number(items[0].num)} className="community-ordered">{items.map((item, idx) => <li key={idx}>{inline(item.body)}</li>)}</ol>);
      continue;
    }
    if (line.startsWith('### ')) nodes.push(<h5 key={i}>{inline(line.slice(4))}</h5>);
    else if (line.startsWith('## ')) nodes.push(<h4 key={i}>{inline(line.slice(3))}</h4>);
    else if (line.startsWith('# ')) nodes.push(<h3 key={i}>{inline(line.slice(2))}</h3>);
    else if (line.startsWith('> ')) nodes.push(<blockquote key={i}>{inline(line.slice(2))}</blockquote>);
    else if (line.startsWith('- ')) nodes.push(<p className="community-bullet" key={i}><span aria-hidden="true">• </span>{inline(line.slice(2))}</p>);
    else nodes.push(line ? <p key={i}>{inline(line)}</p> : <Fragment key={i}><br/></Fragment>);
  }
  return <div className="community-markdown">{nodes}</div>;
}
