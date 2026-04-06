/** @noSelf */
interface SkynetAPI {
    // 服务管理
    start(init: () => void): void;
    exit(): void;
    newservice(name: string, ...args: string[]): Address;
    uniqueservice(name: string, ...args: string[]): Address;
    queryservice(name: string): Address;

    // 消息通信
    send(addr: Address, typename: string, ...args: any[]): void;
    call(addr: Address, typename: string, ...args: any[]): any;
    rawcall(addr: Address, typename: string, msg: any, sz: number): [any, number];
    ret(msg?: any, sz?: number): boolean;
    retpack(...args: any[]): boolean;
    response(pack?: (...args: any[]) => [any, number]): ResponseFunc;
    redirect(dest: Address, source: Address, typename: string, ...args: any[]): void;
    ignoreret(): void;

    // 协程
    fork(fn: () => void, ...args: any[]): LuaThread;
    timeout(ti: number, fn: () => void): LuaThread;
    sleep(ti: number, token?: any): "BREAK" | undefined;
    yield(): "BREAK" | undefined;
    wait(token?: any): void;
    wakeup(token: any): boolean;
    killthread(thread: any): LuaThread;

    // 信息
    self(): Address;
    now(): number;
    time(): number;
    starttime(): number;
    hpc(): number;
    error(...args: any[]): void;
    address(addr: number): string;
    harbor(addr: number): number;

    // 协议 & 调度
    dispatch(typename: string, handler: Function): Function | undefined;
    register_protocol(config: {
        name: string;
        id: number;
        pack?: (...args: any[]) => [any, number];
        unpack?: (msg: any, sz: number) => any;
        dispatch?: (...args: any[]) => void;
    }): void;

    // 序列化
    pack(...args: any[]): [any, number];
    unpack(msg: any, sz: number): any;
    packstring(...args: any[]): string;
    trash(msg: any, sz: number): void;
    tostring(msg: any, sz: number): string;

    // 服务注册
    register(name: string): void;
    getenv(key: string): string | undefined;
    setenv(key: string, value: string): void;

    // 调试
    mqlen(): number;
    endless(): boolean;
    stat(what: string): number;
    task(ret?: any): any;
    trace(info?: string): void;
    tracetag(): string | undefined;
    trace_timeout(on: boolean): void;

    // 消息类型常量
    readonly PTYPE_TEXT: 0;
    readonly PTYPE_RESPONSE: 1;
    readonly PTYPE_MULTICAST: 2;
    readonly PTYPE_CLIENT: 3;
    readonly PTYPE_SYSTEM: 4;
    readonly PTYPE_HARBOR: 5;
    readonly PTYPE_SOCKET: 6;
    readonly PTYPE_ERROR: 7;
    readonly PTYPE_QUEUE: 8;
    readonly PTYPE_DEBUG: 9;
    readonly PTYPE_LUA: 10;
    readonly PTYPE_SNAX: 11;
    readonly PTYPE_TRACE: 12;
}

type Address = number | string;
type LuaThread = any;
type ResponseFunc = (ok: boolean, ...args: any[]) => boolean;

declare const skynet: SkynetAPI;

// lua-protobuf helpers (injected by preload.lua)
declare const pb_decode: (msgType: string, data: string) => any;
declare const pb_encode: (msgType: string, data: any) => string;

// Token system (injected by preload.lua)
/** @noSelf */
interface TokenAccountClaims {
    account_id: number;
    username: string;
}

/** @noSelf */
interface TokenGatewayClaims {
    account_id: number;
    server_id: number;
}

declare const token_generate_account: (accountId: number, username: string) => string;
declare const token_validate_account: (token: string) => TokenAccountClaims | null;
declare const token_generate_gateway: (accountId: number, serverId: number) => string;
declare const token_validate_gateway: (token: string) => TokenGatewayClaims | null;

// Password hashing (injected by preload.lua)
declare function password_hash(password: string): string;
declare function password_verify(password: string, stored_hash: string): boolean;

// MongoDB helpers (injected by preload.lua)
declare function mongo_ensureIndex(col: any, spec: any): void;
declare function mongo_findAndModify(col: any, options: any): any;

// Lua os module (for environment variables)
declare namespace os {
    function getenv(varname: string): string | null;
    function time(): number;
}
