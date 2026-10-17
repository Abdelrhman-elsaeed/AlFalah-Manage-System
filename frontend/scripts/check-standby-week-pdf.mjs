import assert from 'node:assert/strict';
import {chromium} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url)),output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-week/pdf');mkdirSync(output,{recursive:true});
const browser=await chromium.launch({headless:true});
try{
 const page=await browser.newPage();await page.route('**/__week_pdf',r=>r.fulfill({contentType:'text/html',body:'<!doctype html><body style="margin:0;background:white"></body>'}));await page.goto('http://localhost:4200/__week_pdf');
 await page.evaluate(async source=>{window.pdf=await import(URL.createObjectURL(new Blob([source],{type:'application/javascript'})));window.pdf.GlobalWorkerOptions.workerSrc='http://localhost:4200/assets/pdfjs/pdf.worker.min.mjs';},readFileSync(resolve(root,'frontend/node_modules/pdfjs-dist/build/pdf.mjs'),'utf8'));
 const bytes=readFileSync(resolve(root,'.audit/standby-week/pdfs/week.pdf'));
 const pages=await page.evaluate(async bytes=>{
  const pdf=await window.pdf.getDocument({data:new Uint8Array(bytes),isEvalSupported:false}).promise,results=[];
  for(let n=1;n<=pdf.numPages;n++){const p=await pdf.getPage(n),v=p.getViewport({scale:1}),items=(await p.getTextContent()).items,canvas=document.createElement('canvas'),render=p.getViewport({scale:1.5});canvas.width=Math.ceil(render.width);canvas.height=Math.ceil(render.height);await p.render({canvasContext:canvas.getContext('2d'),viewport:render}).promise;
   results.push({width:v.width,height:v.height,text:items.map(i=>i.str||'').join(' '),outside:items.filter(i=>i.str?.trim()&&(i.transform[4]<-1||i.transform[4]+i.width>v.width+2||i.transform[5]<0||i.transform[5]>v.height+2)).length,png:canvas.toDataURL().split(',')[1]});
  }await pdf.destroy();return results;
 },[...bytes]);
 writeFileSync(resolve(root,'.audit/standby-week/extracted-text.json'),JSON.stringify(pages.map(p=>p.text),null,2));
 assert.equal(pages.length,7);for(let i=0;i<7;i++){const p=pages[i],date=(18+i)+'-10-2026';
  // PDF.js extracts the existing RTL date runs as DD-MM-YYYY. The last run is the daily footer date, not the shared range or issue timestamp.
  assert.equal(p.text.match(/\d{2}-\d{2}-\d{4}/g)?.at(-1),date,`daily footer date on page ${i+1}`);assert.ok(p.text.includes('QF71-B-2-1'));assert.ok(Math.abs(p.width-595.28)<2&&Math.abs(p.height-841.89)<2);assert.equal(p.outside,0);if(i===0||i===6)writeFileSync(resolve(output,`week-${i+1}.png`),Buffer.from(p.png,'base64'));}
 writeFileSync(resolve(output,'week.pdf'),bytes);writeFileSync(resolve(output,'results.json'),JSON.stringify({at:new Date().toISOString(),pages:7,orderedDates:true,portraitA4:true,formOnEveryDay:true,noTextOutsidePage:true,includesEmptyDays:true},null,2));console.log('PASS: seven ordered daily A4 forms, exact dates and contained text.');
}finally{await browser.close();}
