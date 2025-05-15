using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NetTopologySuite.Geometries;
using Npgsql;
using NpgsqlTypes;
using Database.Entities;
using Route = Database.Entities.Route;

namespace Database;

public class RoutingService : IRoutingService
{
    private Npgsql.NpgsqlConnection conn;

    private class PathSegment
    {
        public int PathId { get; set; }
        public int Sequence { get; set; }
        public long EdgeId { get; set; }
        public long NodeId { get; set; }
    }
    
    public RoutingService(string connectionString)
    {
        NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();
        conn = new NpgsqlConnection(connectionString);
        conn.Open();
    }

    ~RoutingService()
    {
        conn.Close();
    }


    // CRUD операции
    public async Task<int> CreateRoute(Route route)
    {
        using var transaction = conn.BeginTransaction();
        
        try
        {
            var routeId = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO routes (route_name, geom)
                  VALUES (@Name, ST_GeomFromEWKB(@Geometry))
                  RETURNING route_id",
                new { route.Name, Geometry = route.Geometry.AsBinary() },
                transaction);

            await conn.ExecuteAsync(
                @"INSERT INTO route_segments (route_id, edge_id, seq_order)
                  VALUES (@RouteId, @EdgeId, @Sequence)",
                route.Segments.Select(s => new {
                    RouteId = routeId,
                    s.EdgeId,
                    s.Sequence
                }),
                transaction);

            transaction.Commit();
            return routeId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<Route> GetRoute(int routeId)
    {        
        var route = await conn.QuerySingleOrDefaultAsync<Route>(
            @"SELECT 
                route_id AS RouteId,
                route_name AS Name,
                geom AS Geometry,
                created_at AS CreatedAt
              FROM routes
              WHERE route_id = @RouteId",
            new { RouteId = routeId });

        if (route != null)
        {
            route.Segments = (await conn.QueryAsync<RouteSegment>(
                @"SELECT 
                    route_id AS RouteId,
                    edge_id AS EdgeId,
                    seq_order AS Sequence
                  FROM route_segments
                  WHERE route_id = @RouteId
                  ORDER BY seq_order",
                new { RouteId = routeId })).ToList();
        }

        return route;
    }

    public async Task<Route> GetRouteByName(string name)
    {
        var route = await conn.QuerySingleOrDefaultAsync<Route>(
            @"SELECT 
                route_id AS RouteId,
                route_name AS Name,
                geom AS Geometry,
                created_at AS CreatedAt
              FROM routes
              WHERE route_name = @RouteName",
            new { RouteName = name });

        if (route != null)
        {
            route.Segments = (await conn.QueryAsync<RouteSegment>(
                @"SELECT 
                    route_id AS RouteId,
                    edge_id AS EdgeId,
                    seq_order AS Sequence
                  FROM route_segments
                  WHERE route_name = @RouteName
                  ORDER BY seq_order",
                new { RouteName = name })).ToList();
        }

        return route;
    }

    public async Task UpdateRoute(Route route)
    {
        using var transaction = conn.BeginTransaction();

        try
        {
            await conn.ExecuteAsync(
                @"UPDATE routes
                  SET route_name = @Name,
                      geom = ST_GeomFromEWKB(@Geometry)
                  WHERE route_id = @RouteId",
                new {
                    route.Name,
                    Geometry = route.Geometry.AsBinary(),
                    route.RouteId
                },
                transaction);

            await conn.ExecuteAsync(
                @"DELETE FROM route_segments
                  WHERE route_id = @RouteId",
                new { route.RouteId },
                transaction);

            await conn.ExecuteAsync(
                @"INSERT INTO route_segments (route_id, edge_id, seq_order)
                  VALUES (@RouteId, @EdgeId, @Sequence)",
                route.Segments.Select(s => new {
                    route.RouteId,
                    s.EdgeId,
                    s.Sequence
                }),
                transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteRoute(int routeId)
    {
        await conn.ExecuteAsync(
            @"DELETE FROM routes
              WHERE route_id = @RouteId",
            new { RouteId = routeId });
    }

    // Сложные операции маршрутизации
    public async Task<Route> CreateRouteFromPoints(String name, List<Coordinate> points)
    {
        // Находим ближайшие вершины
        var vertices = new List<long>();
        foreach (var point in points)
        {
            var vertexId = await conn.ExecuteScalarAsync<long>(
                @"SELECT id
                  FROM routing_roads_vertices_pgr
                  ORDER BY the_geom <-> ST_SetSRID(ST_Point(@Lon, @Lat), 4326)
                  LIMIT 1",
                new { Lat = point.Y, Lon = point.X });
            
            vertices.Add(vertexId);
        }

        // Вычисляем маршрут
        var parameters = new {
            Vertices = vertices,
            PathsCount = vertices.Count - 1
        };

        var result = await conn.QueryAsync<PathSegment>(
            @"WITH dijkstra AS (
                SELECT *
                FROM pgr_dijkstraVia(
                    'SELECT id, source, target, cost FROM routing_roads',
                    @Vertices,
                    directed := false
                )
            )
            SELECT 
                path_id AS PathId,
                path_seq AS Sequence,
                edge AS EdgeId,
                node AS NodeId
            FROM dijkstra
            WHERE edge > 0",
            parameters);

        // Собираем геометрию
        var edges = result.Select(r => r.EdgeId).Distinct().ToList();
        
        var geometry = await conn.QuerySingleAsync<LineString>(
            @"SELECT ST_LineMerge(ST_Collect(geom)) AS geom
              FROM routing_roads
              WHERE id = ANY(@Edges)",
            new { Edges = edges });

        // Создаем маршрут
        var route = new Route
        {
            Name = name,
            Geometry = geometry,
            Segments = result
                .GroupBy(r => r.PathId)
                .SelectMany(g => g
                    .Select((r, idx) => new RouteSegment
                    {
                        EdgeId = r.EdgeId,
                        Sequence = idx + 1
                    }))
                .ToList()
        };

        route.RouteId = await CreateRoute(route);
        return route;
    }
}
