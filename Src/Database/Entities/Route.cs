using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace Database.Entities;

public class Route
{
    public int RouteId { get; set; }
    public required string Name { get; set; }
    public required MultiLineString Geometry { get; set; }
    public DateTime CreatedAt { get; set; }
    //public required List<RouteSegment> Segments { get; set; }
}