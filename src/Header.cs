using Spectre.Console;
using Spectre.Console.Rendering;

namespace src;

public enum SortMode
{
    MemoryDesc,
    NameAsc,
    PidAsc,
    None
}

public sealed class Header(string text, SortMode type) : IRenderable
{
    private readonly string _text = text;
    private readonly Style _style = GetStyleForType(type);

    private static Style GetStyleForType(SortMode type) => type switch
    {
        SortMode.MemoryDesc => new Style(Color.White, Color.Green),
        SortMode.NameAsc => new Style(Color.White, Color.Blue),
        SortMode.PidAsc => new Style(Color.White, Color.Yellow),
        SortMode.None => new Style(Color.White, Color.Grey),
        _ => new Style(Color.White, Color.Grey),
    };

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        var width = _text.Length + 4;
        return new Measurement(width, width);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        if (options.Capabilities.Unicode)
        {
            yield return new Segment($" {_text} ", _style);
        }
        else
        {
            yield return new Segment($"  {_text}  ", _style);
        }
    }
}
