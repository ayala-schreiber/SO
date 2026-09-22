using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace so.api.Models;
public sealed class ShopOrder
{
 [MaxLength(30)] public string FulfillmentStatus {get;set;}="Pending";
 public string FulfillmentHistoryJson {get;set;}="[]";
 [MaxLength(30)]public string? PaymentMethod {get;set;}
 [MaxLength(254)]public string? PaymentRecipient {get;set;}
 [MaxLength(100)]public string? PaymentRecipientName {get;set;}
 [MaxLength(100)]public string? PaymentReference {get;set;}
 [MaxLength(150)]public string? PaymentConfirmedBy {get;set;}
 public DateTimeOffset? ReservationExpiresAt {get;set;}
 public DateTimeOffset? PaidAt {get;set;}
 [MaxLength(11)] public string PublicCode {get;set;}=PublicOrderCode.Create();
 [MaxLength(30)] public string? LegacyNumber {get;set;}
 public int Id {get;set;}
 public Guid RequestKey {get;set;}
 public string RequestHash {get;set;}="";
 public int? CustomerId {get;set;}
 public Customer? Customer {get;set;}
 [MaxLength(150)] public string ContactName {get;set;}="";
 [MaxLength(254)] public string Email {get;set;}="";
 [MaxLength(30)] public string Phone {get;set;}="";
 [MaxLength(350)] public string Address {get;set;}="";
 [MaxLength(10)] public string? PostalCode {get;set;}
 [MaxLength(500)] public string? DeliveryNotes {get;set;}
 public bool Pickup {get;set;}
 public string ItemsJson {get;set;}="[]";
 public decimal Discount {get;set;}
 [MaxLength(30)] public string? CouponCode {get;set;}
 public decimal Subtotal {get;set;}
 public decimal DeliveryFee {get;set;}
 [MaxLength(30)] public string Status {get;set;}="AwaitingPayment";
 public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow;
 public int Version {get;set;}
 public object View(bool includeInternal=false)=>new {Id=includeInternal?(object)Id:PublicCode,Number=PublicCode,LegacyNumber=includeInternal?LegacyNumber:null,ContactName,Email,Phone,Address,Pickup,PostalCode,DeliveryNotes,
  Items=JsonSerializer.Deserialize<OrderLine[]>(ItemsJson),Subtotal,Discount,CouponCode,DeliveryFee,Total=Subtotal-Discount+DeliveryFee,Status,PaymentMethod,PaymentRecipient,PaymentRecipientName,PaidAt,FulfillmentStatus,FulfillmentHistory=JsonSerializer.Deserialize<FulfillmentEvent[]>(FulfillmentHistoryJson),CreatedAt,ReservationExpiresAt,Version};
}
public sealed record OrderLine(int ProductId,string Name,string? Color,string Size,decimal UnitPrice,int Quantity);

public sealed record FulfillmentEvent(string Status,DateTimeOffset At);
