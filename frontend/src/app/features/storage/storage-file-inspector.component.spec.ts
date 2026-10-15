import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { StorageFileInspectorComponent } from './storage-file-inspector.component';
import { StorageApiService } from './storage-api.service';
import { StorageEvidenceApiService } from './evidence-api.service';

describe('Unified file inspector',()=>{
 let c:StorageFileInspectorComponent;let api:jasmine.SpyObj<StorageApiService>;let evidence:jasmine.SpyObj<StorageEvidenceApiService>;
 beforeEach(()=>{api=jasmine.createSpyObj('library',['details','contextInfo','rename','invalidateDetails']);evidence=jasmine.createSpyObj('evidence',['versionContent']);TestBed.configureTestingModule({imports:[StorageFileInspectorComponent,TranslateModule.forRoot()],providers:[{provide:StorageApiService,useValue:api},{provide:StorageEvidenceApiService,useValue:evidence}]});c=TestBed.createComponent(StorageFileInspectorComponent).componentInstance;c.fileId=9;c.folderId=17;
 api.contextInfo.and.returnValue(of({schoolId:18,schoolName:'Synthetic',canManage:true,canReviewEvidence:true,isTeacher:false,connectionState:'Connected',academicYearId:1}));api.details.and.returnValue(of({file:{storedFileId:9,folderId:17,displayName:'proof.pdf',size:30,mimeType:'application/pdf',uploadedAt:'2026-10-15',state:'Managed',isProtected:false,rowVersion:'v1'},versions:[]}));});
 it('uses the existing scoped record with a read-only historical context',async()=>{c.year=2;await c.load();expect(api.details).toHaveBeenCalledOnceWith(9,2,17,false);expect(c.historical).toBeTrue();expect(c.manage).toBeFalse();expect(c.review).toBeFalse();});
 it('clears retained details and permissions when the next scoped request is denied',async()=>{await c.load();expect(c.details).toBeDefined();const denied=spyOn(c.denied,'emit');api.details.and.returnValue(throwError(()=>({status:403})));await c.load();expect(c.details).toBeUndefined();expect(c.context).toBeUndefined();expect(denied).toHaveBeenCalledTimes(1);});
 it('does not report a 200 business failure as a successful rename',async()=>{await c.load();const changed=spyOn(c.changed,'emit'),failure=spyOn(c.failure,'emit');c.renameName='New.pdf';api.rename.and.returnValue(of({isSuccess:false,message:'Conflict',errors:[]}));await c.rename();expect(changed).not.toHaveBeenCalled();expect(failure).toHaveBeenCalledOnceWith('Conflict');});
});
