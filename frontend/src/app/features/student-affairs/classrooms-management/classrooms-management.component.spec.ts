import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { ClassroomCatalogService, ClassroomCatalog } from '../../../core/services/classroom-catalog.service';
import { AuthService } from '../../../core/services/auth.service';
import { ClassroomsManagementComponent } from './classrooms-management.component';

describe('ClassroomsManagementComponent', () => {
  let api: jasmine.SpyObj<ClassroomCatalogService>;
  const catalogue: ClassroomCatalog = {context:{stage:'Primary',isManager:true,isMember:true,years:[{id:3,name:'2026',isActive:true}]},items:[],hasMore:false};
  beforeEach(async () => {
    api=jasmine.createSpyObj('ClassroomCatalogService',['all','create','edit','gradeLabel']);
    api.all.and.returnValue(of(catalogue)); api.gradeLabel.and.returnValue('Grade 1');
    await TestBed.configureTestingModule({imports:[ClassroomsManagementComponent,NoopAnimationsModule],providers:[
      {provide:ClassroomCatalogService,useValue:api},{provide:AuthService,useValue:{hasRole:()=>false,hasPermission:()=>false}}
    ]}).compileComponents();
  });
  it('offers manager controls without requiring secretary permissions', () => {
    const component=TestBed.createComponent(ClassroomsManagementComponent).componentInstance;
    expect(component.canManage()).toBeTrue(); expect(component.year()).toBe(3); expect(component.grades()).toEqual([1,2,3,4,5,6]);
  });
  it('keeps load failures visible with retry', () => {
    api.all.and.returnValue(throwError(()=>({status:500})));
    const component=TestBed.createComponent(ClassroomsManagementComponent).componentInstance;
    expect(component.loading()).toBeFalse(); expect(component.error()).not.toBe(''); expect(component.canManage()).toBeFalse();
  });
  it('validates the range and location before sending a single atomic batch', () => {
    const component=TestBed.createComponent(ClassroomsManagementComponent).componentInstance;
    component.openCreate(); component.save(); expect(api.create).not.toHaveBeenCalled();
    component.location='Floor 1'; component.firstSection=3; component.lastSection=2;
    component.save(); expect(api.create).not.toHaveBeenCalled();
    component.lastSection=5; api.create.and.returnValue(of({isSuccess:true,message:'',data:[],errors:[]})); component.save();
    expect(api.create).toHaveBeenCalledOnceWith(3,1,['3','4','5'],'Floor 1');
  });
  it('does not offer writes for past years or read-only school members', () => {
    api.all.and.returnValue(of({...catalogue,context:{...catalogue.context,isManager:false,years:[{id:3,name:'old',isActive:false}]}}));
    const component=TestBed.createComponent(ClassroomsManagementComponent).componentInstance;
    component.year.set(3); component.location='Floor'; component.save();
    expect(component.canManage()).toBeFalse(); expect(api.create).not.toHaveBeenCalled();
  });
  it('separates retained legacy stages instead of showing them under the current grade heading', () => {
    api.all.and.returnValue(of({...catalogue,items:[{id:7,label:'Legacy intermediate',stage:'Intermediate',gradeLevel:1} as any]}));
    const component=TestBed.createComponent(ClassroomsManagementComponent).componentInstance;
    expect(component.stages()).toEqual(['Primary','Intermediate']);expect(component.rows()).toEqual([]);
    component.stage.set('Intermediate');expect(component.grades()).toEqual([1,2,3]);expect(component.rows().map(c=>c.id)).toEqual([7]);
    component.location='Floor';component.save();expect(api.create).not.toHaveBeenCalled();
  });
});
