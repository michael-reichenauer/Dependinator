using System.Security.Cryptography;
using System.Text;

namespace Dependinator.Models;


public record Id
{
    private static readonly char[] Base62Chars =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();

    public string Value { get; init; }

    public Id(string value) => Value = GenerateBase62UniqueId(value);

    public static implicit operator string(Id id) => id.Value;
    public static implicit operator Id(string value) => new(value);

    static string GenerateBase62UniqueId(string input, int length = 10)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Base62Encode(bytes, length);
    }

    static string Base62Encode(byte[] bytes, int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = Base62Chars[bytes[i] % Base62Chars.Length];
        }

        return new string(chars);
    }
}

public record NodeId(string name) : Id(name);
public record LinkId(string sourceName, string targetName) : Id($"{sourceName}->{targetName}");
public record LineId(string sourceName, string targetName) : Id($"{sourceName}=>{targetName}");

