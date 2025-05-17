-- Создание таблицы для маршрутизации (каждая запись - ребро - участок дороги между 2 узлами)
CREATE TABLE IF NOT EXISTS routing_roads AS
SELECT osm_id, name, highway, way AS geom
FROM planet_osm_line
WHERE highway IN ('motorway','trunk','primary','secondary',
                'tertiary','unclassified','residential',
                'motorway_link','trunk_link','primary_link',
                'secondary_link','tertiary_link');

-- Добавление необходимых столбцов
ALTER TABLE routing_roads ADD COLUMN IF NOT EXISTS id SERIAL PRIMARY KEY;
ALTER TABLE routing_roads ADD COLUMN IF NOT EXISTS source INTEGER;
ALTER TABLE routing_roads ADD COLUMN IF NOT EXISTS target INTEGER;
ALTER TABLE routing_roads ADD COLUMN IF NOT EXISTS cost DOUBLE PRECISION;

-- Обновление данных 4326 - id системы координат, использующая широту и долготу
UPDATE routing_roads SET
  cost = ST_Length(ST_Transform(geom, 4326)::geography) / 
         CASE 
           WHEN highway IN ('motorway','trunk') THEN 130.0 -- 130 км/ч
           WHEN highway IN ('primary') THEN 90.0
           WHEN highway IN ('secondary') THEN 70.0
           ELSE 50.0 -- городские дороги
         END;

-- Создание топологии (заполняет таблицу routing_roads и создаёт ещё одну таблицу routing_roads_vertices_pgr с вершинами графа (source и target как раз ссылки на них))
-- SELECT pgr_createTopology('routing_roads', 0.00001, 'geom', 'id');
CREATE TABLE vertices_table AS
SELECT * FROM pgr_extractVertices(
  'SELECT id, geom FROM routing_roads ORDER BY id'
);

-- Обновляем поле source
UPDATE routing_roads AS r
SET source = v.id
FROM vertices_table AS v
WHERE ST_Equals(ST_StartPoint(r.geom), v.the_geom);

-- Обновляем поле target
UPDATE routing_roads AS r
SET target = v.id
FROM vertices_table AS v
WHERE ST_Equals(ST_EndPoint(r.geom), v.the_geom);

-- Создание индексов
CREATE INDEX IF NOT EXISTS routing_roads_geom_idx ON routing_roads USING GIST(geom);
CREATE INDEX IF NOT EXISTS routing_roads_source_idx ON routing_roads(source);
CREATE INDEX IF NOT EXISTS routing_roads_target_idx ON routing_roads(target);

-- Создание таблицы маршрутов


CREATE TABLE IF NOT EXISTS routes (
    route_id SERIAL PRIMARY KEY,
    route_name VARCHAR(255) NOT NULL,
    geom GEOMETRY(LINESTRING, 4326), -- Геометрия маршрута
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX routes_geom_idx ON routes USING GIST (geom);
CREATE INDEX routes_route_name_idx ON routes(route_name);

CREATE TABLE route_segments (
    route_id INT REFERENCES routes(route_id) ON DELETE CASCADE,
    edge_id BIGINT NOT NULL, -- ID из таблицы routing_roads
    seq_order INT NOT NULL, -- Порядок следования сегментов
    PRIMARY KEY (route_id, edge_id, seq_order)
);

ALTER TABLE routing_roads_vertices_pgr 
ALTER COLUMN the_geom TYPE Geometry(Point, 4326)
USING ST_Transform(the_geom, 4326);
