# ==========================================
# Stage 1: Build .NET Web API
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS dotnet-build
WORKDIR /src
COPY ["MafiaCorp.Api/MafiaCorp.Api.csproj", "MafiaCorp.Api/"]
RUN dotnet restore "MafiaCorp.Api/MafiaCorp.Api.csproj"
COPY ["MafiaCorp.Api/", "MafiaCorp.Api/"]
WORKDIR "/src/MafiaCorp.Api"
RUN dotnet publish "MafiaCorp.Api.csproj" -c Release -o /app/api

# ==========================================
# Stage 2: Build Java WebSocket Game Server
# ==========================================
FROM eclipse-temurin:17-jdk-alpine AS java-build
WORKDIR /src
COPY MafiaCorp.GameServer/ /src/
RUN chmod +x ./gradlew && ./gradlew jar --no-daemon

# ==========================================
# Stage 3: Unified Runtime Container
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final

# Install OpenJDK JRE, Nginx, and procps
RUN apt-get update && \
    apt-get install -y --no-install-recommends default-jre-headless nginx procps && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy compiled .NET API and Java Game Server
COPY --from=dotnet-build /app/api ./api
COPY --from=java-build /src/server/build/libs/server.jar ./gameserver/server.jar

# Setup Nginx configuration (reverse proxies /api and /ws on dynamic $PORT)
RUN echo 'events { worker_connections 1024; } \
http { \
    include /etc/nginx/mime.types; \
    default_type application/octet-stream; \
    sendfile on; \
    map $http_upgrade $connection_upgrade { \
        default upgrade; \
        "" close; \
    } \
    server { \
        listen PORT_PLACEHOLDER; \
        server_name _; \
        location /health { \
            access_log off; \
            add_header Content-Type text/plain; \
            return 200 "OK"; \
        } \
        location /ws { \
            proxy_pass http://127.0.0.1:54555; \
            proxy_http_version 1.1; \
            proxy_set_header Upgrade $http_upgrade; \
            proxy_set_header Connection $connection_upgrade; \
            proxy_set_header Host $host; \
            proxy_set_header X-Real-IP $remote_addr; \
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; \
            proxy_set_header X-Forwarded-Proto $scheme; \
            proxy_read_timeout 86400s; \
            proxy_send_timeout 86400s; \
        } \
        location /api/ { \
            proxy_pass http://127.0.0.1:5130/api/; \
            proxy_set_header Host $host; \
            proxy_set_header X-Real-IP $remote_addr; \
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; \
            proxy_set_header X-Forwarded-Proto $scheme; \
        } \
        location /swagger { \
            proxy_pass http://127.0.0.1:5130/swagger; \
            proxy_set_header Host $host; \
            proxy_set_header X-Real-IP $remote_addr; \
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; \
            proxy_set_header X-Forwarded-Proto $scheme; \
        } \
        location / { \
            proxy_pass http://127.0.0.1:5130; \
            proxy_set_header Host $host; \
            proxy_set_header X-Real-IP $remote_addr; \
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; \
            proxy_set_header X-Forwarded-Proto $scheme; \
        } \
    } \
}' > /etc/nginx/nginx.conf

# Setup startup entrypoint script
RUN echo '#!/bin/bash\n\
set -e\n\
PORT="${PORT:-10000}"\n\
echo "Starting MafiaCorp Unified Stack on port $PORT..."\n\
sed -i "s/PORT_PLACEHOLDER/$PORT/g" /etc/nginx/nginx.conf\n\
nginx\n\
export ASPNETCORE_URLS="http://127.0.0.1:5130"\n\
export DOTNET_gcServer=0\n\
dotnet /app/api/MafiaCorp.Api.dll &\n\
API_PID=$!\n\
export HOST="127.0.0.1"\n\
export PORT="54555"\n\
export API_URL="http://127.0.0.1:5130"\n\
export JAVA_OPTS="-Xms64m -Xmx200m -XX:+UseSerialGC"\n\
java $JAVA_OPTS -jar /app/gameserver/server.jar &\n\
JAVA_PID=$!\n\
trap "kill -TERM $API_PID $JAVA_PID; nginx -s quit" SIGINT SIGTERM\n\
wait -n $API_PID $JAVA_PID\n\
' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

EXPOSE 10000
ENTRYPOINT ["/app/entrypoint.sh"]

