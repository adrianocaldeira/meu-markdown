using Markdig.Helpers;
using Markdig.Parsers;

namespace MeuMarkdown.Extensions.WikiLinks;

public class WikiLinkInlineParser : InlineParser
{
    public WikiLinkInlineParser()
    {
        OpeningCharacters = new[] { '[' };
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        if (slice.PeekChar() != '[') return false;
        if (slice.PeekChar(2) == '[') return false;

        var startPos = slice.Start;
        slice.NextChar();
        slice.NextChar();

        var sb = new System.Text.StringBuilder();
        while (true)
        {
            var c = slice.CurrentChar;
            if (c == '\0' || c == '\n') return ResetAndFail(slice, startPos);
            if (c == ']' && slice.PeekChar() == ']')
            {
                slice.NextChar();
                slice.NextChar();
                break;
            }
            if (c == '[' || c == ']') return ResetAndFail(slice, startPos);
            sb.Append(c);
            slice.NextChar();
        }

        var raw = sb.ToString().Trim();
        if (raw.Length == 0) return ResetAndFail(slice, startPos);

        string? alias = null;
        var pipeIdx = raw.IndexOf('|');
        if (pipeIdx >= 0)
        {
            alias = raw.Substring(pipeIdx + 1).Trim();
            raw = raw.Substring(0, pipeIdx).Trim();
        }

        string? fragment = null;
        var hashIdx = raw.IndexOf('#');
        if (hashIdx >= 0)
        {
            fragment = raw.Substring(hashIdx + 1).Trim();
            raw = raw.Substring(0, hashIdx).Trim();
        }

        if (raw.Length == 0) return ResetAndFail(slice, startPos);

        var displayText = !string.IsNullOrEmpty(alias) ? alias : raw;

        processor.Inline = new WikiLinkInline
        {
            Target = raw,
            Fragment = fragment,
            DisplayText = displayText,
            Span = new global::Markdig.Syntax.SourceSpan(
                processor.GetSourcePosition(startPos),
                processor.GetSourcePosition(slice.Start - 1)),
            Line = processor.LineIndex,
            Column = processor.GetSourcePosition(startPos)
        };
        return true;
    }

    private static bool ResetAndFail(StringSlice slice, int startPos)
    {
        slice.Start = startPos;
        return false;
    }
}
