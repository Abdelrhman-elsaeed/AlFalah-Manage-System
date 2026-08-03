import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const debuggingPort = 9337;
const profile = mkdtempSync(join(tmpdir(), 'alfalah-layout-'));
const chrome = spawn(chromePath, [
  '--headless=new',
  '--disable-gpu',
  '--no-first-run',
  '--remote-allow-origins=*',
  `--remote-debugging-port=${debuggingPort}`,
  `--user-data-dir=${profile}`,
  '--window-size=1536,900',
  'about:blank'
], { stdio: 'ignore' });

let socket;
const pending = new Map();
let commandId = 0;
let accessToken;
let createdFileId;

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function waitFor(check, description, timeout = 12000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const value = await check();
    if (value) return value;
    await delay(100);
  }
  throw new Error(`Timed out waiting for ${description}`);
}

async function connect() {
  const targets = await waitFor(async () => {
    try {
      const response = await fetch(`http://127.0.0.1:${debuggingPort}/json/list`);
      return response.ok ? response.json() : null;
    } catch {
      return null;
    }
  }, 'Chrome DevTools');
  const target = targets.find(item => item.type === 'page');
  socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.onopen = resolve;
    socket.onerror = reject;
  });
  socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (!message.id || !pending.has(message.id)) return;
    const { resolve, reject } = pending.get(message.id);
    pending.delete(message.id);
    message.error ? reject(new Error(message.error.message)) : resolve(message.result);
  };
}

function command(method, params = {}) {
  const id = ++commandId;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
}

async function evaluate(expression) {
  const result = await command('Runtime.evaluate', {
    expression,
    awaitPromise: true,
    returnByValue: true
  });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
  return result.result.value;
}

async function navigate(url) {
  await command('Page.navigate', { url });
  await waitFor(() => evaluate('document.readyState === "complete"'), url);
}

