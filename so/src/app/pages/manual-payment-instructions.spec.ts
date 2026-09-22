import {vi} from 'vitest';
import {TestBed} from '@angular/core/testing';import {ManualPaymentInstructions} from './manual-payment-instructions';
describe('Manual payment instructions',()=>{
 const order={status:'AwaitingPaymentApproval',paymentMethod:'ManualBit',paymentRecipient:'0501234567',paymentRecipientName:'Test recipient',number:'SO-000042',total:289};
 it('shows the order-specific recipient, exact amount and pending status',()=>{const f=TestBed.createComponent(ManualPaymentInstructions);f.componentRef.setInput('order',order);f.detectChanges();expect(f.nativeElement.textContent).toContain('0501234567');expect(f.nativeElement.textContent).toContain('289');expect(f.nativeElement.textContent).toContain('SO-000042');expect(f.nativeElement.textContent).toContain('ממתינה לאישור תשלום');});
 it('shows the reservation deadline and stops transfer instructions at expiry',()=>{vi.useFakeTimers();try{const f=TestBed.createComponent(ManualPaymentInstructions);f.componentRef.setInput('order',{...order,reservationExpiresAt:new Date(Date.now()+60000).toISOString()});f.detectChanges();expect(f.nativeElement.textContent).toContain('1:00');vi.advanceTimersByTime(61000);f.detectChanges();expect(f.nativeElement.textContent).toContain('זמן שמירת המלאי הסתיים');expect(f.nativeElement.querySelector('ol')).toBeNull();f.destroy();}finally{vi.useRealTimers();}});
 it('removes transfer instructions once paid to avoid a second payment',()=>{const f=TestBed.createComponent(ManualPaymentInstructions);f.componentRef.setInput('order',{...order,status:'Paid'});f.detectChanges();expect(f.nativeElement.querySelector('.transfer-card')).toBeNull();});
 it('does not ask for a transfer on a cancelled order',()=>{const f=TestBed.createComponent(ManualPaymentInstructions);f.componentRef.setInput('order',{...order,status:'Cancelled'});f.detectChanges();expect(f.nativeElement.querySelector('.transfer-card')).toBeNull();});
});
describe('Declaring that the transfer was made',()=>{
 const base={paymentMethod:'ManualBit',paymentRecipient:'0501234567',paymentRecipientName:'Test recipient',number:'SO-AB23CD45',total:289};
 const render=(over:Record<string,unknown>)=>{const f=TestBed.createComponent(ManualPaymentInstructions);f.componentRef.setInput('order',{...base,...over});f.detectChanges();return f;};

 it('asks an unpaid order to transfer and offers the declaration button',()=>{
  const f=render({status:'AwaitingPayment'});
  expect(f.nativeElement.querySelector('.transfer-card')).not.toBeNull();
  expect(f.nativeElement.textContent).toContain('ממתינה לתשלום');
  expect(f.nativeElement.querySelector('button.declare-paid')).not.toBeNull();
 });

 it('emits the declaration once the shopper presses the button',()=>{
  const f=render({status:'AwaitingPayment'});const seen:unknown[]=[];
  f.componentInstance.declared.subscribe(()=>seen.push(true));
  f.nativeElement.querySelector('button.declare-paid').click();
  expect(seen.length).toBe(1);
 });

 it('hides the button while the request is in flight',()=>{
  const f=TestBed.createComponent(ManualPaymentInstructions);
  f.componentRef.setInput('order',{...base,status:'AwaitingPayment'});f.componentRef.setInput('busy',true);f.detectChanges();
  expect(f.nativeElement.querySelector('button.declare-paid').disabled).toBe(true);
 });

 it('does not offer the button again once the shop is already checking the payment',()=>{
  const f=render({status:'AwaitingPaymentApproval'});
  expect(f.nativeElement.querySelector('.transfer-card')).not.toBeNull();
  expect(f.nativeElement.querySelector('button.declare-paid')).toBeNull();
 });

 it('shows no transfer details for an order with no manual method chosen',()=>{
  const f=render({status:'AwaitingPayment',paymentMethod:null});
  expect(f.nativeElement.querySelector('.transfer-card')).toBeNull();
 });
});
