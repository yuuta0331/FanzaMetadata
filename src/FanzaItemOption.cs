using Playnite.SDK;

namespace FanzaMetadata;

public class FanzaItemOption(string name, string value, string link) : GenericItemOption(name, value)
{
    public string Link { get; set; } = link;
}