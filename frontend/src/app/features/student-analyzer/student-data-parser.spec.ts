import { categorizeAnalyzerColumn, classifyAnalyzerColumns, normalizeAnalyzerText, parseManualStudentData } from './student-data-parser';

describe('Student analyzer prototype parity', () => {
  it('normalizes Arabic variants, hidden characters and Arabic numerals', () => {
    expect(normalizeAnalyzerText('  إِثــارة\u200F الفوضى ١٢ ')).toBe('اثاره الفوضي 12');
  });

  it('classifies the prototype grant and deduction vocabulary', () => {
    expect(categorizeAnalyzerColumn('المشاركة الفعالة')).toBe('grant');
    expect(categorizeAnalyzerColumn('عدم حل الواجب كلي')).toBe('deduction');
    expect(categorizeAnalyzerColumn('تأخر صباحي')).toBe('deduction');
  });

  it('skips identity and total columns while keeping unknown columns', () => {
    const result = classifyAnalyzerColumns(['اسم الطالب', 'المجموع', 'التعاون', 'الغياب', 'بند جديد']);
    expect(result.grants).toEqual(['التعاون']);
    expect(result.deductions).toEqual(['الغياب']);
    expect(result.unknown).toEqual(['بند جديد']);
  });

  it('keeps manual CSV flow and real student names', () => {
    const result = parseManualStudentData('اسم الطالب,التعاون,التأخر\nأحمد محمد,5,2', 'csv');
    expect(result.students[0]['__name__']).toBe('أحمد محمد');
    expect(result.students[0]['التعاون']).toBe(5);
  });
});

