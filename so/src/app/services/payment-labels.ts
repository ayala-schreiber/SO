export function orderStatus(status:string){return ({AwaitingPayment:'ממתינה לתשלום',AwaitingPaymentApproval:'ממתינה לאישור תשלום',Paid:'שולמה',Expired:'פג תוקף ללא תשלום',Cancelled:'בוטלה'} as Record<string,string>)[status]||status;}
export function paymentLabel(method?:string|null){return method==='ManualBit'?'bit אישי':method==='ManualPayPal'?'PayPal אישי':'טרם נבחר';}
export interface ManualMethod{id:string;label:string;available:boolean;recipient:string;recipientName:string;}
