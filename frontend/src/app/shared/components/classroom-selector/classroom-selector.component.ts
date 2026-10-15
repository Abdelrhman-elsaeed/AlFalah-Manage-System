import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, OnInit, Output, forwardRef, inject } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MultiSelectModule } from 'primeng/multiselect';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ClassroomCatalogService, ClassroomOption } from '../../../core/services/classroom-catalog.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
@Component({selector:'app-classroom-selector',standalone:true,imports:[CommonModule,FormsModule,MultiSelectModule],
 providers:[{provide:NG_VALUE_ACCESSOR,useExisting:forwardRef(()=>ClassroomSelectorComponent),multi:true}],
 template:`<p-multiSelect [options]="groups" [group]="true" optionGroupLabel="label" optionGroupChildren="items" optionLabel="label" optionValue="id" optionDisabled="disabled" [filter]="true" filterBy="label" [showToggleAll]="false" display="chip" appendTo="body" [ngModel]="value" (ngModelChange)="select($event)" (onBlur)="touch()" [disabled]="disabled||loading||!!error" placeholder="اختر من فصول المدرسة" ariaLabel="اختيار فصول المدرسة" styleClass="w-full" [virtualScroll]="true" [virtualScrollItemSize]="44" [maxSelectedLabels]="30" />
 <small *ngIf="loading" role="status">جارٍ تحميل سجل الفصول…</small><p *ngIf="error" role="alert">{{error}} <button type="button" (click)="load()">إعادة المحاولة</button></p><small *ngIf="!loading&&!error&&!options.length">لم تُضف فصول لهذا العام. يضيفها المدير من «إدارة الفصول».</small>`,
 styles:[`:host{display:block;min-width:0;width:100%}small{display:block;color:var(--text-muted);font-size:12px;margin-top:8px}p{color:#a5352e;font-size:12px;line-height:1.8}button{font:inherit;min-height:44px;border:1px solid var(--border);border-radius:8px;background:white;color:var(--brand-900);cursor:pointer}`]})
export class ClassroomSelectorComponent implements OnInit,OnChanges,ControlValueAccessor {
 private readonly api=inject(ClassroomCatalogService);private readonly destroy=inject(DestroyRef);private sequence=0;private initialized=false;
 @Input() academicYearId?:number|null;@Output() ready=new EventEmitter<boolean>();
 options:ClassroomOption[]=[];value:number[]=[];disabled=false;loading=true;error='';
 private change:(value:number[])=>void=()=>{};touch:()=>void=()=>{};
 get groups(){const groups=new Map<string,{label:string;items:(ClassroomOption&{disabled:boolean})[]}>();for(const option of this.options){const key=option.stage+'-'+option.gradeLevel;let group=groups.get(key);if(!group){group={label:this.api.gradeLabel(option.stage,option.gradeLevel),items:[]};groups.set(key,group);}group.items.push({...option,label:option.label+(!option.isActive?' (غير نشط)':''),disabled:!option.isActive&&!this.value.includes(option.id)});}return [...groups.values()];}
 ngOnInit(){this.initialized=true;this.load();}ngOnChanges(){if(this.initialized)this.load();}
 load(){const request=++this.sequence;this.loading=true;this.error='';this.ready.emit(false);this.api.all(this.academicYearId||undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{if(request!==this.sequence)return;this.options=r.items;this.loading=false;this.ready.emit(true);},error:e=>{if(request!==this.sequence)return;this.options=[];this.loading=false;this.error=extractHttpErrorMessage(e)||'تعذر تحميل الفصول. أعد المحاولة قبل الحفظ.';}});}
 writeValue(value:number[]|null){this.value=value??[];}registerOnChange(fn:(v:number[])=>void){this.change=fn;}registerOnTouched(fn:()=>void){this.touch=fn;}setDisabledState(disabled:boolean){this.disabled=disabled;}
 select(value:number[]){this.value=[...new Set(value)];this.change(this.value);this.touch();}
}
