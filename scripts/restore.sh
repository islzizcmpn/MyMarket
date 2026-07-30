#!/usr/bin/env bash
#
# Restores a backup produced by ./scripts/backup.sh.
#
#   ./scripts/restore.sh ./backups/20260726T031500Z
#
# DESTRUCTIVE: this drops and recreates the application database, and overwrites objects in the media
# bucket. It refuses to run without an explicit confirmation. See docs/runbooks/restore.md for the
# full procedure, including stopping the app containers first.

set -Eeuo pipefail

# See backup.sh: stops Git Bash from mangling the docker bind-mount spec. No-ops on Linux.
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL='*'

cd "$(dirname "$0")/.."

SOURCE="${1:-}"
if [[ -z "${SOURCE}" || ! -d "${SOURCE}" ]]; then
    echo "usage: $0 <backup-directory>" >&2
    echo "example: $0 ./backups/20260726T031500Z" >&2
    exit 2
fi

if [[ ! -s "${SOURCE}/postgres.dump" ]]; then
    echo "no postgres.dump in ${SOURCE}" >&2
    exit 2
fi

if [[ -f .env ]]; then
    set -a
    # shellcheck disable=SC1091
    source .env
    set +a
fi

POSTGRES_USER="${POSTGRES_USER:-pcmarket}"
POSTGRES_DB="${POSTGRES_DB:-pcmarket}"
MINIO_BUCKET="${MINIO_BUCKET:-pcmarket-media}"
MINIO_ROOT_USER="${MINIO_ROOT_USER:-minioadmin}"
MINIO_ROOT_PASSWORD="${MINIO_ROOT_PASSWORD:-minioadmin}"

log() { printf '[restore] %s\n' "$*"; }
trap 'log "FAILED at line $LINENO"; exit 1' ERR

cat <<WARNING

  About to restore into database '${POSTGRES_DB}' and bucket '${MINIO_BUCKET}'
  from: ${SOURCE}
  $(cat "${SOURCE}/manifest.txt" 2>/dev/null || true)

  This DROPS the current database. Everything written since the backup is lost.

WARNING

read -r -p "Type the database name to confirm: " CONFIRM
if [[ "${CONFIRM}" != "${POSTGRES_DB}" ]]; then
    log "confirmation did not match; nothing was changed"
    exit 1
fi

# The app holds connections that would block DROP DATABASE, so stop it first. Postgres and MinIO
# stay up because we restore through them.
log "stopping application containers"
docker compose stop api web admin nginx || true

log "recreating database"
docker compose exec -T postgres psql -U "${POSTGRES_USER}" -d postgres -v ON_ERROR_STOP=1 <<SQL
SELECT pg_terminate_backend(pid) FROM pg_stat_activity
 WHERE datname = '${POSTGRES_DB}' AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS "${POSTGRES_DB}";
CREATE DATABASE "${POSTGRES_DB}" OWNER "${POSTGRES_USER}";
SQL

log "restoring dump"
docker compose exec -T postgres \
    pg_restore -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" --no-owner --no-privileges \
    < "${SOURCE}/postgres.dump"

if [[ -d "${SOURCE}/media" ]]; then
    log "restoring media bucket"
    SOURCE_ABS="$(cd "${SOURCE}" && pwd)"
    docker compose run --rm --entrypoint sh \
        --volume "${SOURCE_ABS}:/backup:ro" \
        minio-init -c "
            mc alias set local http://minio:9000 '${MINIO_ROOT_USER}' '${MINIO_ROOT_PASSWORD}' >/dev/null &&
            mc mb --ignore-existing local/'${MINIO_BUCKET}' &&
            mc mirror --overwrite /backup/media local/'${MINIO_BUCKET}' &&
            mc anonymous set download local/'${MINIO_BUCKET}'
        "
else
    log "no media/ directory in the backup — skipping bucket restore"
fi

log "starting application containers"
docker compose up -d

log "done — check https://\${API_HOST}/health before announcing recovery"
