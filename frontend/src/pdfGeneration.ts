import type { PdfLanguage, ResumeBuilderContent } from './resumeBuilderPdf';

export function renderBuilderPdf(content: ResumeBuilderContent, language: PdfLanguage): Promise<Blob> {
  return new Promise((resolve, reject) => {
    const worker = new Worker(new URL('./resumePdf.worker.tsx', import.meta.url), { type: 'module' });
    const finish = () => { clearTimeout(timer); worker.terminate(); };
    const timer = setTimeout(() => { finish(); reject(new Error('Tạo PDF quá thời gian cho phép. Hãy thử lại.')); }, 60000);
    worker.onmessage = (event: MessageEvent<{ blob?: Blob; error?: string }>) => {
      finish();
      if (event.data.blob instanceof Blob) resolve(event.data.blob);
      else reject(new Error(event.data.error ?? 'Chưa dựng được PDF.'));
    };
    worker.onerror = event => {
      if (import.meta.env.DEV) console.error('PDF worker could not start', event.message);
      finish(); reject(new Error('Chưa dựng được PDF. Hãy thử lại sau.'));
    };
    worker.postMessage({ content, language });
  });
}
