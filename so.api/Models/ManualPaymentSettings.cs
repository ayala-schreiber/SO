using System.ComponentModel.DataAnnotations;
namespace so.api.Models;
public sealed class ManualPaymentSettings{
 public int Id {get;set;}=1;
 [MaxLength(254)]public string PayPalRecipient {get;set;}="";
 [MaxLength(100)]public string PayPalName {get;set;}="";
 [MaxLength(30)]public string BitPhone {get;set;}="0000000000";
 [MaxLength(100)]public string BitName {get;set;}="חנות הדגמה";
 public int Version {get;set;}
}
