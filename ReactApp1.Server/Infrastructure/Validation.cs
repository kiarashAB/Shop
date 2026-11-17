using System.Text;
using System.Text.RegularExpressions;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Infrastructure;

/// اعتبارسنجی ورودی‌ها در سمت سرور (هرگز به اعتبارسنجی فرانت‌اند تکیه نمی‌کنیم).
public static class Validation
{
    public static string NormalizeDigits(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    /// شمارهٔ موبایل ایران را به شکل ۰۹xxxxxxxxx (ارقام انگلیسی) برمی‌گرداند.
    public static string NormalizePhone(string? raw)
    {
        var s = NormalizeDigits(raw ?? "").Trim().Replace(" ", "").Replace("-", "");
        if (s.StartsWith("+98")) s = "0" + s[3..];
        else if (s.StartsWith("0098")) s = "0" + s[4..];
        else if (s.StartsWith("98") && s.Length == 12) s = "0" + s[2..];
        if (!Regex.IsMatch(s, @"^09\d{9}$")) throw new DomainException("شماره موبایل معتبر نیست (مثال: 09123456789).");
        return s;
    }

    public static string Required(string? value, string label, int max = 200)
    {
        var t = value?.Trim();
        if (string.IsNullOrWhiteSpace(t)) throw new DomainException($"{label} را وارد کنید.");
        if (t.Length > max) throw new DomainException($"{label} بیش از حد طولانی است.");
        return t;
    }

    public static string? Optional(string? value, string label, int max = 200)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (t.Length > max) throw new DomainException($"{label} بیش از حد طولانی است.");
        return t;
    }

    public static string? OptionalEmail(string? value)
    {
        var t = Optional(value, "ایمیل", 150);
        if (t is null) return null;
        if (!Regex.IsMatch(t, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) throw new DomainException("ایمیل معتبر نیست.");
        return t;
    }

    public static string Password(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8) throw new DomainException("رمز عبور باید حداقل ۸ کاراکتر باشد.");
        if (value.Length > 100) throw new DomainException("رمز عبور بیش از حد طولانی است.");
        return value;
    }

    public static string? PostalCode(string? value)
    {
        var t = Optional(value == null ? null : NormalizeDigits(value), "کد پستی", 10);
        if (t is null) return null;
        if (!Regex.IsMatch(t, @"^\d{10}$")) throw new DomainException("کد پستی باید ۱۰ رقم باشد.");
        return t;
    }
}
