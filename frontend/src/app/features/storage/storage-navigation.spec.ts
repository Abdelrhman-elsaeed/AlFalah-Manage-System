import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { firstValueFrom, isObservable, of, throwError } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { StorageApiService } from './storage-api.service';
import { VisitArchiveApiService } from './visit-archive-api.service';
import { storageGuard } from './storage.guard';

describe('Personal storage bookmarks after management grant',()=>{
 let api:jasmine.SpyObj<StorageApiService>;let router:Router;
 beforeEach(()=>{api=jasmine.createSpyObj('storage',['accessInfo']);TestBed.configureTestingModule({providers:[{provide:AuthService,useValue:{isAuthenticated:()=>true}},{provide:StorageApiService,useValue:api},{provide:VisitArchiveApiService,useValue:{}},Router]});router=TestBed.inject(Router);});
 async function enter(){const r=TestBed.runInInjectionContext(()=>storageGuard({data:{own:true}} as unknown as ActivatedRouteSnapshot,{url:'/instructor/my-files'} as RouterStateSnapshot));return isObservable(r)?firstValueFrom(r):await r;}
 it('redirects a live storage administrator to school files without requesting teacher folders',async()=>{api.accessInfo.and.returnValue(of({canManage:true,canReviewEvidence:true}));const target=await enter();expect(router.serializeUrl(target as UrlTree)).toBe('/school-manager/storage');expect(api.accessInfo).toHaveBeenCalledOnceWith(false);});
 it('preserves ordinary teacher access and its separate server authorization',async()=>{api.accessInfo.and.callFake(own=>own?of({canManage:true,canReviewEvidence:false}):throwError(()=>({status:403})));expect(await enter()).toBeTrue();expect(api.accessInfo.calls.allArgs()).toEqual([[false],[true]]);});
 it('does not infer a school administration grant from a role or a denied personal request',async()=>{api.accessInfo.and.returnValue(throwError(()=>({status:403})));expect(router.serializeUrl(await enter() as UrlTree)).toBe('/unauthorized');});
});
