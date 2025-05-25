using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NetTopologySuite.Geometries;
using Npgsql;
using NpgsqlTypes;
using Database.Entities;
using NetTopologySuite.IO;

namespace Database;

public class RoutingService
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
                  VALUES (@Name, ST_SetSRID(ST_GeomFromEWKB(@Geometry), 4326))
                  RETURNING route_id",
                new { route.Name, Geometry = route.Geometry.AsBinary() },
                transaction);

            /*await conn.ExecuteAsync(
                @"INSERT INTO route_segments (route_id, edge_id, seq_order)
                  VALUES (@RouteId, @EdgeId, @Sequence)",
                route.Segments.Select(s => new {
                    RouteId = routeId,
                    s.EdgeId,
                    s.Sequence
                }),
                transaction);*/

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

        /*if (route != null)
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
        }*/

        return route;
    }

    public async Task<Route> GetRouteByName(String name)
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

        /*if (route != null)
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
        }*/

        return route;
    }

    public async Task<List<Route>> GetAllRoutes()
    {
        var routes = new List<Route>();

        // 1. Получаем основные данные маршрутов
        await using (var cmd = new NpgsqlCommand(
            @"SELECT 
                route_id, 
                route_name, 
                ST_AsBinary(geom) AS geometry,
                created_at
              FROM routes
              ORDER BY created_at DESC", 
            conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var route = new Route
                {
                    RouteId = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Geometry = reader.IsDBNull(2) ? null : new WKBReader().Read(reader.GetFieldValue<byte[]>(2)) as MultiLineString,
                    CreatedAt = reader.GetDateTime(3),
                    //Segments = null
                };
                routes.Add(route);
            }
        }

        // 2. Опционально: получаем сегменты для каждого маршрута
        /*foreach (var route in routes)
        {
            route.Segments = route.Segments = (await conn.QueryAsync<RouteSegment>(
                    @"SELECT 
                        route_id AS RouteId,
                        edge_id AS EdgeId,
                        seq_order AS Sequence
                    FROM route_segments
                    WHERE route_id = @RouteId
                    ORDER BY seq_order",
                    new { route.RouteId })).ToList();
        }*/

        return routes;
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

            /*await conn.ExecuteAsync(
                @"INSERT INTO route_segments (route_id, edge_id, seq_order)
                  VALUES (@RouteId, @EdgeId, @Sequence)",
                route.Segments.Select(s => new {
                    route.RouteId,
                    s.EdgeId,
                    s.Sequence
                }),
                transaction);*/

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
    public async Task<Route?> CreateRouteFromPoints(String name, List<Coordinate> points)
    {
        if (points.Count <= 1) {
            return null;
        }

        // Находим ближайшие вершины
        var vertices = new List<long>();
        foreach (var point in points)
        {   
            var vertexId = await conn.ExecuteScalarAsync<long>(
                @"SELECT id
                FROM routing_roads_vertices_pgr
                WHERE ST_DWithin(
                    the_geom,
                    ST_SetSRID(ST_Point(@Lon, @Lat), 4326),
                    100
                )
                ORDER BY the_geom <-> ST_SetSRID(ST_Point(@Lon, @Lat), 4326)
                LIMIT 1",
                new { Lat = point.X, Lon = point.Y });
            
            if (vertices.Contains(vertexId)) {
                return null;
            }
            vertices.Add(vertexId);
        }

        if (vertices.Count <= 1) {
            return null;
        }

        MultiLineString geometry;
        var subGeoms = new List<LineString> ();
        var verticesArr = vertices.ToArray();

        for (int i = 1; i < verticesArr.Length; ++i)
        {
            var byteGeometry = await conn.QuerySingleAsync<byte[]>(
            @"WITH dijkstra_result AS (
                SELECT edge, path_seq, node
                    FROM pgr_bdDijkstra(
                        'SELECT id, source, target, cost, reverse_cost FROM routing_roads',
                        @Start,
                        @End,
                        directed := false
                    )
                    WHERE edge > 0
            ),
            get_geom AS (
                SELECT path_seq,
                    CASE 
                        WHEN r.source = d.node THEN r.geom 
                        ELSE ST_Reverse(r.geom) 
                    END AS route_geometry
                FROM dijkstra_result d
                JOIN routing_roads r ON d.edge = r.id
                ORDER BY d.path_seq
            )
            SELECT ST_AsBinary(ST_Transform(ST_LineMerge(ST_Union(route_geometry ORDER BY path_seq)), 4326)) FROM get_geom;",
            new { Start = verticesArr[i - 1], End = verticesArr[i] });

            if (byteGeometry == null) {
                continue;
            }

            var subGeom = new WKBReader().Read(byteGeometry);
    
            if (subGeom is LineString lineString)
            {
                subGeoms.Add(lineString);
            }
        }

        geometry = new MultiLineString(subGeoms.ToArray());

        // Создаем маршрут
        var route = new Route
        {
            Name = name,
            Geometry = geometry,
            /*Segments = result
                .GroupBy(r => r.PathId)
                .SelectMany(g => g
                    .Select((r, idx) => new RouteSegment
                    {
                        EdgeId = r.EdgeId,
                        Sequence = idx + 1
                    }))
                .ToList()*/
        };

        route.RouteId = await CreateRoute(route);
        return route;
    }
    
    public async Task<List<Route>> FindNearestRoutesAsync(double longitude, double latitude, 
        int limit = 1, double maxDistanceMeters = 1000)
    {
        // Получаем ближайшие маршруты в пределах maxDistanceMeters
        var routes = (await conn.QueryAsync<Route>(
                @"SELECT 
            route_id AS RouteId,
            route_name AS Name,
            geom AS Geometry,
            created_at AS CreatedAt
          FROM routes
          WHERE ST_Distance(
            geom::geography,
            ST_SetSRID(ST_MakePoint(@Lon, @Lat), 4326)::geography) < @MaxDistance
          ORDER BY ST_Distance(geom::geography, ST_SetSRID(ST_MakePoint(@Lon, @Lat), 4326)::geography) ASC
          LIMIT @Limit",
                new { 
                    Lon = longitude, 
                    Lat = latitude, 
                    Limit = limit,
                    MaxDistance = maxDistanceMeters
                }))
            .ToList();
            
        // Для каждого маршрута загружаем сегменты
        /*foreach (var route in routes)
        {
            route.Segments = (await conn.QueryAsync<RouteSegment>(
                    @"SELECT 
                route_id AS RouteId,
                edge_id AS EdgeId,
                seq_order AS Sequence
              FROM route_segments
              WHERE route_id = @RouteId
              ORDER BY seq_order",
                    new { RouteId = route.RouteId }))
                .ToList();
        }*/

        return routes;
    }
}
