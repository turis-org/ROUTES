using DotNetEnv;
using System.Text.Json;
using Database;
using NetTopologySuite.Geometries;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Npgsql;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Database.Tests;

public class RoutingServiceTests
{
    private readonly RoutingService _routingService;

    public RoutingServiceTests()
    {
        if (!File.Exists("/home/grigoriy/Projects/ROUTES/.env")) throw new FileNotFoundException("Create .env file first!");
        Env.Load();

        var connStr = new NpgsqlConnectionStringBuilder
        {
            Host = Env.GetString("DB_HOST", "localhost"),
            Port = Env.GetInt("PG_PORT", 5432),
            Database = Env.GetString("PG_DATABASE", "turis"),
            Username = Env.GetString("PG_USER", "admin"),
            Password = Env.GetString("PG_PASSWORD", "ab4dsF5hpli1"),
            SslMode = Env.GetBool("DB_USE_SSL", false) ? SslMode.Require : SslMode.Disable
        }.ToString();


        _routingService = new(connStr);
    }

    [Fact]
    public void CreateRouteFromPoints_2CorrectPoints_CorrectRouteReturned()
    {   
        // Arrange
        List<Coordinate> points = TestData.GetTwoPoints;
        var name = "My Road";

        // Act
        var response = _routingService.CreateRouteFromPoints(name, points).GetAwaiter().GetResult();

        // Assert
        Assert.NotNull(response);
        Assert.Equal(name, response.Name);
        Assert.NotNull(response.Geometry.Coordinates);

        // В результате в X находится долгота, а в Y широта (на вход подаётся наоборот !!!)
        Assert.InRange(response.Geometry.Coordinates.First().X, points.First().Y - 0.01, points.First().Y + 0.01);
        Assert.InRange(response.Geometry.Coordinates.First().Y, points.First().X - 0.01, points.First().X + 0.01);
        Assert.InRange(response.Geometry.Coordinates.Last().X, points.Last().Y - 0.01, points.Last().Y + 0.01);
        Assert.InRange(response.Geometry.Coordinates.Last().Y, points.Last().X - 0.01, points.Last().X + 0.01);
        //Assert.InRange(points.First().Distance(response.Geometry.Coordinates.First()), 0, 5);
        //Assert.InRange(points.Last().Distance(response.Geometry.Coordinates.Last()), 0, 5);
    }

    /*[Fact]
    public void CreateRouteFromPoints_3CorrectPoints_CorrectRouteReturned()
    {   
        // Arrange
        List<Coordinate> points = TestData.GetThreePoints;
        var name = "My Road";

        // Act
        var response = _routingService.CreateRouteFromPoints(name, points).GetAwaiter().GetResult();

        // Assert
        Assert.NotNull(response);
        Assert.Equal(name, response.Name);
        Assert.NotNull(response.Geometry.Coordinates);

        Assert.InRange(response.Geometry.Coordinates.First().X, points.First().Y - 0.01, points.First().Y + 0.01);
        Assert.InRange(response.Geometry.Coordinates.First().Y, points.First().X - 0.01, points.First().X + 0.01);
        Assert.InRange(response.Geometry.Coordinates.Last().X, points.Last().Y - 0.01, points.Last().Y + 0.01);
        Assert.InRange(response.Geometry.Coordinates.Last().Y, points.Last().X - 0.01, points.Last().X + 0.01);

        _routingService.DeleteRoute(response.RouteId).GetAwaiter();
    }*/

    [Fact]
    public void CreateRouteFromPoints_2IncorrectPoints_NullReturned()
    {   
        // Arrange
        List<Coordinate> points = TestData.GetInvalidPoints;
        var name = "My Road";

        // Act
        var response = _routingService.CreateRouteFromPoints(name, points).GetAwaiter().GetResult();

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public void CreateRouteFromPoints_1CorrectPoint_NullReturned()
    {   
        // Arrange
        List<Coordinate> points = TestData.GetOnePoint;
        var name = "My Road";

        // Act
        var response = _routingService.CreateRouteFromPoints(name, points).GetAwaiter().GetResult();

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public void CreateRouteFromPoints_0CorrectPoint_NullReturned()
    {   
        // Arrange
        List<Coordinate> points = TestData.GetEmptyPoints;
        var name = "My Road";

        // Act
        var response = _routingService.CreateRouteFromPoints(name, points).GetAwaiter().GetResult();

        // Assert
        Assert.Null(response);
    }
}
