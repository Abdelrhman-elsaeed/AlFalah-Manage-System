import * as XLSX from 'xlsx';
import { GlobalWorkerOptions, getDocument } from 'pdfjs-dist';

const pdfAssetBase = new URL('assets/pdfjs/', document.baseURI).toString();
GlobalWorkerOptions.workerSrc = `${pdfAssetBase}pdf.worker.min.mjs`;

export type StudentValue = string | number;
export interface StudentRecord { [column: string]: StudentValue; }
export interface ParsedStudentData {
  headers: string[];
  students: StudentRecord[];
  sheetName?: string;
}
export interface ColumnClassification {
  grants: string[];
  deductions: string[];
  unknown: string[];
}

// The vocabulary and matching order below are copied verbatim from prototype v2.0.
export const KNOWN_GRANTS = [
  'الطالب المثالي', 'المشاركة الفعالة', 'الإبداع', 'التعاون',
  'العمل التطوعي', 'فوز بالمسابقات', 'المشاركة مسابقات', 'مشاركة مسابقات',
  'الأيام والمناسبات', 'سلوك إيجابي', 'انضباط الصلاة',
  'القدوة الحسنة', 'الدعم الاجتماعي', 'مصادر', 'تسليم الخطة',
  'التدريس وفق الخطة', 'مشارك في فعاليات', 'مشارك في أنشطة',
  'مشارك في الإذاعة', 'مشارك في مسابقات', 'يدرس عن بعد',
  'التحول الرقمي', 'شهادات تدريبية', 'الأسلوب التربوي',
  'الاحترام المتبادل', 'تقبل التوجيهات', 'مشارك في الاصطفاف',
  'ملتزم في الدوام', 'ملتزم في الحصص', 'الاستجابة السريعة',
  'ملتزم في الإشراف', 'منضبط في المناوبة', 'مبكر في التسليم',
  'تفعيل الانتظار', 'تكريم المتميزين', 'رعاية الموهوبين',
  'رعاية الضعاف', 'رفع مستوى الطلاب', 'تفعيل الواجبات',
  'يصحح الواجبات', 'وضع خطة القائد', 'لديه ملف إنجاز',
  'تفعيل الزيارات', 'تحليل النتائج', 'مفعل التعلم المهن',
  'تبادل الخبرات', 'التنمية المهنية', 'لديه زيارات هادفة',
  'الإبداع والابتكار', 'مشاركة في المسابقات',
  'ملتزم التعليمات', 'يتابع القائد', 'وضع خطة',
  'يحضر الدروس', 'يهيئ المعلم', 'يهيئ المصادر',
  'ملتزم الدوام', 'ملتزم الحصص', 'يصلح الواجبات',
  'يسلم الخطة', 'صحة الصباح', 'يفعل المناقبة',
  'تقييم المعلم', 'تقييم الطالب', 'مشارك', 'مشاركة'
];

export const KNOWN_DEDUCTIONS = [
  'عدم التفاعل', 'إثارة الفوضى', 'غير ملتزم التعليمات',
  'غ ملتزم التعليمات', 'غير ملتزم', 'التحدث في الحصة', 'لم يتابع القائد',
  'لم يضع خطة القائد', 'لم يضع خطة', 'لا يوجد ملف إنجاز',
  'لا يوجد ملف', 'لم يحضر الدروس', 'لم يهيئ المصادر',
  'لم يهيئ المعلم', 'التأخر عن الدوام', 'التأخر عن الحصص',
  'التأخر عن الحصة', 'متهاون في الإشراف', 'متهاون الإشراف',
  'متأخر في التسليم', 'لم يصحح الواجبات', 'لم يسلم الخطة',
  'التأخر الصباحي', 'مخالفة الزي', 'الشغب', 'الهروب من الحصة',
  'إتلاف الممتلكات', 'إحضار الجوال', 'امتهان الكتب',
  'الهروب عن الصلاة', 'الشغب في الاحتياط', 'التأخر الدراسي',
  'عدم تنفيذ الواجب', 'عدم تسليم المشروع', 'عدم إحضار الأدوات',
  'النوم في الحصة', 'عدم حل الواجب جزئ', 'عدم حل الواجب كلي',
  'عدم حل الواجب', 'التأخر عن الصلاة', 'عدم إحضار الزي',
  'مخالف في الانتظار', 'عدم الشكى', 'التأخر في الدفاعات',
  'لم يفعل المناقبة', 'الهروب من المدرسة', 'التأخر',
  'الغياب', 'غياب', 'تغيب', 'لم يحضر', 'عدم الحضور',
  'عدم التسليم', 'لم يسلم', 'مشكلة', 'مخالفة',
  'عدم الانضباط', 'إزعاج', 'إثارة', 'فوضى',
  'لم يراجع', 'عدم المراجعة', 'عدم الاهتمام',
  'التدخين', 'ممنوعات', 'هاتف', 'جوال'
];

