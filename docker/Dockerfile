# ============================================================
# Stage 1: 编译 Skynet 框架
# ============================================================
FROM ubuntu:22.04 AS builder

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    gcc \
    make \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /build/skynet

# 拷贝 skynet 源码
COPY deps/skynet/ .

# 跳过 jemalloc，使用系统 malloc
RUN make linux MYCFLAGS="-I../../skynet-src -g -DNOUSE_JEMALLOC" MALLOC_STATICLIB=

# 编译 lua-protobuf
COPY deps/lua-protobuf /build/lua-protobuf
RUN cd /build/skynet/3rd/lua && \
    make clean && \
    make -j4 "MYCFLAGS=-fPIC -I/build/skynet/skynet-src -DLUA_USE_LINUX" linux && \
    gcc -shared -o liblua.so -Wl,--whole-archive liblua.a -Wl,--no-whole-archive -lm -ldl
RUN cd /build/lua-protobuf && \
    gcc -O2 -fPIC -shared pb.c -o pb.so \
    -I/build/skynet/3rd/lua \
    -L/build/skynet/3rd/lua -llua -lm

# ============================================================
# Stage 2: 最小运行镜像（代码通过 Volume 挂载）
# ============================================================
FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    libgcc-s1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# 只拷贝 Skynet 框架运行时（代码通过 Volume 挂载进来）
COPY --from=builder /build/skynet/skynet    ./skynet
COPY --from=builder /build/skynet/cservice  ./cservice
COPY --from=builder /build/skynet/luaclib   ./luaclib
COPY --from=builder /build/skynet/lualib    ./lualib
COPY --from=builder /build/skynet/service   ./service
COPY --from=builder /build/lua-protobuf/pb.so ./luaclib/pb.so
COPY --from=builder /build/skynet/3rd/lua/liblua.so ./luaclib/liblua.so

EXPOSE 8889 8000

# 直接执行 start.sh（通过 volume 挂载进来，在宿主机上设置可执行权限）
ENTRYPOINT ["/bin/bash", "/app/start.sh"]
