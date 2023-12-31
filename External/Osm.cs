using System;

namespace cycloid.External;

public class Osm
{
    public Uri TilesUri { get; } = new("https://tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png");
}