export const SKIP_COLUMNS = [
  'م', 'رقم', 'الرقم', '#', 'اسم الطالب', 'الاسم', 'اسم',
  'الطالب', 'المجموع', 'الإجمالي', 'الصف', 'الفصل',
  'التاريخ', 'ملاحظات', 'التوقيع', 'رقم الجلوس',
  'اسم المعلم', 'المادة', 'الوحدة', 'الاسبوع',
  'المجموع الكلي', 'مجموع المنح', 'مجموع الخصم'
];

export function normalizeAnalyzerText(text: string): string {
  return text
    .replace(/[\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF\u00AD\u034F\u2028\u2029]/g, '')
    .replace(/[\u200C\u200D]/g, '')
    .replace(/["'»«]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/[أإآٱ]/g, 'ا')
    .replace(/ة/g, 'ه')
    .replace(/ى/g, 'ي')
    .replace(/[\u064B-\u065F\u0670]/g, '')
    .replace(/\u0640/g, '')
    .replace(/ؤ/g, 'و')
    .replace(/ئ/g, 'ي')
    .replace(/[٠١٢٣٤٥٦٧٨٩]/g, digit => String('٠١٢٣٤٥٦٧٨٩'.indexOf(digit)))
    .toLowerCase();
}

export function categorizeAnalyzerColumn(header: string): 'grant' | 'deduction' | 'unknown' {
  const normalized = normalizeAnalyzerText(header);
  if (KNOWN_GRANTS.some(item => normalizeAnalyzerText(item) === normalized)) return 'grant';
  if (KNOWN_DEDUCTIONS.some(item => normalizeAnalyzerText(item) === normalized)) return 'deduction';
  for (const item of KNOWN_GRANTS) {
    const known = normalizeAnalyzerText(item);
    if (normalized.includes(known) || known.includes(normalized)) return 'grant';
  }
  for (const item of KNOWN_DEDUCTIONS) {
    const known = normalizeAnalyzerText(item);
    if (normalized.includes(known) || known.includes(normalized)) return 'deduction';
  }
  const deductionPrefixes = [
    'عدم ', 'لم ', 'لا يوجد', 'غياب', 'هروب', 'اتلاف', 'مخالف', 'مخالفه',
    'شغب', 'تاخر', 'متهاون', 'اثاره', 'لم يحضر', 'لم يسلم', 'لم يصحح', 'غير ملتزم'
  ];
  const grantPrefixes = [
    'مشارك', 'ملتزم', 'مبكر', 'منضبط', 'تكريم', 'رعايه', 'تفعيل', 'تحليل',
    'ابداع', 'تعاون', 'فوز', 'مثالي', 'حضور', 'التزام', 'انضباط', 'مواظبه', 'تميز'
  ];
  if (deductionPrefixes.some(item => normalized.startsWith(normalizeAnalyzerText(item)) || normalized.includes(normalizeAnalyzerText(item)))) return 'deduction';
  if (grantPrefixes.some(item => normalized.startsWith(normalizeAnalyzerText(item)) || normalized.includes(normalizeAnalyzerText(item)))) return 'grant';
  return 'unknown';
}

export function classifyAnalyzerColumns(headers: string[]): ColumnClassification {
  const result: ColumnClassification = { grants: [], deductions: [], unknown: [] };
  for (const header of headers) {
    if (!header || header.length < 2) continue;
    const normalized = normalizeAnalyzerText(header);
    if (SKIP_COLUMNS.some(item => normalizeAnalyzerText(item) === normalized)) continue;
    result[`${categorizeAnalyzerColumn(header)}${categorizeAnalyzerColumn(header) === 'unknown' ? '' : 's'}` as keyof ColumnClassification].push(header);
  }
  if (!result.grants.length && !result.deductions.length) {
    result.unknown = headers.filter(header => header?.length > 1 && !SKIP_COLUMNS.some(item => normalizeAnalyzerText(item) === normalizeAnalyzerText(header)));
  }
  return result;
}

export async function parseStudentFile(file: File, onProgress: (message: string) => void): Promise<ParsedStudentData> {
  const extension = file.name.split('.').pop()?.toLowerCase();
  if (extension === 'pdf') return parsePdf(file, onProgress);
  return parseWorkbook(file, onProgress);
}

export function parseManualStudentData(raw: string, format: 'json' | 'csv'): ParsedStudentData {
  let students: StudentRecord[];
  if (format === 'json') {
    const parsed: unknown = JSON.parse(raw);
    const rows = Array.isArray(parsed) ? parsed : [parsed];
    if (!rows.every(row => row !== null && typeof row === 'object' && !Array.isArray(row))) throw new Error('صيغة JSON يجب أن تحتوي على كائن طالب أو قائمة كائنات.');
    students = rows as StudentRecord[];
  } else {
    const lines = raw.split('\n').filter(line => line.trim());
    if (lines.length < 2) throw new Error('يجب أن يحتوي CSV على صف رأس وصف بيانات على الأقل.');
    const headers = lines[0].split(',').map(header => header.trim());
    students = lines.slice(1).map(line => {
      const values = line.split(',');
      const student: StudentRecord = {};
      headers.forEach((header, index) => {
        const value = values[index]?.trim() ?? '';
        student[header] = value !== '' && !Number.isNaN(Number(value)) ? Number(value) : value;
      });
      return student;
    });
  }
  if (!students.length) throw new Error('لم يتم العثور على بيانات طلاب.');
  students = students.map((student, index) => {
    const nameField = Object.keys(student).find(key => ['اسم الطالب', 'الاسم', 'اسم', 'name', 'الطالب'].includes(key));
    return { ...student, __name__: nameField ? student[nameField] : `طالب ${index + 1}` };
  });
  return { headers: Object.keys(students[0]).filter(key => !key.startsWith('__')), students };
}

async function parseWorkbook(file: File, onProgress: (message: string) => void): Promise<ParsedStudentData> {
  onProgress('جاري فتح ملف Excel...');
  const workbook = XLSX.read(await file.arrayBuffer(), { type: 'array', cellDates: true, cellNF: false, cellText: false });
  if (!workbook.SheetNames.length) throw new Error('الملف لا يحتوي على أي ورقة بيانات.');
  let sheetName = workbook.SheetNames[0];
  let maxRows = -1;
  for (const candidate of workbook.SheetNames) {
    const sheet = workbook.Sheets[candidate];
    const range = XLSX.utils.decode_range(sheet['!ref'] || 'A1:A1');
    const rows = range.e.r - range.s.r;
    if (rows > maxRows) { maxRows = rows; sheetName = candidate; }
  }
  onProgress(`جاري تحليل ورقة: "${sheetName}"...`);
  const raw = XLSX.utils.sheet_to_json<(string | number)[]>(workbook.Sheets[sheetName], {
    header: 1, defval: '', blankrows: false, raw: false
  });
  if (raw.length < 2) throw new Error('الملف يحتوي على أقل من صفين. تأكد من أن الملف يحتوي على بيانات.');
  const headerIndex = findHeaderRow(raw);
  const headerSource = headerIndex < 0 ? raw.find(row => row?.length >= 2) ?? raw[0] : raw[headerIndex];
  const headers = headerIndex < 0
    ? headerSource.map((_, index) => `العمود ${index + 1}`)
    : headerSource.map(value => cleanText(String(value ?? '')));
  const valid = headers.map((header, index) => ({ header, index })).filter(item => item.header.length > 0);
  const students = raw.slice(headerIndex + 1).flatMap((row, rowIndex) => {
    if (!row?.length || row.filter(value => value !== null && value !== undefined && String(value).trim() !== '').length < 2) return [];
    const student: StudentRecord = {};
    valid.forEach(({ header, index }) => student[header] = parseValue(String(row[index] ?? '')));
    const finalHeaders = valid.map(item => item.header);
    const nameField = findNameField(student, finalHeaders);
    student['__name__'] = nameField ? String(student[nameField]) : `طالب ${rowIndex + 1}`;
    student['__nameField__'] = nameField ?? '';
    return String(student['__name__']).length > 1 ? [student] : [];
  });
  if (!students.length) throw new Error('لم يتم العثور على بيانات طلاب. تأكد من أن الملف يحتوي على جدول منح وخصم.');
  onProgress(`✅ تم استخراج ${students.length} طالب و ${valid.length} عمود بنجاح`);
  return { headers: valid.map(item => item.header), students, sheetName };
}

interface PdfItem { text: string; x: number; y: number; width: number; page: number; }

async function parsePdf(file: File, onProgress: (message: string) => void): Promise<ParsedStudentData> {
  onProgress('جاري فتح ملف PDF...');
  const pdf = await getDocument({
    data: await file.arrayBuffer(),
    cMapUrl: `${pdfAssetBase}cmaps/`,
    cMapPacked: true,
    disableFontFace: false
  }).promise;
  const all: PdfItem[] = [];
  for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber++) {
    onProgress(`جاري قراءة الصفحة ${pageNumber} من ${pdf.numPages}...`);
    const page = await pdf.getPage(pageNumber);
    const viewport = page.getViewport({ scale: 1.5 });
    const content = await page.getTextContent();
    for (const rawItem of content.items) {
      if (!('str' in rawItem)) continue;
      const text = cleanText(rawItem.str || '');
      if (!text) continue;
      all.push({ text, x: Math.round(rawItem.transform[4]), y: Math.round(viewport.height - rawItem.transform[5]), width: rawItem.width || 0, page: pageNumber });
    }
  }
  if (!all.length) throw new Error('الملف لا يحتوي على نص قابل للاستخراج. قد يكون الملف صورة ممسوحة ضوئياً.');
  let rows: PdfItem[][] | null = null;
  for (const threshold of [3, 5, 7, 10, 14]) {
    const candidate = groupPdfRows(all, threshold);
    if (candidate.length >= 2 && candidate.some(row => row.length >= 3)) { rows = candidate; break; }
  }
  if (!rows || rows.length < 2) throw new Error('لم يتم العثور على جدول في الملف. تأكد من أن الملف هو تقرير بنود المنح والخصم.');
  const identified = identifyPdfHeader(rows);
  const headers = identified.header.map(item => cleanText(item.text));
  const students: StudentRecord[] = [];
  identified.data.forEach((row, rowIndex) => {
    if (row.length < 2 || isNonDataRow(row.map(item => item.text).join(''))) return;
    const hasNumber = row.some(item => {
      const value = item.text.replace(/[٠١٢٣٤٥٦٧٨٩]/g, digit => String('٠١٢٣٤٥٦٧٨٩'.indexOf(digit))).trim();
      return value !== '' && !Number.isNaN(Number(value));
    });
    if (!hasNumber && row.length < 3) return;
    const student: StudentRecord = {};
    row.forEach((item, columnIndex) => student[headers[columnIndex] || `عمود${columnIndex + 1}`] = parseValue(item.text));
    const nameField = findNameField(student, headers);
    student['__name__'] = nameField ? String(student[nameField]) : `طالب ${rowIndex + 1}`;
    student['__nameField__'] = nameField ?? '';
    if (String(student['__name__']).length > 1) students.push(student);
  });
  if (!students.length) throw new Error('تم قراءة الملف لكن لم يتم العثور على بيانات طلاب. تحقق من تنسيق الملف.');
  onProgress(`✅ تم استخراج ${students.length} طالب بنجاح`);
  return { headers, students };
}

function findHeaderRow(rows: (string | number)[][]): number {
  const limit = Math.min(20, rows.length);
  for (let index = 0; index < limit; index++) {
    const row = rows[index];
    if (!row) continue;
    const unique = new Set(row.map(value => String(value || '').trim()).filter(Boolean));
    if (unique.size < 3) continue;
    const text = row.map(value => String(value || '').trim().replace(/\u0640/g, '').toLowerCase()).join(' ');
    if (['الطالب', 'الاسم', 'اسم الطالب', 'متدرب', 'النقاط', 'مخالفة', 'مخالفات', 'السلوك', 'اسم'].some(keyword => text.includes(keyword))) return index;
  }
  let best = -1;
  let maxText = 0;
  for (let index = 0; index < limit; index++) {
    const row = rows[index];
    if (!row || new Set(row.map(value => String(value || '').trim()).filter(Boolean)).size < 3) continue;
    const textCells = row.filter(value => { const text = String(value || '').trim(); return text !== '' && Number.isNaN(Number(text)); }).length;
    if (textCells > maxText) { maxText = textCells; best = index; }
  }
  return best;
}

function groupPdfRows(items: PdfItem[], threshold: number): PdfItem[][] {
  const sorted = [...items].sort((a, b) => Math.abs(a.y - b.y) <= threshold ? b.x - a.x : a.y - b.y);
  const rows: PdfItem[][] = [];
  let y = sorted[0].y;
  let row = [sorted[0]];
  for (const item of sorted.slice(1)) {
    if (Math.abs(item.y - y) <= threshold) row.push(item);
    else { rows.push(row); row = [item]; y = item.y; }
  }
  rows.push(row);
  return rows.map(itemsInRow => itemsInRow.sort((a, b) => b.x - a.x));
}

function identifyPdfHeader(rows: PdfItem[][]): { header: PdfItem[]; data: PdfItem[][] } {
  let index = -1;
  for (let rowIndex = 0; rowIndex < Math.min(20, rows.length); rowIndex++) {
    const row = rows[rowIndex];
    if (row.length < 2 || new Set(row.map(item => item.text.trim()).filter(Boolean)).size < 3) continue;
    const text = row.map(item => item.text.replace(/\u0640/g, '')).join(' ').toLowerCase();
    if (['الطالب', 'الاسم', 'متدرب', 'النقاط', 'مخالفة', 'مخالفات', 'السلوك', 'اسم'].some(keyword => text.includes(keyword))) { index = rowIndex; break; }
  }
  if (index < 0) {
    for (let rowIndex = 0; rowIndex < Math.min(20, rows.length); rowIndex++) {
      const row = rows[rowIndex];
      if (row.length < 3 || new Set(row.map(item => item.text.trim()).filter(Boolean)).size < 3) continue;
      const textCount = row.filter(item => Number.isNaN(Number(item.text.trim().replace(/[٠١٢٣٤٥٦٧٨٩]/g, digit => String('٠١٢٣٤٥٦٧٨٩'.indexOf(digit)))))).length;
      const averageLength = row.reduce((sum, item) => sum + item.text.length, 0) / row.length;
      if (textCount / row.length >= .5 && averageLength < 30) { index = rowIndex; break; }
    }
  }
  if (index < 0) {
    const first = rows.find(row => row.length >= 2) ?? rows[0];
    return { header: first.map((_, itemIndex) => ({ text: `العمود ${itemIndex + 1}`, x: 0, y: 0, width: 0, page: 1 })), data: rows };
  }
  return { header: rows[index], data: rows.slice(index + 1) };
}

function cleanText(text: string): string {
  return text.replace(/[\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF\u00AD\u200C\u200D]/g, '').replace(/\s+/g, ' ').replace(/[٠١٢٣٤٥٦٧٨٩]/g, digit => String('٠١٢٣٤٥٦٧٨٩'.indexOf(digit))).trim();
}

function parseValue(value: string): StudentValue {
  const cleaned = cleanText(value);
  return cleaned !== '' && !Number.isNaN(Number(cleaned)) ? Number(cleaned) : cleaned;
}

function findNameField(student: StudentRecord, headers: string[]): string | null {
  for (const keyword of ['اسم الطالب', 'الاسم', 'اسم', 'الطالب', 'name', 'student', 'متدرب']) {
    const found = headers.find(header => cleanText(header).toLowerCase().includes(keyword.toLowerCase()));
    if (found && typeof student[found] === 'string' && String(student[found]).length >= 2) return found;
  }
  return headers.find(header => typeof student[header] === 'string' && String(student[header]).length >= 2 && /[\u0600-\u06FF]/.test(String(student[header]))) ?? null;
}

function isNonDataRow(text: string): boolean {
  const lower = text.toLowerCase();
  return ['code plus', 'كود بلس', 'code+', 'صفحة من', 'page of', 'التاريخ:', 'copyright', '©'].some(pattern => lower.includes(pattern));
}
