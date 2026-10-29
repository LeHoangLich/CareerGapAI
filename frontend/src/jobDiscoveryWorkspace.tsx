import { useState, type ReactNode } from 'react';
import { CandidateKeywordRecommendations } from './matching';
import { useStickyFit } from './useStickyFit';

type Resume = { id: string; title: string; versions: { id: string; versionNo: number; createdAt?: string }[] };

export function JobDiscoveryWorkspace({ resumes, onSearchTitle, children }: {
  resumes: Resume[]; onSearchTitle: (title: string) => void; children: ReactNode;
}) {
  const [keywordTarget, setKeywordTarget] = useState<HTMLDivElement | null>(null);
  const [aiTarget, setAiTarget] = useState<HTMLDivElement | null>(null);
  const stickyRef = useStickyFit();
  return <div className="job-discovery-grid grid items-start gap-6">
    <aside ref={stickyRef} className="job-discovery-controls viewport-sticky min-w-0" aria-label="Điều khiển gợi ý việc theo CV">
      <CandidateKeywordRecommendations resumes={resumes} onSearchTitle={onSearchTitle} resultTargets={{ keyword: keywordTarget, ai: aiTarget }}/>
    </aside>
    <div className="job-discovery-results min-w-0">
      <div ref={setAiTarget} className="matching-output-slot"/>
      <div ref={setKeywordTarget} className="matching-output-slot"/>
      {children}
    </div>
  </div>;
}
