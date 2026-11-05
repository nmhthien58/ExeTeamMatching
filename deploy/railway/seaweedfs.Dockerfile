# BE-85 – SeaweedFS (S3) cho Railway: sinh s3.json từ S3_ACCESS_KEY / S3_SECRET_KEY lúc khởi động thay vì mount file
# (khóa dev trong deploy/seaweedfs/s3.json đã công khai trên GitHub, không dùng ngoài máy dev).
FROM chrislusf/seaweedfs:3.97
COPY deploy/railway/seaweedfs-entrypoint.sh /seaweedfs-entrypoint.sh
ENTRYPOINT ["/bin/sh", "/seaweedfs-entrypoint.sh"]
