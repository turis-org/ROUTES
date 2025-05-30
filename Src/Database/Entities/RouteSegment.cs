namespace Database.Entities;

public class RouteSegment
{
    public int RouteId { get; set; }
    public long EdgeId { get; set; }
    public int Sequence { get; set; }
    public long NodeId { get; set; }
}