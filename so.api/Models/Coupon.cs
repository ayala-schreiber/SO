using System.ComponentModel.DataAnnotations;
namespace so.api.Models;
public sealed class Coupon
{
 public bool IsArchived{get;set;}
 public int Id{get;set;}
 [Required,RegularExpression("^[A-Za-z0-9_-]{3,30}$"),MaxLength(30)]public string Code{get;set;}="";
 [Required,RegularExpression("^(Percent|Fixed)$"),MaxLength(10)]public string Kind{get;set;}="Percent";
 [Range(typeof(decimal),"0.01","1000000")]public decimal Value{get;set;}
 [Range(typeof(decimal),"0","1000000")]public decimal MinimumSubtotal{get;set;}
 public DateTimeOffset? ExpiresAt{get;set;}
 public bool Active{get;set;}=true;
 public int Version{get;set;}
}
