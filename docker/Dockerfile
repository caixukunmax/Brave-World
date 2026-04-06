# ============================================================
# Stage 1: 编译 Skynet
# ============================================================
FROM ubuntu:22.04 AS builder

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    gcc \
    make \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /build/skynet

# 拷贝 skynet 源码（3rd/lua 有源码，jemalloc 子模块目录为空需拉取）
COPY skynet/ .

# 跳过 jemalloc（Docker 构建环境可能无外网，用系统 malloc）
RUN make linux MYCFLAGS="-I../../skynet-src -g -DNOUSE_JEMALLOC" MALLOC_STATICLIB=

# 验证关键产物
RUN test -x skynet && \
    test -f cservice/snlua.so && \
    test -f cservice/gate.so && \
    test -f luaclib/skynet.so

# 编译 lua-protobuf
RUN apt-get update && apt-get install -y --no-install-recommends git && \
    git clone https://github.com/starwing/lua-protobuf /build/lua-protobuf && \
    cd /build/lua-protobuf && \
    make LUABINDIR=/build/skynet/3rd/lua LUA_DIR=/build/skynet/3rd/lua && \
    rm -rf /var/lib/apt/lists/*

# ============================================================
# Stage 2: 最小运行镜像
# ============================================================
FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    libgcc-s1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# 从 builder 拷贝编译产物
COPY --from=builder /build/skynet/skynet    ./skynet
COPY --from=builder /build/skynet/cservice  ./cservice
COPY --from=builder /build/skynet/luaclib   ./luaclib
COPY --from=builder /build/skynet/lualib    ./lualib
COPY --from=builder /build/skynet/service   ./service
COPY --from=builder /build/lua-protobuf/pb.so ./luaclib/pb.so

# 拷贝业务生成文件（宿主机构建，非 Docker 内构建）
COPY tslua/   ./lualib/tslua
COPY protos/  ./lualib/tslua/protos
COPY tables/  ./lualib/tables

# 拷贝自定义配置和启动脚本
COPY config.lua   ./config.lua
COPY main.lua     ./main.lua
COPY preload.lua  ./preload.lua
COPY start.sh     ./start.sh
COPY gateway_service.lua ./gateway_service.lua

RUN chmod +x ./start.sh

EXPOSE 8889 8000

ENTRYPOINT ["./start.sh"]
