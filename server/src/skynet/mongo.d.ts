/** @noSelf */
interface MongoClientConfig {
    host: string;
    port?: number;
    username?: string;
    password?: string;
    authmod?: string;
    authdb?: string;
}

/** @noSelf */
interface MongoClient {
    disconnect(): void;
    runCommand(...args: any[]): any;
}

/** @noSelf */
interface MongoDb {
    runCommand(...args: any[]): any;
    auth(user: string, pass: string): boolean;
}

/** @noSelf */
interface MongoCollection {
}

/** @noSelf */
interface MongoCursor {
    hasNext(): boolean;
    next(): any;
    sort(key: any): MongoCursor;
    skip(amount: number): MongoCursor;
    limit(amount: number): MongoCursor;
    close(): void;
}

/** @noSelf */
interface MongoModule {
    client(conf: MongoClientConfig): MongoClient;
}

declare const mongo: MongoModule;

// tstl 冒号调用辅助（在 preload.lua 中实现）
// tstl 无法对任意对象生成 Lua 冒号调用，这些函数包装了冒号语法
declare const mongo_findOne: (col: any, query: any) => any;
declare const mongo_insert: (col: any, doc: any) => void;
declare const mongo_update: (col: any, query: any, update: any, upsert?: boolean, multi?: boolean) => void;
declare const mongo_delete: (col: any, query: any, single?: boolean) => void;
declare const mongo_findArray: (col: any, query: any) => any[];
declare const mongo_count: (col: any, query: any) => number;
