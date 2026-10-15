import {chromium} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const browser=await chromium.launch({headless:true});
const output=resolve('../docs/specs/school-duty/verification/roster-source-pdf');mkdirSync(output,{recursive:true});
const pdfjs=readFileSync(resolve('node_modules/pdfjs-dist/build/pdf.mjs'),'utf8');
const cases=['source-eighteen-weeks','source-full-year','source-long-names-closures','source-partial-weeks','source-seven-study-days'];
try{
 const page=await browser.newPage();await page.route('**/__roster_inspection',r=>r.fulfill({contentType:'text/html',body:'<!doctype html><html><body style="margin:0;background:white"></body></html>'}));await page.goto('http://localhost:4200/__roster_inspection');
 await page.evaluate(async source=>{window.rosterPdfJs=await import(URL.createObjectURL(new Blob([source],{type:'application/javascript'})));window.rosterPdfJs.GlobalWorkerOptions.workerSrc='http://localhost:4200/assets/pdfjs/pdf.worker.min.mjs';},pdfjs);
 const results=[];
 for(const name of [...cases,'actual-school-roster']){
  const path=name==='actual-school-roster'?resolve('../.audit/roster-source-20261015/actual-roster.pdf'):resolve('../.audit/roster-source-20261015/pdf-v5',name+'.pdf');
  const pages=await page.evaluate(async bytes=>{
   const pdf=await window.rosterPdfJs.getDocument({data:new Uint8Array(bytes),cMapUrl:'http://localhost:4200/assets/pdfjs/cmaps/',cMapPacked:true}).promise,images=[];
   for(let n=1;n<=pdf.numPages;n++){const p=await pdf.getPage(n),view=p.getViewport({scale:1.4}),canvas=document.createElement('canvas');canvas.width=Math.ceil(view.width);canvas.height=Math.ceil(view.height);await p.render({canvasContext:canvas.getContext('2d'),viewport:view}).promise;images.push(canvas.toDataURL().split(',')[1]);}
   await pdf.destroy();return images;
  },[...readFileSync(path)]);
  for(let i=0;i<pages.length;i++)writeFileSync(resolve(output,`pdfjs-${name}-${i+1}.png`),Buffer.from(pages[i],'base64'));
  results.push({file:name+'.pdf',pages:pages.length,engine:'PDF.js4.10.38',actualCanvasRendering:true});
 }
 writeFileSync(resolve(output,'pdfjs-results.json'),JSON.stringify(results,null,2));console.log('Actual PDF.js canvas rendering complete for all six documents.');
}finally{await browser.close();}
