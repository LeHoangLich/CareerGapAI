import { Document, Font, Page, Text, View, pdf } from '@react-pdf/renderer';
Font.register({ family: 'NotoProbe', src: '/fonts/NotoSans-Regular.ttf' });
export async function downloadProbe() {
  const doc = <Document title="Dữ liệu giả — kiểm tra PDF"><Page size="A4" style={{ padding: 40, fontFamily: 'NotoProbe', fontSize: 12 }}><Text style={{ fontSize: 22 }}>Hồ sơ thử nghiệm — Nguyễn Ứng Viên</Text><Text>DỮ LIỆU GIẢ · Không phải CV của người thật.</Text>{Array.from({ length: 30 }, (_, i) => <View key={i} wrap={false} style={{ marginTop: 20 }}><Text>Kinh nghiệm {i + 1}: Phát triển ứng dụng, kiểm thử dữ liệu và quản lý phiên bản.</Text><Text>React · TypeScript · PostgreSQL · English text export check.</Text></View>)}</Page></Document>;
  const blob = await pdf(doc).toBlob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a'); a.href = url; a.download = 'synthetic-vietnamese-probe.pdf'; a.click();
  setTimeout(() => URL.revokeObjectURL(url), 10000);
}
