using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
namespace so.api.Models;

public sealed class Customer : IdentityUser<int>
{
 [NotMapped] public bool EmailVerified { get => EmailConfirmed; set => EmailConfirmed = value; }
 public string? VerificationHash {get;set;}
 public DateTimeOffset? VerificationExpiresAt {get;set;}
 public string? ResetHash {get;set;}
 public DateTimeOffset? ResetExpiresAt {get;set;}
 [MaxLength(150)] public string Name {get;set;}="";
 public string SessionId {get;set;}="";
 public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow;
}
