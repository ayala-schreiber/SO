using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

if (args.Length != 1 || !File.Exists(Path.Combine(args[0], "so.api.csproj")))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/So.AdminSetup -- C:\\SO\\so.api");
    return 1;
}
Console.Write("Owner username: ");
var username = Console.ReadLine()?.Trim();
if (string.IsNullOrWhiteSpace(username) || username.Length > 100) { Console.Error.WriteLine("Invalid username."); return 1; }
Console.Write("Password (at least 14 characters; hidden): ");
var password = ReadPassword();
Console.Write("Confirm password (hidden): ");
var confirmation = ReadPassword();
if (password.Length < 14 || password.Length > 256 || password != confirmation)
{ Console.Error.WriteLine("Passwords must match and contain 14–256 characters."); return 1; }
var hash = new PasswordHasher<string>(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 })).HashPassword(username, password);
var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true };
start.ArgumentList.Add("user-secrets"); start.ArgumentList.Add("set"); start.ArgumentList.Add("--project"); start.ArgumentList.Add(Path.GetFullPath(args[0]));
using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
process.StandardInput.Write(JsonSerializer.Serialize(new Dictionary<string,string> { ["Admin:Username"] = username, ["Admin:PasswordHash"] = hash }));
process.StandardInput.Close();
process.WaitForExit();
if (process.ExitCode == 0) Console.WriteLine("Owner configured locally. Restart the development API. No password was saved in the project.");
return process.ExitCode;

static string ReadPassword()
{
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
        else if (!char.IsControl(key.KeyChar) && value.Length < 257) value.Append(key.KeyChar);
    }
}
