#!/usr/bin/env bash
#
# Backs up everything that cannot be rebuilt from the repository: the PostgreSQL database and the
# MinIO media bucket. Both are taken from the running compose stack, so no host tooling is needed
# beyond docker.
#
#   ./scripts/backup.sh                      # -> $BACKUP_DIR/<timestamp>/
#   BACKUP_DIR=/mnt/backups ./scripts/backup.sh
#
# Suitable for cron. Exits non-zero on any failure so a wrapper can alert:
#   0 3 * * *  cd /opt/pcmarket && ./scripts/backup.sh >> /var/log/pcmarket-backup.log 2>&1

set -Eeuo pipefail

# Git Bash / MSYS rewrites arguments that look like Unix paths, which mangles a docker `-v host:container`
# spec into something that silently becomes an anonymous volume — the mirror then "succeeds" into
# nowhere. Ignored on Linux, where these variables mean nothing.
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL='*'

cd "$(dirname "$0")/.."

# .env holds the credentials the containers use; source it if present.
if [[ -f .env ]]; then
    set -a
    # shellcheck disable=SC1091
    source .env
    set +a
fi

BACKUP_DIR="${BACKUP_DIR:-./backups}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
POSTGRES_USER="${POSTGRES_USER:-pcmarket}"
POSTGRES_DB="${POSTGRES_DB:-pcmarket}"
MINIO_BUCKET="${MINIO_BUCKET:-pcmarket-media}"
MINIO_ROOT_USER="${MINIO_ROOT_USER:-minioadmin}"
MINIO_ROOT_PASSWORD="${MINIO_ROOT_PASSWORD:-minioadmin}"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
TARGET="${BACKUP_DIR}/${STAMP}"
mkdir -p "${TARGET}"
# Bind mounts need a real absolute path — a relative one (or one containing /./) silently becomes an
# anonymous volume, and the media half of the backup would go nowhere.
TARGET_ABS="$(cd "${TARGET}" && pwd)"

log() { printf '[backup] %s\n' "$*"; }

# Report which step failed rather than a bare non-zero exit.
trap 'log "FAILED at line $LINENO"; exit 1' ERR

# --- PostgreSQL -------------------------------------------------------------
# Custom format (-Fc): compressed, and restorable selectively with pg_restore.
log "dumping database '${POSTGRES_DB}'"
docker compose exec -T postgres \
    pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -Fc \
    > "${TARGET}/postgres.dump"

if [[ ! -s "${TARGET}/postgres.dump" ]]; then
    log "database dump is empty — refusing to keep it"
    exit 1
fi

# --- MinIO media ------------------------------------------------------------
# Run mc through compose so it lands on the stack's network and can reach `minio` by name — no need
# to publish MinIO or install anything on the host.
log "mirroring media bucket '${MINIO_BUCKET}'"
docker compose run --rm --entrypoint sh \
    --volume "${TARGET_ABS}:/backup" \
    minio-init -c "
        mc alias set local http://minio:9000 '${MINIO_ROOT_USER}' '${MINIO_ROOT_PASSWORD}' >/dev/null &&
        mkdir -p /backup/media &&
        mc mirror --overwrite local/'${MINIO_BUCKET}' /backup/media
    "

# A mirror that reports success but leaves nothing on the host means the bind mount did not take —
# fail loudly rather than keep a backup that is quietly missing every product image.
if [[ ! -d "${TARGET}/media" ]]; then
    log "media mirror did not reach ${TARGET}/media — check the bind mount"
    exit 1
fi

# --- Metadata ---------------------------------------------------------------
{
    echo "created_utc=${STAMP}"
    echo "postgres_db=${POSTGRES_DB}"
    echo "minio_bucket=${MINIO_BUCKET}"
    echo "image_tag=${IMAGE_TAG:-dev}"
    echo "git_commit=$(git rev-parse --short HEAD 2>/dev/null || echo unknown)"
} > "${TARGET}/manifest.txt"

log "wrote $(du -sh "${TARGET}" | cut -f1) to ${TARGET}"

# --- Retention --------------------------------------------------------------
# Prune only directories that look like our timestamps, so an unrelated file in BACKUP_DIR is safe.
log "pruning backups older than ${BACKUP_RETENTION_DAYS} days"
find "${BACKUP_DIR}" -mindepth 1 -maxdepth 1 -type d -name '20*Z' \
    -mtime "+${BACKUP_RETENTION_DAYS}" -print -exec rm -rf {} + || true

log "done"
