export class HttpClient {
  private getBaseUrl(): string {
    const host = process.env.GAMESERVER_HTTP_HOST || '127.0.0.1';
    const port = process.env.GAMESERVER_HTTP_PORT || '8890';
    return `http://${host}:${port}`;
  }

  private async request<T>(method: string, endpoint: string, body?: unknown): Promise<T> {
    const url = `${this.getBaseUrl()}${endpoint}`;
    const options: RequestInit = {
      method,
      headers: { 'Content-Type': 'application/json' },
    };
    if (body) options.body = JSON.stringify(body);

    const response = await fetch(url, options);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    return response.json() as Promise<T>;
  }

  async getStatus(): Promise<unknown> {
    return this.request('GET', '/api/status');
  }

  async getPlayers(): Promise<unknown[]> {
    return this.request('GET', '/api/players');
  }

  async getMaps(): Promise<unknown[]> {
    return this.request('GET', '/api/maps');
  }

  async sendGmCommand(command: string, targetPlayerId?: number): Promise<unknown> {
    return this.request('POST', '/api/gm', { command, targetPlayerId });
  }

  async broadcast(message: string): Promise<unknown> {
    return this.request('POST', '/api/broadcast', { message });
  }

  async kickPlayer(accountId: number): Promise<unknown> {
    return this.request('POST', '/api/kick', { accountId });
  }
}