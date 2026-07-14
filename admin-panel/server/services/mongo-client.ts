import { MongoClient as Mongo, Db, Collection, Document } from 'mongodb';

export class MongoClient {
  private client: Mongo | null = null;
  private db: Db | null = null;

  private async connect(): Promise<Db> {
    if (this.db) return this.db;
    const host = process.env.MONGO_HOST || '127.0.0.1';
    const port = process.env.MONGO_PORT || '27017';
    const database = process.env.MONGO_DB || 'tslua2';
    const uri = `mongodb://${host}:${port}/${database}`;

    this.client = new Mongo(uri);
    await this.client.connect();
    this.db = this.client.db(database);
    console.log(`[MongoDB] Connected to ${host}:${port}/${database}`);
    return this.db;
  }

  private async collection(name: string): Promise<Collection<Document>> {
    const db = await this.connect();
    return db.collection(name);
  }

  async listAccounts(filter?: { username?: string }): Promise<Document[]> {
    const col = await this.collection('accounts');
    const query: Document = {};
    if (filter?.username) query.username = { $regex: filter.username, $options: 'i' };
    return col.find(query).limit(100).toArray();
  }

  async getAccount(id: number): Promise<Document | null> {
    const col = await this.collection('accounts');
    return col.findOne({ account_id: id });
  }

  async createAccount(data: { username: string; password: string }): Promise<Document> {
    const col = await this.collection('accounts');
    const counters = await this.collection('counters');
    const counter = await counters.findOneAndUpdate(
      { _id: 'account_id' },
      { $inc: { seq: 1 } },
      { upsert: true, returnDocument: 'after' }
    );
    const accountId = counter?.seq || 1000;
    const doc = { account_id: accountId, username: data.username, password: data.password, status: 1, created_at: new Date() };
    await col.insertOne(doc);
    return doc;
  }

  async updateAccount(id: number, data: Record<string, unknown>): Promise<void> {
    const col = await this.collection('accounts');
    await col.updateOne({ account_id: id }, { $set: data });
  }

  async banAccount(id: number): Promise<void> {
    await this.updateAccount(id, { status: 0 });
  }

  async unbanAccount(id: number): Promise<void> {
    await this.updateAccount(id, { status: 1 });
  }

  async resetPassword(id: number, newPassword: string): Promise<void> {
    await this.updateAccount(id, { password: newPassword });
  }

  async listRoles(filter?: { role_name?: string }): Promise<Document[]> {
    const col = await this.collection('roles');
    const query: Document = {};
    if (filter?.role_name) query.role_name = { $regex: filter.role_name, $options: 'i' };
    return col.find(query).limit(100).toArray();
  }

  async getRole(id: number): Promise<Document | null> {
    const col = await this.collection('roles');
    return col.findOne({ role_id: id });
  }

  async updateRole(id: number, data: Record<string, unknown>): Promise<void> {
    const col = await this.collection('roles');
    await col.updateOne({ role_id: id }, { $set: data });
  }

  async getInventory(roleId: number): Promise<Document[]> {
    const col = await this.collection('inventories');
    return col.find({ role_id: roleId }).toArray();
  }

  async addItem(roleId: number, itemId: number, count: number): Promise<void> {
    const col = await this.collection('inventories');
    const existing = await col.findOne({ role_id: roleId, item_id: itemId });
    if (existing) {
      await col.updateOne({ role_id: roleId, item_id: itemId }, { $inc: { count } });
    } else {
      await col.insertOne({ role_id: roleId, item_id: itemId, count });
    }
  }

  async removeItem(roleId: number, itemId: number, count: number): Promise<void> {
    const col = await this.collection('inventories');
    await col.updateOne({ role_id: roleId, item_id: itemId }, { $inc: { count: -count } });
    await col.deleteOne({ role_id: roleId, item_id: itemId, count: { $lte: 0 } });
  }

  async listChests(): Promise<Document[]> {
    const col = await this.collection('gm_chests');
    return col.find({}).toArray();
  }

  async createChest(data: Record<string, unknown>): Promise<Document> {
    const col = await this.collection('gm_chests');
    const result = await col.insertOne(data);
    return { ...data, _id: result.insertedId };
  }

  async deleteChest(id: number): Promise<void> {
    const col = await this.collection('gm_chests');
    await col.deleteOne({ id });
  }

  async getStats(): Promise<{ totalAccounts: number; totalRoles: number; rolesByJob: Record<string, number> }> {
    const accountsCol = await this.collection('accounts');
    const rolesCol = await this.collection('roles');
    const totalAccounts = await accountsCol.countDocuments();
    const totalRoles = await rolesCol.countDocuments();
    const jobAgg = await rolesCol.aggregate([{ $group: { _id: '$job', count: { $sum: 1 } } }]).toArray();
    const rolesByJob: Record<string, number> = {};
    for (const item of jobAgg) {
      if (item._id) rolesByJob[item._id] = item.count;
    }
    return { totalAccounts, totalRoles, rolesByJob };
  }

  async disconnect(): Promise<void> {
    if (this.client) {
      await this.client.close();
      this.client = null;
      this.db = null;
    }
  }
}