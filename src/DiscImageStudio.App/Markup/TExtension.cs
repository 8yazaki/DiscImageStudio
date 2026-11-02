using System.Windows.Markup;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Markup;

/// <summary>XAML access to <see cref="L.T"/>: <c>Text="{loc:T '设置'}"</c>. The Chinese source text
/// is the key, so a window loaded before the language is initialized still shows readable text.
/// The language is fixed at startup, so the value is resolved once when the XAML loads.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
}
