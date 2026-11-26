import { pdf } from '@react-pdf/renderer';
import { createResumePdfDocument, type PdfLanguage, type ResumeBuilderContent } from './resumeBuilderPdf';

// A fresh worker owns the renderer/font cache for one document. Reusing cached glyphs
// across documents can corrupt the ToUnicode mapping after a language/content change.
self.addEventListener('message', async (event: MessageEvent<{ content: ResumeBuilderContent; language: PdfLanguage }>) => {
  try {
    const blob = await pdf(createResumePdfDocument(event.data)).toBlob();
    self.postMessage({ blob });
  } catch (error) {
    if (import.meta.env.DEV) console.error('PDF worker render failed', error);
    self.postMessage({ error: 'Chưa dựng được PDF. Hãy thử lại sau.' });
  }
});
