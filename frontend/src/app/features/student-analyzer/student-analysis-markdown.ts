import { marked } from 'marked';

/** Converts the AI's GitHub-flavoured Markdown into HTML for Angular to sanitize and display. */
export function renderStudentAnalysisMarkdown(markdown: string): string {
  return marked.parse(sanitizeStudentAnalysisText(markdown), { async: false, breaks: true, gfm: true });
}

/** Removes chat-template tokens occasionally leaked by routed/free AI models. */
export function sanitizeStudentAnalysisText(text: string): string {
  const marker = /point\s*:\s*(?:user|assistant)\s*\|/i.exec(text);
  if (!marker) return text.trim();

  const lines = text.slice(0, marker.index).split(/\r?\n|\r/);
  while (lines.length) {
    const tail = lines.at(-1)?.trim() ?? '';
    if (!tail || /^(?:[*_`"'#|:.\-–—]+|آ)$/u.test(tail)) lines.pop();
    else break;
  }

  let clean = lines.join('\n').trimEnd();
  if ([...clean].filter(character => character === '`').length % 2 !== 0)
    clean = clean.slice(0, clean.lastIndexOf('`')).trimEnd();
  return clean.replace(/\s*\*+\s+\*+\s*$/u, '').trim();
}
