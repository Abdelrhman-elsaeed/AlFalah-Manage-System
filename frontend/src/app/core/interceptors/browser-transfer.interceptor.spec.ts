import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { browserTransferInterceptor, BROWSER_TRANSFER_TYPE } from './browser-transfer.interceptor';
import { environment } from '../../../environments/environment';

describe('Application binary transfer',()=>{
 let http:HttpClient;let backend:HttpTestingController;
 beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(withInterceptors([browserTransferInterceptor])),provideHttpClientTesting()]});http=TestBed.inject(HttpClient);backend=TestBed.inject(HttpTestingController);});
 afterEach(()=>backend.verify());
 it('receives identical bytes and Arabic filename without exposing an attachment request to browser download integration',async()=>{
  const result=firstValueFrom(http.get(environment.apiUrl+'/api/v1/school-duty/plans/roster.pdf',{responseType:'blob',observe:'response'}));
  const request=backend.expectOne(environment.apiUrl+'/api/v1/school-duty/plans/roster/content');
  expect(request.request.headers.get('Accept')).toBe(BROWSER_TRANSFER_TYPE);
  request.flush(new Blob(['%PDF-proof'],{type:BROWSER_TRANSFER_TYPE}),{headers:{'Content-Type':BROWSER_TRANSFER_TYPE,'X-AlFalah-Content-Type':'application/pdf','X-AlFalah-File-Name':encodeURIComponent('جدول المناوبة.pdf')}});
  const response=await result;expect(await response.body!.text()).toBe('%PDF-proof');expect(response.body!.type).toBe('application/pdf');expect(decodeURIComponent(response.headers.get('Content-Disposition')!.split("''")[1])).toBe('جدول المناوبة.pdf');
 });
 it('keeps real network failures visible instead of pretending that another program completed the download',async()=>{
  const result=firstValueFrom(http.get(environment.apiUrl+'/api/v1/storage/drive/content',{responseType:'blob'}));
  backend.expectOne(environment.apiUrl+'/api/v1/storage/drive/content').error(new ProgressEvent('error'));
  await expectAsync(result).toBeRejectedWith(jasmine.objectContaining({status:0}));
 });
 it('does not alter external requests, JSON or legacy ordinary binary responses',async()=>{
  const result=firstValueFrom(http.get('https://example.invalid/image',{responseType:'blob'}));const r=backend.expectOne('https://example.invalid/image');expect(r.request.headers.has('Accept')).toBeFalse();r.flush(new Blob(['image'],{type:'image/png'}));expect((await result).type).toBe('image/png');
  http.get(environment.apiUrl+'/api/v1/storage/context').subscribe();const json=backend.expectOne(environment.apiUrl+'/api/v1/storage/context');expect(json.request.headers.has('Accept')).toBeFalse();json.flush({});
 });
});
