using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
namespace so.api.Models;
public static class IsraeliPhone
{
 public const string Message="יש להזין מספר טלפון ישראלי תקין: 10 ספרות לנייד או 9 ספרות לקו נייח.";
 public static string? Normalize(string? value)
 {
  if(string.IsNullOrWhiteSpace(value)||value.Length>20)return null;
  var clean=Regex.Replace(value.Trim(),@"[\s()\-]","");
  if(clean.StartsWith("+972",StringComparison.Ordinal))clean="0"+clean[4..];
  return Regex.IsMatch(clean,@"^0(?:[23489][0-9]{7}|[57][0-9]{8})$")?clean:null;
 }
}
public sealed class IsraeliPhoneAttribute:ValidationAttribute
{
 public IsraeliPhoneAttribute():base(IsraeliPhone.Message){}
 public override bool IsValid(object? value)=>value is string text&&IsraeliPhone.Normalize(text)!=null;
}
