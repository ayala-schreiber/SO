using System.Security.Cryptography;
namespace so.api.Models;
public static class PublicOrderCode
{
 public const string Alphabet="ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
 public static string Create()=>"SO-"+new string(Enumerable.Range(0,8).Select(_=>Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
}
