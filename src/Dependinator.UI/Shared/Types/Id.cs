using System.Security.Cryptography;
using System.Text;

namespace Dependinator.UI.Shared.Types;

public record Id(string Value)
{
    private static readonly char[] Base62Chars =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();

    // public static implicit operator string(Id id) => id.Value;
    // public static implicit operator Id(string value) => new(value);

    protected static string ToId(string input, int length = 10)
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
