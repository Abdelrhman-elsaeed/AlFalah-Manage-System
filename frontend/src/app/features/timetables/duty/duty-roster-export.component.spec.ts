import { TestBed } from '@angular/core/testing';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, of } from 'rxjs';
import { DutyDocumentsService } from '../../../core/services/duty-documents.service';
import { DutyRosterExportComponent } from './duty-roster-export.component';

describe('Official roster export', () => {
  let component: DutyRosterExportComponent;
  let api: jasmine.SpyObj<DutyDocumentsService>;
  const pdf = () => new HttpResponse({ body: new Blob(['%PDF-'+ '0'.repeat(120)],{type:'application/pdf'}), headers:new HttpHeaders({'Content-Disposition':'attachment; filename="roster.pdf"'}) });
  beforeEach(() => {
    api=jasmine.createSpyObj('DutyDocumentsService',['roster']);
    TestBed.configureTestingModule({imports:[DutyRosterExportComponent,TranslateModule.forRoot()],providers:[{provide:DutyDocumentsService,useValue:api}]});
    component=TestBed.createComponent(DutyRosterExportComponent).componentInstance;component.termId=1;component.revision=7;
    spyOn(URL,'createObjectURL').and.returnValue('blob:synthetic-roster');spyOn(HTMLAnchorElement.prototype,'click');
  });
  it('downloads the authenticated complete plan revision once with its server filename', async()=>{
    api.roster.and.returnValue(of(pdf()));await component.download();
    expect(api.roster).toHaveBeenCalledOnceWith(1,7);expect(HTMLAnchorElement.prototype.click).toHaveBeenCalledTimes(1);expect(component.busy()).toBeFalse();
  });
  it('deduplicates in-flight clicks and discards a response for a changed plan revision',async()=>{
    const delayed=new Subject<HttpResponse<Blob>>();api.roster.and.returnValue(delayed);
    const first=component.download();await component.download();component.revision=8;delayed.next(pdf());delayed.complete();await first;
    expect(api.roster).toHaveBeenCalledTimes(1);expect(URL.createObjectURL).not.toHaveBeenCalled();
  });
  it('discards a response after changing the academic term',async()=>{
    const delayed=new Subject<HttpResponse<Blob>>();api.roster.and.returnValue(delayed);const first=component.download();component.termId=2;delayed.next(pdf());delayed.complete();await first;
    expect(URL.createObjectURL).not.toHaveBeenCalled();expect(component.busy()).toBeFalse();
  });
  it('does not download a successful non-PDF error response',async()=>{
    api.roster.and.returnValue(of(new HttpResponse({body:new Blob(['{"error":"unavailable"}'],{type:'application/json'})})));await component.download();
    expect(component.error()).not.toBe('');expect(URL.createObjectURL).not.toHaveBeenCalled();
  });
});
