FROM pgrouting/pgrouting:17-3.5-main

LABEL maintainer="Turis Project - <add link>"

ENV USED_MAPS https://download.geofabrik.de/russia/central-fed-district-latest.osm.pbf
ENV CONNECTIONS 4
ENV THREADS 4

# Установим зависимости
RUN apt update && apt install -y aria2 osm2pgsql \
# Скачивание данных OSM
    && mkdir /tmp/maps/ \
    && aria2c -x ${CONNECTIONS} -s ${THREADS} ${USED_MAPS} -d /tmp/maps \
# Удаление ненужных зависимотей
    && apt remove -y aria2