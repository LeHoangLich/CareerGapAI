import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { CommunityMarkdown } from './communityMarkdown';

const render = (text: string) => renderToStaticMarkup(<CommunityMarkdown text={text}/>);
describe('community text rendering at the trust boundary', () => {
  it('escapes raw HTML and rejects executable URLs and embedded images', () => {
    const html = render('<script>alert(1)</script>\n<img src=x onerror=alert(1)>\n[run](javascript:alert)\n[data](data:text/html,evil)\n![image](https://example.invalid/a.png)');
    expect(html).not.toContain('<script');
    expect(html).not.toContain('<img');
    expect(html).toContain('&lt;script&gt;');
    expect(html).not.toContain('href="javascript:');
    expect(html).not.toContain('href="data:');
  });
  it('preserves Vietnamese, safe formatting, links and exact handles', () => {
    const html = render('# Chia sẻ nghề nghiệp\n**Kỹ năng** và *kinh nghiệm*\n[nguồn](https://example.invalid/path) @dong_nghiep contact@example.invalid @abcdefghijklmnopqrstuvwxyz');
    expect(html).toContain('<h3>Chia sẻ nghề nghiệp</h3>');
    expect(html).toContain('<strong>Kỹ năng</strong>');
    expect(html).toContain('rel="noopener noreferrer"');
    expect(html).toContain('#community?handle=dong_nghiep');
    expect(html).not.toContain('#community?handle=example');
    expect(html).not.toContain('#community?handle=abcdefghijkl');
  });
  it('renders code blocks, lists, strike/underline and internal community images safely', () => {
    const imageId = '11111111-2222-3333-4444-555555555555';
    const html = render(
      '1. Bước một\n2. Bước hai\n- Mục A\n- Mục B\n~~cũ~~ ++mới++ `dotnet test`\n```csharp\nvar x = 1 < 2;\n```\n![Minh họa](/api/community/images/' + imageId + ')'
    );
    expect(html).toContain('<ol start="1" class="community-ordered"><li>Bước một</li><li>Bước hai</li></ol>');
    expect(html).toContain('<ul class="community-unordered"><li>Mục A</li><li>Mục B</li></ul>');
    expect(html).toContain('<del>cũ</del>');
    expect(html).toContain('<u>mới</u>');
    expect(html).toContain('<code>dotnet test</code>');
    expect(html).toContain('<pre class="community-code-block"><code class="language-csharp">var x = 1 &lt; 2;</code></pre>');
    expect(html).toContain(`<img class="community-inline-image" src="/api/community/images/${imageId}" alt="Minh họa" loading="lazy"/>`);
  });
});
