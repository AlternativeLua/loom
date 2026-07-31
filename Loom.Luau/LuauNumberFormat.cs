using System.Globalization;

namespace Loom.Luau;

// Mirrors Luau's VM/src/lnumprint.cpp (Schubfach shortest round-trip formatting) and VM/src/lobject.cpp's
// luaO_str2d (strtod + "0x..." hex-integer fallback), verified against the real luau.exe interpreter rather
// than derived from the classic Lua "%.14g" behavior, which Luau's custom formatter does not follow.
public static class LuauNumberFormat
{
    public static string ToLuauString(double value)
    {
        if (double.IsNaN(value))
            return "nan";

        if (double.IsPositiveInfinity(value))
            return "inf";

        if (double.IsNegativeInfinity(value))
            return "-inf";

        var negative = BitConverter.DoubleToInt64Bits(value) < 0;
        if (value == 0.0)
            return negative ? "-0" : "0";

        var (digits, k) = ShortestDigits(Math.Abs(value));
        var declen = digits.Length;
        var dot = declen + k;
        var sign = negative ? "-" : "";

        if (dot < -5 || dot > 21)
        {
            var mantissa = digits.Length > 1 ? $"{digits[..1]}.{digits[1..]}" : digits;
            var exponent = dot - 1;
            return $"{sign}{mantissa}e{(exponent < 0 ? '-' : '+')}{Math.Abs(exponent).ToString("D2", CultureInfo.InvariantCulture)}";
        }

        if (dot <= 0)
            return $"{sign}0.{new string('0', -dot)}{digits}";

        if (dot == declen)
            return sign + digits;

        if (dot < declen)
            return $"{sign}{digits[..dot]}.{digits[dot..]}";

        return $"{sign}{digits}{new string('0', dot - declen)}";
    }

    // Extracts the shortest round-trippable significant-digit string and decimal exponent k, such that
    // value == (digits as integer) * 10^k. .NET's default double formatting has produced this same
    // shortest/nearest/ties-to-even decimal expansion since .NET Core 3.0, matching what Schubfach computes;
    // only the fixed/scientific layout choice below (Luau uses a flat dot in [-5, 21] rule) differs from .NET's own.
    private static (string Digits, int K) ShortestDigits(double abs)
    {
        var s = abs.ToString(CultureInfo.InvariantCulture);
        var eIndex = s.IndexOf('E');
        string mantissa;
        var exp = 0;
        if (eIndex >= 0)
        {
            mantissa = s[..eIndex];
            exp = int.Parse(s[(eIndex + 1)..], CultureInfo.InvariantCulture);
        }
        else
        {
            mantissa = s;
        }

        var dotIndex = mantissa.IndexOf('.');
        string digits;
        int fracLen;
        if (dotIndex >= 0)
        {
            digits = mantissa[..dotIndex] + mantissa[(dotIndex + 1)..];
            fracLen = mantissa.Length - dotIndex - 1;
        }
        else
        {
            digits = mantissa;
            fracLen = 0;
        }

        var k = exp - fracLen;

        var start = 0;
        while (start < digits.Length - 1 && digits[start] == '0')
            start++;
        digits = digits[start..];

        var end = digits.Length;
        while (end > 1 && digits[end - 1] == '0')
        {
            end--;
            k++;
        }

        return (digits[..end], k);
    }

    public static bool TryParse(string input, out double result)
    {
        result = 0;
        var n = input.Length;
        var i = 0;

        while (i < n && IsSpace(input[i]))
            i++;

        var start = i;
        var negative = false;
        if (i < n && (input[i] == '+' || input[i] == '-'))
        {
            negative = input[i] == '-';
            i++;
        }

        if (i + 1 < n && input[i] == '0' && (input[i + 1] == 'x' || input[i + 1] == 'X'))
        {
            i += 2;
            var hexStart = i;
            while (i < n && Uri.IsHexDigit(input[i]))
                i++;

            if (i == hexStart)
                return false;

            var value = 0UL;
            for (var j = hexStart; j < i; j++)
                unchecked { value = value * 16 + (ulong)HexDigitValue(input[j]); }

            result = negative ? -(double)value : value;
        }
        else if (!TryParseSpecial(input, negative, ref i, out result) && !TryParseDecimal(input, negative, ref i, out result))
        {
            return false;
        }

        while (i < n && IsSpace(input[i]))
            i++;

        return i == n && i > start;
    }

    private static bool TryParseSpecial(string input, bool negative, ref int i, out double result)
    {
        var rest = input.AsSpan(i);
        if (rest.StartsWith("infinity", StringComparison.OrdinalIgnoreCase))
        {
            i += 8;
            result = negative ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        if (rest.StartsWith("inf", StringComparison.OrdinalIgnoreCase))
        {
            i += 3;
            result = negative ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        if (rest.StartsWith("nan", StringComparison.OrdinalIgnoreCase))
        {
            i += 3;
            result = double.NaN;
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryParseDecimal(string input, bool negative, ref int i, out double result)
    {
        result = 0;
        var n = input.Length;
        var digitsStart = i;
        var sawDigit = false;

        while (i < n && char.IsAsciiDigit(input[i]))
        {
            i++;
            sawDigit = true;
        }

        if (i < n && input[i] == '.')
        {
            i++;
            while (i < n && char.IsAsciiDigit(input[i]))
            {
                i++;
                sawDigit = true;
            }
        }

        if (!sawDigit)
            return false;

        if (i < n && (input[i] == 'e' || input[i] == 'E'))
        {
            var backtrack = i;
            i++;
            if (i < n && (input[i] == '+' || input[i] == '-'))
                i++;

            var expDigitsStart = i;
            while (i < n && char.IsAsciiDigit(input[i]))
                i++;

            if (i == expDigitsStart)
                i = backtrack;
        }

        var numberText = (negative ? "-" : "") + input[digitsStart..i];
        return double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

    private static int HexDigitValue(char c) =>
        c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            _ => c - 'A' + 10
        };
}
