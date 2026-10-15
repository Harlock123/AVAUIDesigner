using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AVAUIDesigner.Engine.Properties;

namespace AVAUIDesigner.Engine.Tests;

/// <summary>Every row of the spec §7 conversion table, in both directions.</summary>
public class XamlValueConverterTests
{
    private static T De<T>(string text) => (T)XamlValueConverter.Deserialize(text, typeof(T))!;
    private static string Ser<T>(T value) => XamlValueConverter.Serialize(value, typeof(T));

    // ---- Color ----

    [Fact]
    public void Color_FromHexAndName()
    {
        Assert.Equal(Color.FromRgb(255, 0, 0), De<Color>("#FF0000"));
        Assert.Equal(Colors.Red, De<Color>("Red"));
        Assert.Equal(Color.FromArgb(0x80, 0xFF, 0, 0), De<Color>("#80FF0000"));
    }

    [Fact]
    public void Color_ToString_PrefersKnownNameThenHex()
    {
        Assert.Equal("Red", Ser(Colors.Red));
        Assert.Equal("#123456", Ser(Color.FromRgb(0x12, 0x34, 0x56)));
        Assert.Equal("#80123456", Ser(Color.FromArgb(0x80, 0x12, 0x34, 0x56)));
    }

    [Fact]
    public void Color_Invalid_Fails()
    {
        Assert.False(XamlValueConverter.TryDeserialize("notacolor", typeof(Color), out _));
        Assert.Throws<FormatException>(() => XamlValueConverter.Deserialize("notacolor", typeof(Color)));
    }

    // ---- SolidColorBrush ----

    [Theory]
    [InlineData("Red", 0xFF, 0xFF, 0x00, 0x00)]
    [InlineData("#80FF0000", 0x80, 0xFF, 0x00, 0x00)]
    public void Brush_FromString(string text, byte a, byte r, byte g, byte b)
    {
        foreach (var type in new[] { typeof(IBrush), typeof(SolidColorBrush) })
        {
            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(XamlValueConverter.Deserialize(text, type));
            Assert.Equal(Color.FromArgb(a, r, g, b), brush.Color);
        }
    }

