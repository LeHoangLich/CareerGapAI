export const coverLetterLimit = 5000;

export function normalizeCoverLetter(value: string): string | null {
  if (value.length > coverLetterLimit || value.includes('\0'))
    throw new Error('Thư ứng tuyển tối đa 5.000 ký tự và không chứa ký tự không hợp lệ.');
  return value.replace(/\r\n?/g, '\n').trim() || null;
}
