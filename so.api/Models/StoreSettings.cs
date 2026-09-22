using System.ComponentModel.DataAnnotations;
namespace so.api.Models;
public sealed class StoreSettings
{
 public int Id {get;set;} = 1;
 [Required, StringLength(100)] public string StoreName {get;set;} = "SO";
 [StringLength(150)] public string? BusinessName {get;set;}
 [RegularExpression(@"^\d{9}$")] public string? BusinessNumber {get;set;}
 [EmailAddress, StringLength(200)] public string? ContactEmail {get;set;}
 [Phone, StringLength(30)] public string? ContactPhone {get;set;} = "0000000000";
 [Range(typeof(decimal),"0","10000")] public decimal DeliveryFee {get;set;} = 50;
 [Range(typeof(decimal),"0","1000000")] public decimal FreeDeliveryAbove {get;set;} = 500;
 [Range(1,1440)] public int ReservationMinutes {get;set;} = 30;
 [StringLength(500)] public string? PickupWhatsAppUrl {get;set;}
 public bool PickupEnabled {get;set;} = true;
 [StringLength(250)] public string PickupAddress {get;set;} = "כתובת לדוגמה";
 [Range(0,int.MaxValue)] public int Version {get;set;}
}
