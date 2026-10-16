import assert from 'node:assert/strict';
import {chromium} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';
const root=fileURLToPath(new URL('../../',import.meta.url));
const output=resolve(root,'docs/specs/classroom-catalog/verification/statement');mkdirSync(output,{recursive:true});
const browser=await chromium.launch({headless:true}),results=[];
try{
  const page=await browser.newPage();await page.route('**/__assignment_pdf',r=>r.fulfill({contentType:'text/html',body:'<!doctype html><body style="margin:0;background:white"></body>'}));await page.goto('http://localhost:4200/__assignment_pdf');
  await page.evaluate(async source=>{window.statementPdf=await import(URL.createObjectURL(new Blob([source],{type:'application/javascript'})));window.statementPdf.GlobalWorkerOptions.workerSrc='http://localhost:4200/assets/pdfjs/pdf.worker.min.mjs';},readFileSync(resolve(root,'frontend/node_modules/pdfjs-dist/build/pdf.mjs'),'utf8'));
  for(const name of ['reference-24','large-120','long-names','dense-appendix','primary-six','multi-stage','co-split-other','prefixed-year']){
    const bytes=readFileSync(resolve(root,'.audit/assignment-statement/pdf',name+'.pdf'));
    const pages=await page.evaluate(async bytes=>{
      const pdf=await window.statementPdf.getDocument({data:new Uint8Array(bytes),isEvalSupported:false}).promise,results=[];
      for(let n=1;n<=pdf.numPages;n++){const p=await pdf.getPage(n),view=p.getViewport({scale:1}),items=(await p.getTextContent()).items;
        const canvas=document.createElement('canvas'),render=p.getViewport({scale:1.5});canvas.width=Math.ceil(render.width);canvas.height=Math.ceil(render.height);await p.render({canvasContext:canvas.getContext('2d'),viewport:render}).promise;
        results.push({width:view.width,height:view.height,text:items.map(i=>i.str||'').join(' '),outside:items.filter(i=>i.str?.trim()&&(i.transform[4]<-1||i.transform[4]+i.width>view.width+2||i.transform[5]<0||i.transform[5]>view.height+2)).length,png:canvas.toDataURL().split(',')[1]});}
      await pdf.destroy();return results;
    },[...bytes]);
    if(name==='reference-24')assert.equal(pages.length,1);
    if(name==='large-120')assert.ok(pages.length>=5);
    if(name==='dense-appendix')assert.ok(pages.length>=2);
    for(const [i,p] of pages.entries()){assert.ok(Math.abs(p.width-841.89)<2&&Math.abs(p.height-595.28)<2);assert.equal(p.outside,0);writeFileSync(resolve(output,`${name}-${i+1}.png`),Buffer.from(p.png,'base64'));}
    const text=pages.map(p=>p.text).join(' ');for(let i=1;i<=(name==='large-120'?120:24);i++)assert.ok(text.includes(String(i).padStart(3,'0')),`Missing teacher ${i} in ${name}`);
    writeFileSync(resolve(output,name+'.pdf'),bytes);results.push({case:name,pages:pages.length,landscapeA4:true,noTextOutsidePage:true,allTeachersPresent:true,pdfJsCanvas:true});
  }
  writeFileSync(resolve(output,'pdf-results.json'),JSON.stringify({at:new Date().toISOString(),results},null,2));console.log('PASS: A4 landscape PDFs, reference24 on one page, all teachers, dense appendix, continuation and bounds.');
}finally{await browser.close();}
