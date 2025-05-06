-- Создание таблицы для маршрутизации
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

-- Создание топологии
SELECT pgr_createTopology('routing_roads', 0.0001, 'geom', 'id');

-- Создание индексов
CREATE INDEX IF NOT EXISTS routing_roads_geom_idx ON routing_roads USING GIST(geom);
CREATE INDEX IF NOT EXISTS routing_roads_source_idx ON routing_roads(source);
CREATE INDEX IF NOT EXISTS routing_roads_target_idx ON routing_roads(target);