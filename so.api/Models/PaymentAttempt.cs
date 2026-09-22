using System.ComponentModel.DataAnnotations;
namespace so.api.Models;
public sealed class PaymentAttempt{
 public int Id {get;set;} public int OrderId {get;set;} public ShopOrder Order {get;set;}=null!;
 public Guid RequestKey {get;set;}
 [MaxLength(30)] public string State {get;set;}="Pending";
 [MaxLength(100)] public string? ProviderTransactionId {get;set;}
 public decimal Amount {get;set;}
 public DateTimeOffset ExpiresAt {get;set;}
 public DateTimeOffset CreatedAt {get;set;}
 public int Version {get;set;}
}
public sealed class StockHold{
 public int Id {get;set;} public int PaymentAttemptId {get;set;} public PaymentAttempt PaymentAttempt {get;set;}=null!;
 public int ProductId {get;set;} public Product Product {get;set;}=null!;public int Quantity {get;set;}
}
