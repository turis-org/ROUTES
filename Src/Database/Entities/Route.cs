using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace Database;

public class Route
{
    public int RouteId { get; set; }
    public string Name { get; set; }
    public LineString Geometry { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RouteSegment> Segments { get; set; }
}