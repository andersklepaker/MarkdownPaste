using System.Text;
using System.Text.RegularExpressions;
using MarkdownPaste.Core;

var html = MarkdownClipboard.ToHtmlClipboard("# Æøå\n\n**fet** og *kursiv*\n\n- én\n- to\n\n[Teams](https://example.com)\n\n![bilde](https://example.com/image.png)\n\n<script>alert(1)</script>");
var bytes = Encoding.UTF8.GetBytes(html);
int Offset(string name) => int.Parse(Regex.Match(html, $"{name}:(\\d+)").Groups[1].Value);
var startHtml = Offset("StartHTML");
var endHtml = Offset("EndHTML");
var startFragment = Offset("StartFragment");
var endFragment = Offset("EndFragment");
var fragment = Encoding.UTF8.GetString(bytes[startFragment..endFragment]);

void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
}

Check(Encoding.UTF8.GetString(bytes[startHtml..]).StartsWith("<html>"), "StartHTML must be a UTF-8 byte offset");
Check(endHtml == bytes.Length, "EndHTML must be a UTF-8 byte offset");
Check(fragment.Contains("<h1>Æøå</h1>") && fragment.Contains("<strong>fet</strong>") && fragment.Contains("<em>kursiv</em>"), "Markdown formatting");
Check(fragment.Contains("<ul>") && fragment.Contains("href=\"https://example.com\""), "Lists and links");
Check(!fragment.Contains("<img") && !fragment.Contains("<script>"), "Images and raw HTML must not be rendered");
Check(!MarkdownClipboard.ToHtmlClipboard("[bad](javascript:alert)").Contains("href=\"javascript:"), "Unsafe links must not be rendered");
Console.WriteLine("All Markdown clipboard checks passed.");
