using Database.Entities;
using NetTopologySuite.Geometries;

namespace Database.Tests;

public static class TestData
{
    public static List<Coordinate> GetTwoPoints => new()
    {
        new Coordinate(55.759471, 37.616917),
        new Coordinate(55.743097, 37.614078)
    };

    public static List<Coordinate> GetThreePoints => new()
    {
        new Coordinate(55.759471, 37.616917),
        new Coordinate(55.743097, 37.614078),
        new Coordinate(55.730466, 37.604430)
    };

    public static List<Coordinate> GetMultiplePoints => new()
    {
        new Coordinate(55.759471, 37.616917),
        new Coordinate(55.743097, 37.614078),
        new Coordinate(55.730466, 37.604430),
        new Coordinate(55.719607, 37.556047),
        new Coordinate(55.756103, 37.574292),
        new Coordinate(55.765144, 37.591503),
        new Coordinate(55.764212, 37.602364),
        new Coordinate(55.761350, 37.609056)
    };

    public static List<Coordinate> GetInvalidPoints => new()
    {
        new Coordinate(37.602364, 55.764212),
        new Coordinate(37.609056, 55.761350)
    };

    public static List<Coordinate> GetOnePoint => new()
    {
        new Coordinate(55.759471, 37.616917)
    };

    public static List<Coordinate> GetEmptyPoints => new();
}