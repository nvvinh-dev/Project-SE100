#!/usr/bin/env bash
# Sao lưu database demo (Supabase) ra file, lưu ngoài Supabase (NFR-07).
# Gói miễn phí của Supabase không tự sao lưu: chạy ít nhất mỗi tuần một lần và trước mỗi buổi demo.
#
# Cách dùng:
#   export DB_URL='postgresql://<user>:<mật khẩu>@<host Session pooler>:5432/postgres'
#   bash scripts/backup-db.sh
#
# Chuỗi kết nối chỉ đọc từ biến môi trường DB_URL, không ghi vào file nào.
# pg_dump chạy trong image postgres:17 để cùng phiên bản với database, máy không cần cài PostgreSQL.

set -euo pipefail

: "${DB_URL:?Chưa đặt biến môi trường DB_URL (chuỗi kết nối Session pooler của Supabase)}"

backup_dir="${BACKUP_DIR:-backups}"
mkdir -p "$backup_dir"
backup_file="$backup_dir/householdfarm-$(date +%Y%m%d-%H%M%S).dump"

# DB_URL được truyền vào container qua biến môi trường và chỉ mở rộng bên trong container,
# để chuỗi kết nối không nằm trên dòng lệnh của máy.
if ! docker run --rm -e DB_URL postgres:17 \
    sh -c 'pg_dump --format=custom --no-owner --no-privileges --schema=public "$DB_URL"' > "$backup_file"; then
  rm -f "$backup_file"
  echo "Sao lưu thất bại." >&2
  exit 1
fi

# Lệnh khôi phục cũng chạy qua image postgres:17 để máy không cần cài PostgreSQL.
cat <<EOF
Đã sao lưu vào $backup_file

Khôi phục vào một database trống: đặt DB_URL là chuỗi kết nối của database đích, rồi chạy
  docker run --rm -i -e DB_URL postgres:17 sh -c 'pg_restore --no-owner --no-privileges --dbname="\$DB_URL"' < $backup_file
Database đích chạy trên chính máy này thì dùng host host.docker.internal thay cho localhost.
Lỗi 'schema "public" already exists' khi khôi phục là vô hại: database đích đã có sẵn schema public.
EOF
