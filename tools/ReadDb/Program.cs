using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Data.Sqlite;

var db = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sovrant", "data", "sovrant.db");
var newPassword = args.Length > 0 ? args[0] : "Admin123!";

var csb = new SqliteConnectionStringBuilder { DataSource = db };
using var conn = new SqliteConnection(csb.ToString());
conn.Open();

// Generate new Argon2id hash (same params as Argon2idPasswordHasher)
var salt = RandomNumberGenerator.GetBytes(32);
using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(newPassword))
{
    Salt = salt, Iterations = 3, MemorySize = 65536, DegreeOfParallelism = 1
};
var hash = argon2.GetBytes(32);
var stored = $"{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hash)}";

using var cmd = conn.CreateCommand();
cmd.CommandText = "UPDATE users SET password_hash = $hash WHERE role = 'admin'";
cmd.Parameters.AddWithValue("$hash", stored);
var rows = cmd.ExecuteNonQuery();

Console.WriteLine($"Updated {rows} admin user(s). New password: {newPassword}");
