import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { Subject, of } from 'rxjs';
import { ClassroomCatalog, ClassroomCatalogService, ClassroomOption } from '../../../core/services/classroom-catalog.service';
import { ClassroomSelectorComponent } from './classroom-selector.component';

describe('ClassroomSelectorComponent',()=>{
  let api:jasmine.SpyObj<ClassroomCatalogService>;
  const data:ClassroomCatalog={context:{stage:'Primary',isManager:false,isMember:true,years:[]},items:[
    {id:1,stage:'Primary',gradeLevel:1,label:'First',isActive:true},
    {id:101,stage:'Primary',gradeLevel:2,label:'Selected inactive',isActive:false},
    {id:102,stage:'Primary',gradeLevel:2,label:'Unselected inactive',isActive:false}
  ] as ClassroomOption[],hasMore:false};
  beforeEach(async()=>{
    api=jasmine.createSpyObj('catalog',['all','gradeLabel']);api.all.and.returnValue(of(data));api.gradeLabel.and.callFake((_,grade)=>`Grade ${grade}`);
    await TestBed.configureTestingModule({imports:[ClassroomSelectorComponent,NoopAnimationsModule],providers:[{provide:ClassroomCatalogService,useValue:api}]}).compileComponents();
  });
  it('hydrates selected IDs, groups grades and keeps selected inactive options removable',()=>{
    const component=TestBed.createComponent(ClassroomSelectorComponent).componentInstance;
    component.writeValue([101]);component.ngOnInit();
    expect(component.value).toEqual([101]);expect(component.groups.length).toBe(2);
    expect(component.groups[1].items.find(x=>x.id===101)?.disabled).toBeFalse();
    expect(component.groups[1].items.find(x=>x.id===102)?.disabled).toBeTrue();
  });
  it('never clears form selection on failed lookup and keeps saving blocked',()=>{
    const response=new Subject<ClassroomCatalog>();api.all.and.returnValue(response);
    const component=TestBed.createComponent(ClassroomSelectorComponent).componentInstance;
    const readiness:boolean[]=[];component.ready.subscribe(value=>readiness.push(value));component.writeValue([101]);component.ngOnInit();
    response.error({status:503});expect(component.value).toEqual([101]);expect(readiness).toEqual([false]);expect(component.error).not.toBe('');
  });
  it('ignores a stale response after the year changed and does not mutate selected values',()=>{
    const old=new Subject<ClassroomCatalog>(),next=new Subject<ClassroomCatalog>();api.all.and.returnValues(old,next);
    const component=TestBed.createComponent(ClassroomSelectorComponent).componentInstance;
    component.writeValue([101]);component.ngOnInit();component.academicYearId=9;component.ngOnChanges();
    next.next({...data,items:[{...data.items[0],id:200}]});old.next(data);
    expect(component.options.map(x=>x.id)).toEqual([200]);expect(component.value).toEqual([101]);
  });
  it('emits only selected IDs without creating classroom text',()=>{
    const component=TestBed.createComponent(ClassroomSelectorComponent).componentInstance;
    let saved:number[]=[];component.registerOnChange(value=>saved=value);component.select([1,101,1]);
    expect(saved).toEqual([1,101]);
  });
});
