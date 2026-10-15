using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AVAUIDesigner.Engine.Properties;

/// <summary>
/// Converts between XAML attribute strings and typed Avalonia values.
/// </summary>
public static class XamlValueConverter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>True for markup extensions like "{Binding Foo}" (but not the "{}" escape).</summary>
    public static bool IsMarkupExtension(string? text) =>
        text is { Length: > 1 } && text[0] == '{' && text[1] != '}';

    public static string Serialize(object? value, Type targetType)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case string s:
                return s;
            case bool b:
                return b ? "True" : "False";
            case double d:
                return FormatDouble(d);
            case float f:
                return FormatDouble(f);
            case decimal m:
                return m.ToString(Inv);
            case int or long or short or byte:
                return Convert.ToString(value, Inv)!;
            case Color c:
                return FormatColor(c);
            case ISolidColorBrush sb:
                return FormatColor(sb.Color);
            case Thickness t:
                if (t.Left == t.Top && t.Top == t.Right && t.Right == t.Bottom) return FormatDouble(t.Left);
                if (t.Left == t.Right && t.Top == t.Bottom) return $"{FormatDouble(t.Left)},{FormatDouble(t.Top)}";
                return $"{FormatDouble(t.Left)},{FormatDouble(t.Top)},{FormatDouble(t.Right)},{FormatDouble(t.Bottom)}";
            case CornerRadius cr:
                if (cr.TopLeft == cr.TopRight && cr.TopRight == cr.BottomRight && cr.BottomRight == cr.BottomLeft)
                    return FormatDouble(cr.TopLeft);
                return $"{FormatDouble(cr.TopLeft)},{FormatDouble(cr.TopRight)},{FormatDouble(cr.BottomRight)},{FormatDouble(cr.BottomLeft)}";
            case GridLength gl:
                if (gl.IsAuto) return "Auto";
                if (gl.IsStar) return gl.Value == 1 ? "*" : $"{FormatDouble(gl.Value)}*";
                return FormatDouble(gl.Value);
            case FontFamily ff:
                return ff.ToString();
            case FontWeight fw:
                return Enum.IsDefined(fw) ? fw.ToString() : ((int)fw).ToString(Inv);
            case Point p:
                return $"{FormatDouble(p.X)},{FormatDouble(p.Y)}";
            case Size sz:
                return $"{FormatDouble(sz.Width)},{FormatDouble(sz.Height)}";
            case Rect r:
                return $"{FormatDouble(r.X)},{FormatDouble(r.Y)},{FormatDouble(r.Width)},{FormatDouble(r.Height)}";
            case Enum e:
                return e.ToString();
            case DateTime dt:
                return dt.ToString("yyyy-MM-dd", Inv);
            case DateTimeOffset dto:
                return dto.ToString("yyyy-MM-dd", Inv);
            case TimeSpan ts:
                return ts.ToString(@"hh\:mm\:ss", Inv);
        }
        return Convert.ToString(value, Inv) ?? string.Empty;
    }

    /// <summary>Converts XAML text to a typed value. Throws <see cref="FormatException"/> on failure.</summary>
    public static object? Deserialize(string text, Type targetType)
    {
        if (TryDeserialize(text, targetType, out var value)) return value;
        throw new FormatException($"'{text}' is not a valid {targetType.Name} value.");
    }

    public static bool TryDeserialize(string? text, Type targetType, out object? value)
    {
        value = null;
        if (text is null) return false;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var t = text.Trim();
        try
        {
            if (type == typeof(string) || type == typeof(object))
            {
                value = text;
                return true;
            }
            if (IsMarkupExtension(t)) return false;
            if (type == typeof(bool))
            {
                if (bool.TryParse(t, out var b)) { value = b; return true; }
                return false;
            }
            if (type == typeof(double))
            {
                if (TryParseDouble(t, out var d)) { value = d; return true; }
                return false;
            }
            if (type == typeof(float))
            {
                if (TryParseDouble(t, out var d)) { value = (float)d; return true; }
                return false;
            }
            if (type == typeof(decimal))
            {
                if (decimal.TryParse(t, NumberStyles.Float, Inv, out var m)) { value = m; return true; }
                return false;
            }
            if (type == typeof(int))
            {
                if (int.TryParse(t, NumberStyles.Integer, Inv, out var i)) { value = i; return true; }
                return false;
            }
            if (type == typeof(long))
            {
                if (long.TryParse(t, NumberStyles.Integer, Inv, out var l)) { value = l; return true; }
                return false;
            }
            if (type == typeof(Color))
            {
                if (Color.TryParse(t, out var c)) { value = c; return true; }
                return false;
            }
            if (typeof(IBrush).IsAssignableFrom(type) || type == typeof(IBrush))
            {
                if (Color.TryParse(t, out var c)) { value = new SolidColorBrush(c); return true; }
                return false;
            }
            if (type == typeof(Thickness)) { value = Thickness.Parse(t); return true; }
            if (type == typeof(CornerRadius)) { value = CornerRadius.Parse(t); return true; }
            if (type == typeof(GridLength)) { value = GridLength.Parse(t); return true; }
            if (type == typeof(FontFamily)) { value = FontFamily.Parse(t); return true; }
            if (type == typeof(FontWeight))
            {
                if (Enum.TryParse<FontWeight>(t, true, out var fw)) { value = fw; return true; }
                if (int.TryParse(t, out var n)) { value = (FontWeight)n; return true; }
                return false;
            }
            if (type == typeof(Point)) { value = Point.Parse(t); return true; }
            if (type == typeof(Size)) { value = Size.Parse(t); return true; }
            if (type == typeof(Rect)) { value = Rect.Parse(t); return true; }
            if (type.IsEnum)
            {
                if (Enum.TryParse(type, t, true, out var e)) { value = e; return true; }
                return false;
            }
            if (type == typeof(DateTime))
            {
                if (DateTime.TryParse(t, Inv, DateTimeStyles.None, out var dt)) { value = dt; return true; }
                return false;
            }
            if (type == typeof(DateTimeOffset))
            {
                if (DateTimeOffset.TryParse(t, Inv, DateTimeStyles.None, out var dto)) { value = dto; return true; }
                return false;
            }
            if (type == typeof(TimeSpan))
            {
                if (TimeSpan.TryParse(t, Inv, out var ts)) { value = ts; return true; }
                return false;
            }

            // Fallbacks: a static Parse(string) method, then a TypeConverter.
            var parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
            if (parse is not null && type.IsAssignableFrom(parse.ReturnType))
            {
                value = parse.Invoke(null, [t]);
                return true;
            }
            var converter = TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(typeof(string)))
            {
                value = converter.ConvertFrom(null, Inv, t);
                return value is not null;
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or TargetInvocationException
                                       or OverflowException or NotSupportedException or InvalidOperationException)
        {
            value = null;
        }
        return false;
    }

    /// <summary>True when <paramref name="text"/> is a valid value of <paramref name="type"/> (markup extensions always are).</summary>
    public static bool IsValid(string text, Type type) =>
        IsMarkupExtension(text.Trim()) || TryDeserialize(text, type, out _);

    /// <summary>Compares two XAML strings semantically for the given type (so "8" equals "8,8,8,8").</summary>
    public static bool AreEquivalent(string? a, string? b, Type? type)
    {
        if (a == b) return true;
        if (a is null || b is null || type is null) return false;
        if (TryDeserialize(a, type, out var va) && TryDeserialize(b, type, out var vb))
        {
            if (va is ISolidColorBrush ba && vb is ISolidColorBrush bb) return ba.Color == bb.Color;
            return Equals(va, vb) || (va is double da && vb is double db && double.IsNaN(da) && double.IsNaN(db));
        }
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatDouble(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsPositiveInfinity(d)) return "Infinity";
        if (double.IsNegativeInfinity(d)) return "-Infinity";
        return Math.Round(d, 4).ToString("0.####", Inv);
    }

    public static bool TryParseDouble(string text, out double d)
    {
        switch (text)
        {
            case "NaN": d = double.NaN; return true;
            case "Infinity": d = double.PositiveInfinity; return true;
            case "-Infinity": d = double.NegativeInfinity; return true;
            case "Auto": d = double.NaN; return true;
        }
        return double.TryParse(text, NumberStyles.Float, Inv, out d);
    }

    private static string FormatColor(Color c)
    {
        var known = KnownColorName(c);
        if (known is not null) return known;
        return c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private static readonly Lazy<Dictionary<uint, string>> KnownColors = new(() =>
    {
        var map = new Dictionary<uint, string>();
        foreach (var prop in typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (prop.GetValue(null) is Color color) map.TryAdd(color.ToUInt32(), prop.Name);
        }
        return map;
    });

    /// <summary>All named colors (Red, Blue, ...), for editors.</summary>
    public static IReadOnlyCollection<string> KnownColorNames => KnownColors.Value.Values;

    private static string? KnownColorName(Color c) => KnownColors.Value.GetValueOrDefault(c.ToUInt32());
}
