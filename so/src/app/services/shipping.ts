export interface ShippingSettings {deliveryFee:number;freeDeliveryAbove:number;pickupEnabled:boolean;pickupAddress:string;pickupWhatsAppUrl?:string|null;}
// השרת הוא הקובע. זהו חישוב התצוגה בלבד, והוא חייב להיות זהה בעגלה ובמסך התשלום,
// אחרת שני המסכים יציגו דמי משלוח שונים לאותה עגלה.
export function deliveryFee(settings:ShippingSettings|null,productsTotal:number,pickup:boolean){
 if(!settings)return 0;
 if(pickup&&settings.pickupEnabled)return 0;
 return productsTotal>=settings.freeDeliveryAbove?0:settings.deliveryFee;
}
