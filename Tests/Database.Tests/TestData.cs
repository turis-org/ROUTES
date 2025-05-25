using Database.Entities;
using NetTopologySuite.Geometries;

namespace Database.Tests;

public static class TestData
{
    public static List<Coordinate> GetTwoPoints => new()
    {
        new Coordinate(54.997792, 82.916929),
        new Coordinate(54.980895, 83.035171)
    };

    public static List<Coordinate> GetThreePoints => new()
    {
        new Coordinate(54.997792, 82.916929),
        new Coordinate(54.994992, 82.965884),
        new Coordinate(54.980895, 83.035171)
    };

    public static List<Coordinate> GetInvalidPoints => new()
    {
        new Coordinate(83.035171, 54.980895),
        new Coordinate(82.965884, 54.994992)
    };

    public static List<Coordinate> GetOnePoint => new()
    {
        new Coordinate(54.997792, 82.916929),
    };

    public static List<Coordinate> GetEmptyPoints => new();
}