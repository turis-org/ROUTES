#!/bin/bash
set -e  # Прерывать при ошибках

# Функция выполнения SQL от имени postgres
exec_psql() {
    PGPASSWORD=${PG_PASSWORD} psql -v ON_ERROR_STOP=1 -U "admin" -d "${PG_DATABASE}" <<-EOSQL
        $1
EOSQL
}

#    CREATE ROLE '${PG_USER}' WITH CREATEDB LOGIN PASSWORD '${PG_PASSWORD}';
# 1. Создание пользователей и ролей
exec_psql "
    CREATE ROLE '${REPL_USER}' WITH REPLICATION LOGIN PASSWORD '${REPL_PASSWD}';
"

# 2. Создание баз данных
exec_psql "
    CREATE DATABASE '${PG_DATABASE}' WITH OWNER '${PG_USER}';
    GRANT ALL PRIVILEGES ON DATABASE '${PG_DATABASE}' TO '${PG_USER}';
"

# 3. Настройка расширений

# 4. Настройка публикаций для логической репликации
if [ "$IS_MASTER" = "true" ]; then
    exec_psql "
	CREATE PUBLICATION sync_routes_pub FOR ALL TABLES;
	SELECT * FROM pg_create_physical_replication_slot('replica1_slot');
	SELECT * FROM pg_create_physical_replication_slot('replica2_slot');
	"
	# Настройка pg_hba.conf
	cat > "$PGDATA/pg_hba.conf" <<EOF
# TYPE  DATABASE  USER  ADDRESS  METHOD
host    ${PG_DATABASE}  ${PG_USER}   all			 scram-sha-256
host    replication		${REPL_USER} routes_db_repl1 scram-sha-256
host	replication		${REPL_USER} routes_db_repl2 scram-sha-256
EOF

fi

if [ "$IS_SYNC_REPL1" = "true" ]; then
	exec_psql "
	CREATE PUBLICATION routes_pub FOR ALL TABLES;
	CREATE SUBSCRIPTION routes_sub
	CONNECTION 'host=routes_db port=${PG_PORT} user=${REPL_USER} password=${REPL_PASSWD} application_name=routes_db_repl1'
	PUBLICATION sync_routes_pub
	WITH (copy_data=true, create_slot=false, slot_name='replica1_slot');
	"

	# Настройка pg_hba.conf
	cat > "$PGDATA/pg_hba.conf" <<EOF
# TYPE  DATABASE  USER  ADDRESS  METHOD
host    ${PG_DATABASE}  ${PG_USER}   all					scram-sha-256
host    replication		${REPL_USER} routes_db_repl1_backup scram-sha-256
EOF

fi

if [ "$IS_SYNC_REPL2" = "true" ]; then
	exec_psql "
	CREATE PUBLICATION routes_pub FOR ALL TABLES;
	CREATE SUBSCRIPTION routes_sub
	CONNECTION 'host=routes_db port=${PG_PORT} user=${REPL_USER} password=${REPL_PASSWD} application_name=routes_db_repl2'
	PUBLICATION sync_routes_pub
	WITH (copy_data=true, create_slot=false, slot_name='replica2_slot');

	"

	# Настройка pg_hba.conf
	cat > "$PGDATA/pg_hba.conf" <<EOF
# TYPE  DATABASE  USER  ADDRESS  METHOD
host    ${PG_DATABASE}  ${PG_USER}   all					scram-sha-256
host    replication		${REPL_USER} routes_db_repl2_backup scram-sha-256
EOF

fi

if [ "$IS_REPL1" = "true" ]; then
	exec_psql "
	CREATE SUBSCRIPTION routes_sub
	CONNECTION 'host=routes_db_repl1 port=${PG_PORT} user=${REPL_USER} password=${REPL_PASSWD} application_name=routes_db_repl1_backup'
	PUBLICATION routes_pub
	WITH (copy_data=true, create_slot=true, slot_name='backup1_slot');
	"
fi

if [ "$IS_REPL2" = "true" ]; then
	exec_psql "
	CREATE SUBSCRIPTION routes_sub
	CONNECTION 'host=routes_db_repl2 port=${PG_PORT} user=${REPL_USER} password=${REPL_PASSWD} application_name=routes_db_repl2_backup'
	PUBLICATION routes_pub
	WITH (copy_data=true, create_slot=true, slot_name='backup2_slot');
	"
fi


echo "PostgreSQL initialization complete!"
