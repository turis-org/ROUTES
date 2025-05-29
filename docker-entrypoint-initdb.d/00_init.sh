#!/bin/bash

exec_psql() {
	PGPASSWORD=${POSTGRES_PASSWORD} psql -v ON_ERROR_STOP=1 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" <<-EOSQL
        $1
EOSQL
}

exec_psql "GRANT ALL PRIVILEGES ON DATABASE ${POSTGRES_DB} TO ${POSTGRES_USER};"

exec_psql "CREATE EXTENSION IF NOT EXISTS postgis;"
exec_psql "CREATE EXTENSION IF NOT EXISTS hstore;"
exec_psql "CREATE EXTENSION IF NOT EXISTS pgrouting;"

touch /var/lib/postgresql/flat_node

# Импорт данных в PostgreSQL
for map in $(ls /tmp/maps)
do
echo $map
nohup osm2pgsql -F /var/lib/postgresql/flat_node -d $POSTGRES_DB -U $POSTGRES_USER --cache=1000 --number-processes=4 --create --multi-geometry --slim --drop --hstore --proj 3857 /tmp/maps/$map
done

if [ ${IS_MASTER} = "true" ]; then

	cat > "$PGDATA/pg_hba.conf" <<EOF
		# TYPE  DATABASE  USER  ADDRESS  METHOD
		local   all       all             scram-sha-256
		host    ${POSTGRES_DB}	${POSTGRES_USER}		   all			   scram-sha-256
		host    replication		${REPL_USER}	   routes_db_repl1		   scram-sha-256
		host	replication		${REPL_USER}	   routes_db_repl2		   scram-sha-256
		host    replication		${REPL_USER}	   172.18.0.1/16		   scram-sha-256
		host	replication		${REPL_USER}	   172.18.0.3/16		   trust
		host	replication		${REPL_USER}	   172.18.0.4/16		   trust
EOF

fi

if [ ${IS_SYNC_REPL1} = "true" ]; then

	cat > "$PGDATA/pg_hba.conf" <<EOF
		# TYPE  DATABASE  USER  ADDRESS  METHOD
		local   all       all             scram-sha-256
		host    ${POSTGRES_DB}	${POSTGRES_USER}		   all			   scram-sha-256
		host    replication		${REPL_USER}	   routes_db_repl1_backup:${PGR1B_PORT} trust
		host	replication		${REPL_USER}	   routes_db				scram-sha-256
		host    replication		${REPL_USER}	   172.18.0.1/16		   trust
		host	replication		${REPL_USER}	   172.18.0.2/16		   trust
EOF

fi

if [ ${IS_SYNC_REPL2} = "true" ]; then

	cat > "$PGDATA/pg_hba.conf" <<EOF
		# TYPE  DATABASE  USER  ADDRESS  METHOD
		local   all       all             scram-sha-256
		host    ${POSTGRES_DB}	${POSTGRES_USER}		   all			   scram-sha-256
		host    replication		${REPL_USER}	   routes_db_repl2_backup:${PGR1B_PORT} trust
		host	replication		${REPL_USER}	   routes_db:${PG_PORT} 	scram-sha-256
		host    replication		${REPL_USER}	   172.18.0.1/16		   scram-sha-256
		host	replication		${REPL_USER}	   172.18.0.2/16		   scram-sha-256
EOF

fi
