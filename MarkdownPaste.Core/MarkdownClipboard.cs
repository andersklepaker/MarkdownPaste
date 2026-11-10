using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkdownPaste.Core;

public static class MarkdownClipboard
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .Build();

    public static string ToHtmlClipboard(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var link in document.Descendants<LinkInline>())
        {
            // Images must not cause the destination application to fetch remote resources.
            if (link.IsImage)
            {
                link.IsImage = false;
                link.Url = null;
            }
            else if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps &&
                      uri.Scheme != Uri.UriSchemeMailto))
            {
                link.Url = null;
            }
        }

        var fragment = Markdown.ToHtml(document, Pipeline);
        const string prefix = "<html><body><!--StartFragment-->";
        const string suffix = "<!--EndFragment--></body></html>";
        const string headerTemplate = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

        var headerLength = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, headerTemplate, 0, 0, 0, 0));
        var startFragment = headerLength + Encoding.UTF8.GetByteCount(prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(suffix);
        var header = string.Format(CultureInfo.InvariantCulture, headerTemplate, headerLength, endHtml, startFragment, endFragment);
        return header + prefix + fragment + suffix;
    }
}
