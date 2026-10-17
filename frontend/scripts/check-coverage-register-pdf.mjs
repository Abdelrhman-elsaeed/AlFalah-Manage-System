import assert from 'node:assert/strict';
import { chromium } from '@playwright/test';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
const root=fileURLToPath(new URL('../../',import.meta.url));
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-phase3/pdf');mkdirSync(output,{recursive:true});
const browser=await chromium.launch({headless:true}),results=[];
try {
  const page=await browser.newPage();await page.route('**/__coverage_pdf',r=>r.fulfill({contentType:'text/html',body:'<!doctype html><body style="margin:0;background:white"></body>'}));await page.goto('http://localhost:4200/__coverage_pdf');
  await page.evaluate(async source=>{window.registerPdf=await import(URL.createObjectURL(new Blob([source],{type:'application/javascript'})));window.registerPdf.GlobalWorkerOptions.workerSrc='http://localhost:4200/assets/pdfjs/pdf.worker.min.mjs';},readFileSync(resolve(root,'frontend/node_modules/pdfjs-dist/build/pdf.mjs'),'utf8'));
  for(const name of ['one','three','four','period-nine','long-signed','signed']) {
    const bytes=readFileSync(resolve(root,'.audit/standby-phase3/pdfs',name+'.pdf'));
    const pages=await page.evaluate(async bytes=>{
      const pdf=await window.registerPdf.getDocument({data:new Uint8Array(bytes),isEvalSupported:false}).promise,results=[];
      for(let n=1;n<=pdf.numPages;n++) {
        const p=await pdf.getPage(n),view=p.getViewport({scale:1}),items=(await p.getTextContent()).items;
        const canvas=document.createElement('canvas'),render=p.getViewport({scale:1.5});canvas.width=Math.ceil(render.width);canvas.height=Math.ceil(render.height);await p.render({canvasContext:canvas.getContext('2d'),viewport:render}).promise;
        const ops=await p.getOperatorList();
        results.push({width:view.width,height:view.height,text:items.map(i=>i.str||'').join(' '),outside:items.filter(i=>i.str?.trim()&&(i.transform[4]<-1||i.transform[4]+i.width>view.width+2||i.transform[5]<0||i.transform[5]>view.height+2)).length,images:ops.fnArray.filter(f=>f===window.registerPdf.OPS.paintImageXObject||f===window.registerPdf.OPS.paintInlineImageXObject).length,png:canvas.toDataURL().split(',')[1]});
      }await pdf.destroy();return results;
    },[...bytes]);
    if(['one','three','period-nine'].includes(name))assert.equal(pages.length,1,name);
    if(name==='four')assert.equal(pages.length,2);
    if(name==='long-signed')assert.ok(pages.length>=3);
    const all=pages.map(p=>p.text).join(' ');
    for(const [i,p] of pages.entries()) {
      assert.ok(Math.abs(p.width-595.28)<2&&Math.abs(p.height-841.89)<2);assert.equal(p.outside,0,`${name} page ${i+1} text bounds`);
      assert.ok(p.text.includes('QF71-B-2-1'),`${name} repeated header`);
      if(i===0||name==='long-signed'&&i===pages.length-1)writeFileSync(resolve(output,`${name}-${i+1}.png`),Buffer.from(p.png,'base64'));
    }
    // The existing Amiri/QuestPDF output maps some shaped Arabic glyphs to NUL in ToUnicode.
    // Use fixture-only ASCII end markers to verify full continuation, plus independent raster review.
    if(name==='long-signed') {assert.ok(all.includes('END-EXECUTION-210'));assert.ok(all.includes('END-NOTES-120'));}
    if(name==='period-nine')assert.ok(all.includes('٩'));
    if(name==='signed')assert.ok(pages.reduce((n,p)=>n+p.images,0)>3,'signature images must be rendered');
    writeFileSync(resolve(output,name+'.pdf'),bytes);results.push({case:name,pages:pages.length,portraitA4:true,noTextOutsidePage:true,repeatedHeader:true,images:pages.map(p=>p.images),continuationEndMarkers:name==='long-signed'?true:undefined,shapedArabicNullMappings:[...all].filter(c=>c==='\0').length});
  }
  writeFileSync(resolve(output,'pdf-results.json'),JSON.stringify({at:new Date().toISOString(),results},null,2));console.log('PASS: 6 actual PDF cases, A4 portrait, source group capacity, continuations, signatures and text bounds.');
} finally {await browser.close();}
