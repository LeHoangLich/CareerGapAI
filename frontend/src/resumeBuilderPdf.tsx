import { Document, Font, Page, StyleSheet, Text, View } from '@react-pdf/renderer';

export type BuilderExperience = { role: string; organization: string; period: string; description: string };
export type BuilderEducation = { program: string; institution: string; period: string; details: string };
export type BuilderProject = { name: string; context: string; description: string };
export type ResumeBuilderContent = {
  schemaVersion: number;
  templateVersion: string;
  displayName: string;
  email: string;
  phone: string;
  location: string;
  headline: string;
  summary: string;
  skills: string[];
  experience: BuilderExperience[];
  education: BuilderEducation[];
  projects: BuilderProject[];
  certifications: string[];
};

Font.register({ family: 'NotoCV', src: '/fonts/NotoSans-Regular.ttf' });

const styles = StyleSheet.create({
  page: { paddingTop: 42, paddingBottom: 46, paddingHorizontal: 48, color: '#17312c', fontFamily: 'NotoCV', fontSize: 10 },
  name: { fontSize: 24, lineHeight: 1.2, fontWeight: 700, color: '#164c3b' },
  headline: { marginTop: 5, fontSize: 12, color: '#52665e' },
  contact: { marginTop: 7, color: '#52665e', fontSize: 9 },
  section: { marginTop: 18 },
  heading: { fontSize: 11, fontWeight: 700, color: '#1e644d', borderBottomWidth: 1, borderBottomColor: '#dce4da', paddingBottom: 4, marginBottom: 7 },
  item: { marginBottom: 8 },
  itemTitle: { fontSize: 10, fontWeight: 700 },
  detail: { marginTop: 2, color: '#52665e' },
  skills: { color: '#214f3e' },
  footer: { position: 'absolute', bottom: 20, height: 12, left: 48, right: 48, fontSize: 8, color: '#809087', textAlign: 'center' }
});

function nonempty(...values: string[]) { return values.map(value => value.trim()).filter(Boolean).join(' · '); }

export type PdfLanguage = 'vi' | 'en';
export const pdfHeadings = {
  vi: { summary: 'GIỚI THIỆU', skills: 'KỸ NĂNG', experience: 'KINH NGHIỆM', projects: 'DỰ ÁN', education: 'HỌC VẤN', certifications: 'CHỨNG CHỈ' },
  en: { summary: 'PROFILE', skills: 'SKILLS', experience: 'EXPERIENCE', projects: 'PROJECTS', education: 'EDUCATION', certifications: 'CERTIFICATIONS' },
};
// A document factory (no hooks/DOM). It also stays outside React Fast Refresh in workers.
export function createResumePdfDocument({ content, language = 'vi' }: { content: ResumeBuilderContent; language?: PdfLanguage }) {
  const headings = pdfHeadings[language];
  return <Document title={`CV ${content.displayName}`} author={content.displayName} language={language}>
    <Page size="A4" style={styles.page} wrap>
      <Text style={styles.name}>{content.displayName || 'Tên ứng viên'}</Text>
      {content.headline.trim() && <Text style={styles.headline}>{content.headline.trim()}</Text>}
      {nonempty(content.email, content.phone, content.location) && <Text style={styles.contact}>{nonempty(content.email, content.phone, content.location)}</Text>}
      {content.summary.trim() && <View style={styles.section}><Text style={styles.heading}>{headings.summary}</Text><Text>{content.summary.trim()}</Text></View>}
      {!!content.skills.filter(Boolean).length && <View style={styles.section}><Text style={styles.heading}>{headings.skills}</Text><Text style={styles.skills}>{content.skills.filter(Boolean).join('  ·  ')}</Text></View>}
      {!!content.experience.some(item => item.role || item.organization || item.description) && <View style={styles.section}>
        <Text style={styles.heading}>{headings.experience}</Text>
        {content.experience.filter(item => item.role || item.organization || item.description).map((item, index) => <View key={`experience-${index}`} style={styles.item} wrap={false}>
          <Text style={styles.itemTitle}>{nonempty(item.role, item.organization)}</Text>
          {nonempty(item.period) && <Text style={styles.detail}>{item.period.trim()}</Text>}
          {!!item.description.trim() && <Text style={styles.detail}>{item.description.trim()}</Text>}
        </View>)}
      </View>}
      {!!content.projects.some(item => item.name || item.description) && <View style={styles.section}>
        <Text style={styles.heading}>{headings.projects}</Text>
        {content.projects.filter(item => item.name || item.description).map((item, index) => <View key={`project-${index}`} style={styles.item} wrap={false}>
          <Text style={styles.itemTitle}>{nonempty(item.name, item.context)}</Text><Text style={styles.detail}>{item.description}</Text>
        </View>)}
      </View>}
      {!!content.education.some(item => item.program || item.institution) && <View style={styles.section}>
        <Text style={styles.heading}>{headings.education}</Text>
        {content.education.filter(item => item.program || item.institution).map((item, index) => <View key={`education-${index}`} style={styles.item} wrap={false}>
          <Text style={styles.itemTitle}>{nonempty(item.program, item.institution)}</Text>
          {nonempty(item.period) && <Text style={styles.detail}>{item.period.trim()}</Text>}
          {!!item.details.trim() && <Text style={styles.detail}>{item.details.trim()}</Text>}
        </View>)}
      </View>}
      {!!content.certifications.filter(Boolean).length && <View style={styles.section}><Text style={styles.heading}>{headings.certifications}</Text>{content.certifications.filter(Boolean).map((item, index) => <Text key={`certification-${index}`} style={styles.item}>{item}</Text>)}</View>}
      <Text style={styles.footer} fixed render={({ pageNumber, totalPages }) => `${pageNumber} / ${totalPages}`} />
    </Page>
  </Document>;
}
