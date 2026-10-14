import { isDutyPdf } from './duty-pdf-content';
describe('Official duty PDF download validation',()=>{
  it('accepts PDF media type parameters while retaining original bytes',async()=>{
    const bytes='%PDF-'+ '0'.repeat(110);const blob=new Blob([bytes],{type:'application/pdf; charset=utf-8'});
    expect(await isDutyPdf(blob)).toBeTrue();expect(await blob.text()).toBe(bytes);
  });
  it('rejects successful HTML/JSON and corrupt PDF responses',async()=>{
    expect(await isDutyPdf(new Blob(['%PDF-'+ '0'.repeat(110)],{type:'text/html'}))).toBeFalse();
    expect(await isDutyPdf(new Blob(['{'+ '0'.repeat(110)],{type:'application/pdf'}))).toBeFalse();
    expect(await isDutyPdf(null)).toBeFalse();expect(await isDutyPdf(new Blob(['%PDF-'],{type:'application/pdf'}))).toBeFalse();
  });
});
