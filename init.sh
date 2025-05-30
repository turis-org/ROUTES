#!/bin/bash

set -e  # Прерывать при ошибках

set -a
source .env # Экспорт переменных окуржения
set +a

# Функция выполнения SQL от имени postgres
exec_psql() {
	docker exec -i -e PGPASSWORD=${PG_PASSWORD} $1 psql -v ON_ERROR_STOP=1 -U "${PG_USER}" -d "${PG_DATABASE}" <<-EOSQL
        $2
EOSQL
}


#    CREATE ROLE '${PG_USER}' WITH CREATEDB LOGIN PASSWORD '${PG_PASSWORD}';
# 1. Создание пользователя для отладки репликации
exec_psql "routes_db" "
    CREATE ROLE ${REPL_USER} WITH REPLICATION LOGIN PASSWORD '${REPL_PASSWD}';
"
exec_psql "routes_db_repl1" "
    CREATE ROLE ${REPL_USER} WITH REPLICATION LOGIN PASSWORD '${REPL_PASSWD}';
"
exec_psql "routes_db_repl2" "
    CREATE ROLE ${REPL_USER} WITH REPLICATION LOGIN PASSWORD '${REPL_PASSWD}';
"
exec_psql "routes_db_repl1_backup" "
    CREATE ROLE ${REPL_USER} WITH REPLICATION LOGIN PASSWORD '${REPL_PASSWD}';
"
exec_psql "routes_db_repl2_backup" "
    CREATE ROLE ${REPL_USER} WITH REPLICATION LOGIN PASSWORD '${REPL_PASSWD}';
"

# 4. Настройка публикаций для логической репликации
exec_psql "routes_db" "
	CREATE PUBLICATION sync_routes_pub FOR ALL TABLES;
	SELECT * FROM pg_create_physical_replication_slot('replica1_slot');
	SELECT * FROM pg_create_physical_replication_slot('replica2_slot');
"


exec_psql "routes_db_repl1" "
	CREATE PUBLICATION routes_pub FOR ALL TABLES;
	CREATE SUBSCRIPTION routes_sub
	CONNECTION 'host=routes_db port=${PG_PORT} user=${REPL_USER} password=${REPL_PASSWD} dbname=replication'
	PUBLICATION sync_routes_pub
	WITH (copy_data=true, create_slot=false, slot_name='replica1_slot');
"

exec_psql "routes_db_repl2" "
	CREATE PUBLICATION routes_pub FOR ALL TABLES;
	CREATE SUBSCRIPTION routes_sub
	CONNECTION 'host=routes_db port=${PG_PORT} user=${REPL_USER} password=${REPL_PASSWD} dbname=replication'
	PUBLICATION sync_routes_pub
	WITH (copy_data=true, create_slot=false, slot_name='replica2_slot');
"

exec_psql "routes_db" "
	ALTER SYSTEM SET synchronous_standby_names TO 'FIRST 2 (routes_db_repl1, routes_db_repl2)';
	SELECT pg_reload_conf();
"

exec_psql "routes_db_repl1_backup" "
	CREATE SUBSCRIPTION routes_sub
	CONNECTION 'host=routes_db_repl1 port=${PGR1_PORT} user=${REPL_USER} password=${REPL_PASSWD} application_name=routes_db_repl1_backup'
	PUBLICATION routes_pub
	WITH (copy_data=true, create_slot=true, slot_name='backup1_slot');
	"

exec_psql "routes_db_repl2_backup" "
	CREATE SUBSCRIPTION routes_sub
	CONNECTION 'host=routes_db_repl2 port=${PGR2_PORT} user=${REPL_USER} password=${REPL_PASSWD} application_name=routes_db_repl2_backup'
	PUBLICATION routes_pub
	WITH (copy_data=true, create_slot=true, slot_name='backup2_slot');
	"

echo "PostgreSQL initialization complete!"