    [Fact]
    public void Brush_ToString()
    {
        Assert.Equal("Red", XamlValueConverter.Serialize(new SolidColorBrush(Colors.Red), typeof(IBrush)));
        Assert.Equal("#80FF0000", XamlValueConverter.Serialize(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0, 0)), typeof(IBrush)));
    }

    // ---- Thickness ----

    [Theory]
    [InlineData("8", 8, 8, 8, 8, "8")]
    [InlineData("8,4", 8, 4, 8, 4, "8,4")]
    [InlineData("8,4,8,4", 8, 4, 8, 4, "8,4")]
    [InlineData("1,2,3,4", 1, 2, 3, 4, "1,2,3,4")]
    [InlineData("0.5", 0.5, 0.5, 0.5, 0.5, "0.5")]
    public void Thickness_BothDirections(string text, double l, double t, double r, double b, string canonical)
    {
        var value = De<Thickness>(text);
        Assert.Equal(new Thickness(l, t, r, b), value);
        Assert.Equal(canonical, Ser(value));
        Assert.True(XamlValueConverter.AreEquivalent(text, canonical, typeof(Thickness)));
    }

    // ---- CornerRadius ----

    [Fact]
    public void CornerRadius_BothDirections()
    {
        Assert.Equal(new CornerRadius(4), De<CornerRadius>("4"));
        Assert.Equal("4", Ser(new CornerRadius(4)));
        var cr = De<CornerRadius>("4,8,4,8");
        Assert.Equal(new CornerRadius(4, 8, 4, 8), cr);
        Assert.Equal(4, cr.TopLeft);
        Assert.Equal(8, cr.TopRight);
        Assert.Equal(4, cr.BottomRight);
        Assert.Equal(8, cr.BottomLeft);
        Assert.Equal("4,8,4,8", Ser(cr));
        Assert.Equal("1,2,3,4", Ser(new CornerRadius(1, 2, 3, 4)));
    }

    // ---- GridLength ----

    [Theory]
    [InlineData("*", 1, GridUnitType.Star, "*")]
    [InlineData("2*", 2, GridUnitType.Star, "2*")]
    [InlineData("0.5*", 0.5, GridUnitType.Star, "0.5*")]
    [InlineData("Auto", 1, GridUnitType.Auto, "Auto")]
    [InlineData("100", 100, GridUnitType.Pixel, "100")]
    public void GridLength_BothDirections(string text, double value, GridUnitType unit, string canonical)
    {
        var gl = De<GridLength>(text);
        Assert.Equal(unit, gl.GridUnitType);
        if (unit != GridUnitType.Auto) Assert.Equal(value, gl.Value);
        Assert.Equal(canonical, Ser(gl));
    }

    [Fact]
    public void GridLength_Invalid_Fails() =>
        Assert.False(XamlValueConverter.TryDeserialize("abc", typeof(GridLength), out _));

    // ---- FontFamily ----

    [Fact]
    public void FontFamily_Name()
    {
        var ff = De<FontFamily>("Arial");
        Assert.Equal("Arial", ff.Name);
        Assert.Equal("Arial", Ser(ff));
    }

    [Fact]
    public void FontFamily_ResourceUri()
    {
        const string uri = "avares://MyApp/Assets/Fonts#Inter";
        var ff = De<FontFamily>(uri);
        Assert.Equal("Inter", ff.Name);
        // Note: Avalonia normalizes the URI authority to lower case ("avares://myapp/...").
        Assert.Equal(uri, Ser(ff), ignoreCase: true);
    }

    // ---- Alignments / Orientation / enums ----

    [Theory]
    [InlineData("Left", HorizontalAlignment.Left)]
    [InlineData("Center", HorizontalAlignment.Center)]
    [InlineData("Right", HorizontalAlignment.Right)]
    [InlineData("Stretch", HorizontalAlignment.Stretch)]
    public void HorizontalAlignment_BothDirections(string text, HorizontalAlignment value)
    {
        Assert.Equal(value, De<HorizontalAlignment>(text));
        Assert.Equal(text, Ser(value));
    }

    [Theory]
    [InlineData("Top", VerticalAlignment.Top)]
    [InlineData("Center", VerticalAlignment.Center)]
    [InlineData("Bottom", VerticalAlignment.Bottom)]
    [InlineData("Stretch", VerticalAlignment.Stretch)]
    public void VerticalAlignment_BothDirections(string text, VerticalAlignment value)
    {
        Assert.Equal(value, De<VerticalAlignment>(text));
        Assert.Equal(text, Ser(value));
    }

    [Theory]
    [InlineData("Horizontal", Orientation.Horizontal)]
    [InlineData("Vertical", Orientation.Vertical)]
    public void Orientation_BothDirections(string text, Orientation value)
    {
        Assert.Equal(value, De<Orientation>(text));
        Assert.Equal(text, Ser(value));
    }

    [Fact]
    public void Enums_UseMemberName_CaseInsensitiveOnInput()
    {
        Assert.Equal(Dock.Bottom, De<Dock>("Bottom"));
        Assert.Equal(Dock.Bottom, De<Dock>("bottom"));
        Assert.Equal("Bottom", Ser(Dock.Bottom));
        Assert.Equal(TextWrapping.Wrap, De<TextWrapping>("Wrap"));
        Assert.False(XamlValueConverter.TryDeserialize("Sideways", typeof(Dock), out _));
    }

    [Fact]
    public void FontWeight_NamedAndNumeric()
    {
        Assert.Equal(FontWeight.Bold, De<FontWeight>("Bold"));
        Assert.Equal("Bold", Ser(FontWeight.Bold));
        Assert.Equal((FontWeight)650, De<FontWeight>("650"));
        Assert.Equal("650", Ser((FontWeight)650));
    }

    // ---- Primitives ----

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("true", true)]
    public void Bool_BothDirections(string text, bool value)
    {
        Assert.Equal(value, De<bool>(text));
        Assert.Equal(value ? "True" : "False", Ser(value));
    }

    [Fact]
    public void Bool_Invalid_Fails() => Assert.False(XamlValueConverter.TryDeserialize("yes", typeof(bool), out _));

    [Fact]
    public void NullableBool_UsesUnderlyingType() =>
        Assert.Equal(true, XamlValueConverter.Deserialize("True", typeof(bool?)));

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("-3", -3)]
    [InlineData("100", 100)]
    [InlineData("1e3", 1000)]
    public void Double_Numbers(string text, double value)
    {
        Assert.Equal(value, De<double>(text));
    }

    [Fact]
    public void Double_SpecialValues()
    {
        Assert.True(double.IsNaN(De<double>("NaN")));
        Assert.Equal(double.PositiveInfinity, De<double>("Infinity"));
        Assert.Equal(double.NegativeInfinity, De<double>("-Infinity"));
        Assert.True(double.IsNaN(De<double>("Auto")));
        Assert.Equal("NaN", Ser(double.NaN));
        Assert.Equal("Infinity", Ser(double.PositiveInfinity));
        Assert.Equal("-Infinity", Ser(double.NegativeInfinity));
    }

    [Fact]
    public void Double_Formatting_IsInvariantAndCompact()
    {
        Assert.Equal("1.5", Ser(1.5));
        Assert.Equal("100", Ser(100.0));
        Assert.Equal("0.3333", Ser(1.0 / 3));
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.5", Ser(1.5));
            Assert.Equal(1.5, De<double>("1.5"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Int_BothDirections()
    {
        Assert.Equal(42, De<int>("42"));
        Assert.Equal("42", Ser(42));
        Assert.False(XamlValueConverter.TryDeserialize("4.2", typeof(int), out _));
    }

    [Fact]
    public void String_IsPassthrough()
    {
        Assert.Equal("  hello, world  ", De<string>("  hello, world  "));
        Assert.Equal("{Binding X}", De<string>("{Binding X}"));
        Assert.Equal("abc", Ser("abc"));
        Assert.Equal(string.Empty, XamlValueConverter.Serialize(null, typeof(string)));
    }

    // ---- Point / Size / Rect ----

    [Fact]
    public void Point_BothDirections()
    {
        Assert.Equal(new Point(10, 20), De<Point>("10,20"));
        Assert.Equal("10,20", Ser(new Point(10, 20)));
    }

    [Fact]
    public void Size_BothDirections()
    {
        Assert.Equal(new Size(100, 50), De<Size>("100,50"));
        Assert.Equal("100,50", Ser(new Size(100, 50)));
    }

    [Fact]
    public void Rect_BothDirections()
    {
        Assert.Equal(new Rect(0, 0, 100, 50), De<Rect>("0,0,100,50"));
        Assert.Equal("0,0,100,50", Ser(new Rect(0, 0, 100, 50)));
    }

    [Theory]
    [InlineData(typeof(Point), "abc")]
    [InlineData(typeof(Size), "1")]
    [InlineData(typeof(Thickness), "abc")]
    [InlineData(typeof(double), "abc")]
    public void InvalidStructText_Fails(Type type, string text)
    {
        Assert.False(XamlValueConverter.TryDeserialize(text, type, out var value));
        Assert.Null(value);
        Assert.False(XamlValueConverter.IsValid(text, type));
    }

    // ---- Markup extensions ----

    [Theory]
    [InlineData("{Binding Foo}", true)]
    [InlineData("{StaticResource Key}", true)]
    [InlineData("{x:Static local:A.B}", true)]
    [InlineData("{}{literal braces}", false)]
    [InlineData("{", false)]
    [InlineData("Red", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMarkupExtension(string? text, bool expected) =>
        Assert.Equal(expected, XamlValueConverter.IsMarkupExtension(text));

    [Fact]
    public void MarkupExtensions_AreNotConvertedButAreValid()
    {
        Assert.False(XamlValueConverter.TryDeserialize("{Binding Width}", typeof(double), out _));
        Assert.True(XamlValueConverter.IsValid("{Binding Width}", typeof(double)));
        Assert.True(XamlValueConverter.IsValid(" {Binding Width}", typeof(Thickness)));
    }

    // ---- AreEquivalent ----

    [Theory]
    [InlineData("8", "8,8,8,8", typeof(Thickness), true)]
    [InlineData("8,4", "8,4,8,4", typeof(Thickness), true)]
    [InlineData("8", "8,4", typeof(Thickness), false)]
    [InlineData("Red", "#FF0000", typeof(Color), true)]
    [InlineData("Red", "#FFFF0000", typeof(IBrush), true)]
    [InlineData("Red", "Blue", typeof(IBrush), false)]
    [InlineData("True", "true", typeof(bool), true)]
    [InlineData("NaN", "NaN", typeof(double), true)]
    [InlineData("Auto", "NaN", typeof(double), true)]
    [InlineData("1", "1.0", typeof(double), true)]
    [InlineData("1", "2", typeof(double), false)]
    [InlineData("Stretch", "stretch", typeof(HorizontalAlignment), true)]
    [InlineData("*", "1*", typeof(GridLength), true)]
    [InlineData("abc", "ABC", typeof(string), false)]
    [InlineData("hello", "hello", null, true)]
    [InlineData("hello", "Hello", null, false)]
    [InlineData(null, "x", typeof(string), false)]
    [InlineData(null, null, typeof(string), true)]
    public void AreEquivalent(string? a, string? b, Type? type, bool expected) =>
        Assert.Equal(expected, XamlValueConverter.AreEquivalent(a, b, type));
}
