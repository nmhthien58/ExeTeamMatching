# BE-85 – Postgres cho Railway: giống service postgres trong docker-compose.yml, chép sẵn DB script (6 database,
# user riêng cho từng service, danh mục, tài khoản Bộ môn) vì Railway không mount thư mục từ repo được.
# Mật khẩu đặt bằng Railway Variables (POSTGRES_PASSWORD, *_DB_PASSWORD, ADMIN_EMAIL, ADMIN_NAME), không ghi ở đây.
FROM postgres:16-alpine
COPY deploy/db/ /docker-entrypoint-initdb.d/
# Volume Railway có lost+found ở gốc nên dữ liệu đặt trong thư mục con.
ENV PGDATA=/var/lib/postgresql/data/pgdata
