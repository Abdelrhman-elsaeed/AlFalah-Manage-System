import { renderStudentAnalysisMarkdown, sanitizeStudentAnalysisText } from './student-analysis-markdown';

describe('Student analysis Markdown rendering', () => {
  it('renders heading levels, emphasis, unordered and ordered lists', () => {
    const html = renderStudentAnalysisMarkdown(`
# عنوان رئيسي
#### عنوان فرعي

**هدف مهم**

1. الخطوة الأولى
2. الخطوة الثانية

* نقطة أولى
* نقطة ثانية
    `);

    expect(html).toContain('<h1>عنوان رئيسي</h1>');
    expect(html).toContain('<h4>عنوان فرعي</h4>');
    expect(html).toContain('<strong>هدف مهم</strong>');
    expect(html).toContain('<ol>');
    expect(html).toContain('<ul>');
    expect(html).not.toContain('####');
    expect(html).not.toContain('**هدف مهم**');
  });

  it('renders GitHub-flavoured tables with a header and body', () => {
    const html = renderStudentAnalysisMarkdown(`
| البند | النوع | القيمة |
| --- | --- | ---: |
| التعاون | منحة | 5 |
| التأخر | خصم | 2 |
    `);

    expect(html).toContain('<table>');
    expect(html).toContain('<thead>');
    expect(html).toContain('<tbody>');
    expect(html).toContain('<th>البند</th>');
    expect(html).toContain('<td>التعاون</td>');
  });

  it('removes leaked provider chat-template tokens and their malformed suffix', () => {
    const text = `* **السلوكي:** التعزيز التفاضلي للسلوكيات المنخفضة \` ** **
***
"
آ
 point:
 user |`;

    const clean = sanitizeStudentAnalysisText(text);
    const html = renderStudentAnalysisMarkdown(text);

    expect(clean).toBe('* **السلوكي:** التعزيز التفاضلي للسلوكيات المنخفضة');
    expect(html).not.toContain('point:');
    expect(html).not.toContain('user |');
  });
});
