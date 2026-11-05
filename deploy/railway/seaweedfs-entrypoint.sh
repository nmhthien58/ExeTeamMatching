#!/bin/sh
# BE-85: ghi cấu hình S3 từ biến môi trường rồi chạy giống command trong docker-compose.yml.
set -e
: "${S3_ACCESS_KEY:?Thiếu S3_ACCESS_KEY}"
: "${S3_SECRET_KEY:?Thiếu S3_SECRET_KEY}"
mkdir -p /etc/seaweedfs /data
cat > /etc/seaweedfs/s3.json <<JSON
{"identities":[{"name":"collab","credentials":[{"accessKey":"${S3_ACCESS_KEY}","secretKey":"${S3_SECRET_KEY}"}],"actions":["Admin","Read","Write","List","Tagging"]}]}
JSON
exec weed server -dir=/data -s3 -s3.port=8333 -s3.config=/etc/seaweedfs/s3.json -filer.port=8888
