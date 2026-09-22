import {Directive,forwardRef} from '@angular/core';
import {AbstractControl,NG_VALIDATORS,ValidationErrors,Validator} from '@angular/forms';
export function normalizePhone(value:string|null|undefined):string|null{
 if(!value||value.length>20)return null;
 let clean=value.trim().replace(/[\s()\-]/g,'');
 if(clean.startsWith('+972'))clean='0'+clean.slice(4);
 return /^0(?:[23489][0-9]{7}|[57][0-9]{8})$/.test(clean)?clean:null;
}
export function displayPhone(value:string):string{
 const clean=normalizePhone(value);if(!clean)return value;
 return clean.length===10?clean.slice(0,3)+'-'+clean.slice(3,6)+'-'+clean.slice(6):clean.slice(0,2)+'-'+clean.slice(2,5)+'-'+clean.slice(5);
}
@Directive({selector:'[soIsraeliPhone]',standalone:true,providers:[{provide:NG_VALIDATORS,useExisting:forwardRef(()=>IsraeliPhoneValidator),multi:true}]})
export class IsraeliPhoneValidator implements Validator{
 validate(control:AbstractControl):ValidationErrors|null{return !control.value||normalizePhone(control.value)?null:{israeliPhone:true};}
}
