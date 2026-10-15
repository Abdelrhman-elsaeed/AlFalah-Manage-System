import { TestBed } from '@angular/core/testing';
import { provideHttpClient, HttpEventType, HttpResponse } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { StorageDriveBrowserComponent } from './storage-drive-browser.component';
import { StorageApiService } from './storage-api.service';
import { ApiResponse } from '../../core/models/api-response.model';
import { StorageUpload } from './storage.models';

describe('Native storage uses the existing library workflow',()=>{
 let c:StorageDriveBrowserComponent;let api:jasmine.SpyObj<StorageApiService>;let http:HttpTestingController;
 beforeEach(async()=>{api=jasmine.createSpyObj('library',['createFolder','upload','invalidateContext','contextInfo']);const params=convertToParamMap({drive:'scoped-folder'});TestBed.configureTestingModule({imports:[StorageDriveBrowserComponent,TranslateModule.forRoot()],providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([]),{provide:ActivatedRoute,useValue:{queryParamMap:of(params),snapshot:{queryParamMap:params}}},{provide:StorageApiService,useValue:api}]});c=TestBed.createComponent(StorageDriveBrowserComponent).componentInstance;http=TestBed.inject(HttpTestingController);http.expectOne(r=>r.url.endsWith('/storage/drive')).flush({isSuccess:true,data:{name:'Library',handle:'scoped-folder',breadcrumbs:[],items:[],canManage:false,writableFolderId:17}});await Promise.resolve();await Promise.resolve();});
 afterEach(()=>http.verify());
 it('does not create a folder in a read-only archive even when its client state is manipulated',async()=>{c.data.set({...c.data()!,writableFolderId:null});c.folderName='Unsafe';await c.create();expect(api.createFolder).not.toHaveBeenCalled();});
 it('reuses the existing folder endpoint with its server-resolved ledger folder',async()=>{spyOn(c,'load').and.resolveTo();api.createFolder.and.returnValue(of({id:18,parentFolderId:17,displayName:'New',kind:'SchoolLibrary',rowVersion:'v1'}));c.folderName='New';await c.create();expect(api.createFolder).toHaveBeenCalledOnceWith(17,'New');});
 it('retains the same upload identity and destination on a lost response and retry',()=>{spyOn(c,'showError');spyOn(c,'load').and.resolveTo();api.upload.and.returnValues(throwError(()=>({status:0})),of(new HttpResponse({body:{isSuccess:true,data:{status:'Completed',operationId:3}} as ApiResponse<StorageUpload>})));const file=new File(['%PDF-proof'],'proof.pdf');c.selectedUpload({target:{files:[file],value:'proof.pdf'}} as unknown as Event);expect(c.canRetryUpload).toBeTrue();c.retryUpload();expect(api.upload).toHaveBeenCalledTimes(2);const [a,b]=api.upload.calls.allArgs();expect(a).toEqual(b);expect(a.slice(0,3)).toEqual([false,17,file]);expect(c.canRetryUpload).toBeFalse();});
 it('opening a non-previewable file shows its details without silently downloading it',async()=>{spyOn(URL,'createObjectURL');await c.file({handle:'file',name:'Sheet.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',size:90,modifiedAt:null,isFolder:false,canModify:false,protection:null},false);expect(c.fileInfo?.name).toBe('Sheet.xlsx');http.expectNone(r=>r.url.endsWith('/content'));expect(URL.createObjectURL).not.toHaveBeenCalled();expect(c.busy()).toBeFalse();});
 it('resolves old folder and file links into this same native browser',async()=>{
  const route=TestBed.inject(ActivatedRoute);Object.assign(route.snapshot,{queryParamMap:convertToParamMap({folder:'17',file:'9'})});const pending=c.load();
  http.expectOne(r=>r.url.endsWith('/drive/resolve')&&r.params.get('folderId')==='17'&&r.params.get('fileId')==='9').flush({isSuccess:true,data:{handle:'resolved-folder',openFile:{handle:'file',storedFileId:9,folderId:17,name:'Sheet.xlsx',mimeType:'application/x-sheet',isFolder:false}}});await Promise.resolve();await Promise.resolve();
  http.expectOne(r=>r.url.endsWith('/drive')&&r.params.get('handle')==='resolved-folder').flush({isSuccess:true,data:{name:'Library',handle:'resolved-folder',breadcrumbs:[],items:[],canManage:false,writableFolderId:17}});await pending;expect(c.fileInfo?.storedFileId).toBe(9);expect(c.data()?.handle).toBe('resolved-folder');
 });
 it('preserves historical-year read-only behavior in the unified browser',async()=>{
  Object.assign(TestBed.inject(ActivatedRoute).snapshot,{queryParamMap:convertToParamMap({drive:'scoped-folder',academicYearId:'2'})});api.contextInfo.and.returnValue(of({schoolId:18,schoolName:'Synthetic',canManage:true,isTeacher:false,connectionState:'Connected',academicYearId:1}));const pending=c.load();http.expectOne(r=>r.url.endsWith('/drive')).flush({isSuccess:true,data:{name:'Library',handle:'scoped-folder',breadcrumbs:[],items:[],canManage:false,writableFolderId:17}});await pending;expect(c.readonlyYear).toBeTrue();expect(c.data()?.writableFolderId).toBeNull();
 });
});
