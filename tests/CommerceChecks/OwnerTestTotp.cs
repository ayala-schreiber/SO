using System.Security.Cryptography;
using System.Buffers.Binary;

// Independent test generator, never used by the application to validate authentication.
static class OwnerTestTotp
{
 public static string Code(string base32, long? seconds = null) {
  const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  var bytes=new List<byte>();int value=0,bits=0;
  foreach(var c in base32){value=(value<<5)|alphabet.IndexOf(c);bits+=5;if(bits>=8){bits-=8;bytes.Add((byte)(value>>bits));}}
  var counter=new byte[8];BinaryPrimitives.WriteInt64BigEndian(counter,(seconds??DateTimeOffset.UtcNow.ToUnixTimeSeconds())/30);
  var hash=HMACSHA1.HashData(bytes.ToArray(),counter);int offset=hash[^1]&15;
  return ((BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset,4))&0x7fffffff)%1000000).ToString("D6");
 }
}