try {
  const loginResponse = await fetch('http://localhost:5264/api/v1/auth/school-login', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ schoolId: 1, username: 'school_manager_1', password: 'AlFalah@Manager2024!' })
  });
  const login = await loginResponse.json();
  if (!loginResponse.ok || !login.data) throw new Error('School-manager login failed');
  accessToken = login.data.accessToken;
  const filesBeforeResponse = await fetch('http://localhost:5264/api/v1/student-analyzer/files?page=1&pageSize=100', {
    headers: { authorization: `Bearer ${accessToken}` }
  });
  const filesBefore = await filesBeforeResponse.json();
  const fileIdsBefore = new Set((filesBefore.data?.items ?? []).map(file => file.id));

  await connect();
  await navigate('http://localhost:4200');
  await evaluate(`(() => {
    localStorage.setItem('alfalah_access_token', ${JSON.stringify(login.data.accessToken)});
    localStorage.setItem('alfalah_refresh_token', ${JSON.stringify(login.data.refreshToken)});
    localStorage.setItem('alfalah_user', ${JSON.stringify(JSON.stringify(login.data.user))});
    return true;
  })()`);
  await navigate('http://localhost:4200/student-analyzer');
  await waitFor(() => evaluate('!!document.querySelector(".upload-actions .btn--ghost")'), 'demo-data action');
  await evaluate('document.querySelector(".upload-actions .btn--ghost").click()');
  await waitFor(() => evaluate('!!document.querySelector(".column-grid")'), 'column-selection view');
  const filesAfterResponse = await fetch('http://localhost:5264/api/v1/student-analyzer/files?page=1&pageSize=100', {
    headers: { authorization: `Bearer ${accessToken}` }
  });
  const filesAfter = await filesAfterResponse.json();
  createdFileId = (filesAfter.data?.items ?? []).find(file => !fileIdsBefore.has(file.id))?.id;

  const progressHasProtectedLabels = await evaluate(`
    [...document.querySelectorAll('.progress > div')]
      .every(step => {
        const label = step.querySelector('.progress-label');
        if (!label) return false;
        const labelStyle = getComputedStyle(label);
        return labelStyle.zIndex !== 'auto'
          && labelStyle.backgroundColor === getComputedStyle(document.querySelector('.progress')).backgroundColor;
      })
  `);
  const primaryUsesPlatformTheme = await evaluate(`(() => {
    const probe = document.createElement('span');
    probe.style.backgroundColor = 'var(--brand-700)';
    document.body.appendChild(probe);
    const brandColor = getComputedStyle(probe).backgroundColor;
    probe.remove();
    return getComputedStyle(document.querySelector('.btn--primary')).backgroundColor === brandColor;
  })()`);

  await evaluate('document.querySelector(".workflow-footer .btn--primary").click()');
  await waitFor(() => evaluate('document.querySelectorAll(".student-card").length >= 5'), 'student cards');
  const cardLayout = await evaluate(`(() => {
    const cards = [...document.querySelectorAll('.student-card')];
    const buttonBottoms = cards.map(card => Math.round(card.querySelector('.btn').getBoundingClientRect().bottom));
    const buttonBottomOffsets = cards.map(card => {
      const cardRect = card.getBoundingClientRect();
      const buttonRect = card.querySelector('.btn').getBoundingClientRect();
      return Math.round(cardRect.bottom - buttonRect.bottom);
    });
    const cardHeights = cards.map(card => Math.round(card.getBoundingClientRect().height));
    return {
      buttonBottoms,
      buttonBottomOffsets,
      cardHeights,
      buttonOffsetSpread: Math.max(...buttonBottomOffsets) - Math.min(...buttonBottomOffsets)
    };
  })()`);

  const checks = {
    progressLabelsProtectedFromConnector: progressHasProtectedLabels,
    primaryButtonsUsePlatformTheme: primaryUsesPlatformTheme,
    studentCardButtonsAligned: cardLayout.buttonOffsetSpread <= 1
  };

  await navigate('http://localhost:4200/student-analyzer');
  const reportButtonExists = await waitFor(
    () => evaluate('!!document.querySelector(\'button[title="فتح التقرير"]\')'),
    'saved student report'
  );
  if (reportButtonExists) {
    await evaluate(`(() => {
      const rows = [...document.querySelectorAll('.library-section tbody tr')];
      const targetRow = rows.find(row => row.textContent?.includes('عمر فيصل النجدي'));
      (targetRow?.querySelector('button[title="فتح التقرير"]')
        ?? document.querySelector('button[title="فتح التقرير"]')).click();
    })()`);
    await waitFor(() => evaluate('!!document.querySelector(".report-doc")'), 'student report document');
    await delay(750);

    const reportRendering = await evaluate(`(() => {
      const bodies = [...document.querySelectorAll('.analysis-body')];
      const text = bodies.map(body => body.textContent || '').join('\\n');
      const hasChatTemplateArtifacts = /point\\s*:\\s*user\\s*\\||(?:^|\\n)\\s*(?:user\\s*\\||\\|\\s*user|assistant\\s*\\|)/im.test(text);
      const canvases = [...document.querySelectorAll('.charts canvas')];
      const canvasDiagnostics = canvases.map(canvas => {
        const context = canvas.getContext('2d');
        const pixels = context?.getImageData(0, 0, canvas.width, canvas.height).data ?? [];
        let paintedPixels = 0;
        for (let index = 3; index < pixels.length; index += 4) {
          if (pixels[index] > 0) paintedPixels++;
        }
        const rect = canvas.getBoundingClientRect();
        return { width: canvas.width, height: canvas.height, cssWidth: rect.width, cssHeight: rect.height, paintedPixels };
      });
      const chartLayoutDiagnostics = [...document.querySelectorAll('.chart-card')].map(card => {
        const canvas = card.querySelector('canvas');
        const title = card.querySelector('h4');
        const cardRect = card.getBoundingClientRect();
        const canvasRect = canvas.getBoundingClientRect();
        const titleRect = title.getBoundingClientRect();
        const style = getComputedStyle(card);
        const inlineStart = parseFloat(style.paddingInlineStart);
        const inlineEnd = parseFloat(style.paddingInlineEnd);
        const paddingBottom = parseFloat(style.paddingBottom);
        return {
          canvasWithinCard: canvasRect.left >= cardRect.left + inlineStart - 1
            && canvasRect.right <= cardRect.right - inlineEnd + 1
            && canvasRect.bottom <= cardRect.bottom - paddingBottom + 1,
          titleDoesNotOverlapCanvas: titleRect.bottom <= canvasRect.top,
          overflowBottom: Math.max(0, canvasRect.bottom - (cardRect.bottom - paddingBottom)),
          cardHeight: cardRect.height,
          canvasHeight: canvasRect.height
        };
      });
      const rawMarkdownPattern = /(^|\\n)\\s*(?:#{1,6}\\s+\\S|[*+-]\\s+\\S)|\\*\\*(?=\\S)[^*\\n]*\\S\\*\\*/m;
      const hasUnrenderedTextNode = bodies.some(body => [...body.childNodes]
        .some(node => node.nodeType === Node.TEXT_NODE && !!node.textContent?.trim()));
      const hasRawMarkdownInParagraph = bodies.some(body => [...body.querySelectorAll('p')]
        .some(paragraph => rawMarkdownPattern.test(paragraph.textContent || '')));
      return {
        hasRawMarkdown: hasUnrenderedTextNode || hasRawMarkdownInParagraph,
        hasChatTemplateArtifacts,
        semanticElements: document.querySelectorAll('.analysis-body :is(h1,h2,h3,h4,h5,h6,strong,ul,ol,table)').length,
        canvasDiagnostics,
        chartLayoutDiagnostics
      };
    })()`);

    checks.aiMarkdownIsRendered = !reportRendering.hasRawMarkdown && reportRendering.semanticElements > 0;
    checks.aiOutputHasNoChatTemplateArtifacts = !reportRendering.hasChatTemplateArtifacts;
    checks.behaviorChartsArePainted = reportRendering.canvasDiagnostics.length === 2
      && reportRendering.canvasDiagnostics.every(canvas => canvas.cssWidth > 0 && canvas.cssHeight > 0 && canvas.paintedPixels > 0);
    checks.behaviorChartsStayInsideCards = reportRendering.chartLayoutDiagnostics.length === 2
      && reportRendering.chartLayoutDiagnostics.every(chart => chart.canvasWithinCard && chart.titleDoesNotOverlapCanvas);
    console.log(JSON.stringify({ reportRendering }, null, 2));
  }

  await navigate('http://localhost:4200/student-analyzer/settings');
  await waitFor(() => evaluate('!!document.querySelector(".settings-page .primary")'), 'student-analyzer settings');
  checks.settingsUsePlatformTheme = await evaluate(`(() => {
    const probe = document.createElement('span');
    probe.style.backgroundColor = 'var(--brand-700)';
    document.body.appendChild(probe);
    const brandColor = getComputedStyle(probe).backgroundColor;
    probe.remove();
    return getComputedStyle(document.querySelector('.settings-page .primary')).backgroundColor === brandColor;
  })()`);
  console.log(JSON.stringify({ checks, cardLayout }, null, 2));
  if (Object.values(checks).some(value => !value)) process.exitCode = 1;
} finally {
  if (createdFileId && accessToken) {
    await fetch(`http://localhost:5264/api/v1/student-analyzer/files/${createdFileId}`, {
      method: 'DELETE',
      headers: { authorization: `Bearer ${accessToken}` }
    }).catch(() => undefined);
  }
  if (socket?.readyState === WebSocket.OPEN) socket.close();
  chrome.kill();
  await delay(500);
  try { rmSync(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 }); } catch { /* Chrome may release its profile after process exit. */ }
}
