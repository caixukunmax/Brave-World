import { Router } from 'express';

export function createServerRoutes(): Router {
  const router = Router();

  // ─── Server Lifecycle ───
  router.get('/server/statuses', (req, res) => {
    const { serverManager } = req.app.locals.services;
    res.json(serverManager.getAllStatuses());
  });

  router.get('/server/status', (req, res) => {
    const { serverManager } = req.app.locals.services;
    const profileId = req.query.profileId as string;
    if (profileId) {
      res.json(serverManager.getStatus(profileId));
    } else {
      const statuses = serverManager.getAllStatuses();
      res.json(statuses[0] || { status: 'stopped' });
    }
  });

  router.get('/server/output', (req, res) => {
    const { serverManager } = req.app.locals.services;
    const profileId = req.query.profileId as string || 'dev';
    res.json({ lines: serverManager.getOutput(profileId) });
  });

  router.post('/server/start', async (req, res) => {
    try {
      const { profileManager, serverManager } = req.app.locals.services;
      const profileId = req.body.profileId || 'dev';
      const profile = await profileManager.get(profileId);
      if (!profile) return res.status(404).json({ error: `Profile ${profileId} not found` });
      const result = await serverManager.start(profile);
      res.json(result);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/server/stop', async (req, res) => {
    try {
      const { serverManager } = req.app.locals.services;
      const profileId = req.body.profileId || 'dev';
      await serverManager.stop(profileId);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/server/restart', async (req, res) => {
    try {
      const { profileManager, serverManager } = req.app.locals.services;
      const profileId = req.body.profileId || 'dev';
      const profile = await profileManager.get(profileId);
      if (!profile) return res.status(404).json({ error: `Profile ${profileId} not found` });
      await serverManager.restart(profile);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/server/command', (req, res) => {
    try {
      const { serverManager } = req.app.locals.services;
      const profileId = req.body.profileId || 'dev';
      serverManager.sendCommand(profileId, req.body.command);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // ─── Build Pipeline ───
  // 完整重启 — 直接运行 restart.bat（和手动执行完全一致）
  router.post('/build/restart', async (req, res) => {
    try {
      const { serverManager, buildRunner } = req.app.locals.services;
      const profileId = req.body.profileId || 'dev';

      const pushLine = (line: string) => {
        serverManager.appendOutput(profileId, `[RESTART] ${line}`);
      };

      pushLine('======== 完整重启 (restart.bat) ========');
      const buildResult = await buildRunner.runRestartBat(pushLine);
      pushLine(`======== 结束 (exit: ${buildResult.exitCode}) ========`);

      res.json({ success: buildResult.success, output: buildResult.output });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/build/:type', async (req, res) => {
    try {
      const { buildRunner } = req.app.locals.services;
      const type = req.params.type;
      let result;
      switch (type) {
        case 'tables': result = await buildRunner.buildTables(); break;
        case 'proto': result = await buildRunner.buildProto(); break;
        case 'server': result = await buildRunner.buildServer(); break;
        case 'all': result = await buildRunner.buildAll(); break;
        default: return res.status(400).json({ error: 'Invalid build type' });
      }
      res.json(result);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // ─── Config Management ───
  router.get('/config/tables', async (req, res) => {
    try {
      const { configReader } = req.app.locals.services;
      const tables = await configReader.listTables();
      res.json(tables);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/config/tables/:name', async (req, res) => {
    try {
      const { configReader } = req.app.locals.services;
      const data = await configReader.readTable(req.params.name);
      res.json(data);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/config/tables/:name', async (req, res) => {
    try {
      const { configWriter } = req.app.locals.services;
      await configWriter.writeTable(req.params.name, req.body);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/config/tables/:name/diff', async (req, res) => {
    try {
      const { configDiff } = req.app.locals.services;
      const diff = await configDiff.diffTable(req.params.name);
      res.json(diff);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/config/tables/:name/rollback', async (req, res) => {
    try {
      const { configWriter } = req.app.locals.services;
      await configWriter.rollbackTable(req.params.name);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/config/tables/:name/validate', async (req, res) => {
    try {
      const { configValidator } = req.app.locals.services;
      const results = await configValidator.validateTable(req.params.name);
      res.json(results);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/config/map-registry', async (req, res) => {
    try {
      const { configReader } = req.app.locals.services;
      res.json(await configReader.readMapRegistry());
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/config/map-registry', async (req, res) => {
    try {
      const { configWriter } = req.app.locals.services;
      await configWriter.writeMapRegistry(req.body);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/config/buildings', async (req, res) => {
    try {
      const { configReader } = req.app.locals.services;
      res.json(await configReader.readBuildings());
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/config/buildings', async (req, res) => {
    try {
      const { configWriter } = req.app.locals.services;
      await configWriter.writeBuildings(req.body);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/config/appsettings', async (req, res) => {
    try {
      const { configReader } = req.app.locals.services;
      res.json(await configReader.readAppSettings());
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/config/appsettings', async (req, res) => {
    try {
      const { configWriter } = req.app.locals.services;
      await configWriter.writeAppSettings(req.body);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // ─── Database Management ───
  router.get('/db/accounts', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      const accounts = await mongoClient.listAccounts({ username: req.query.search as string });
      res.json(accounts);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/db/accounts/:id', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      const account = await mongoClient.getAccount(parseInt(req.params.id));
      res.json(account);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/db/accounts', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      const account = await mongoClient.createAccount(req.body);
      res.json(account);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/db/accounts/:id', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      await mongoClient.updateAccount(parseInt(req.params.id), req.body);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/db/accounts/:id/ban', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      await mongoClient.banAccount(parseInt(req.params.id));
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/db/accounts/:id/unban', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      await mongoClient.unbanAccount(parseInt(req.params.id));
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/db/accounts/:id/reset-password', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      await mongoClient.resetPassword(parseInt(req.params.id), req.body.password);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/db/roles', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      const roles = await mongoClient.listRoles({ role_name: req.query.search as string });
      res.json(roles);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/db/roles/:id', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      const role = await mongoClient.getRole(parseInt(req.params.id));
      res.json(role);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/db/roles/:id', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      await mongoClient.updateRole(parseInt(req.params.id), req.body);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/db/inventory/:roleId', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      res.json(await mongoClient.getInventory(parseInt(req.params.roleId)));
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/db/inventory/:roleId/add', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      await mongoClient.addItem(parseInt(req.params.roleId), req.body.itemId, req.body.count);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/db/inventory/:roleId/remove', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      await mongoClient.removeItem(parseInt(req.params.roleId), req.body.itemId, req.body.count);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/db/chests', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      res.json(await mongoClient.listChests());
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/db/chests', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      const chest = await mongoClient.createChest(req.body);
      res.json(chest);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.delete('/db/chests/:id', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      await mongoClient.deleteChest(parseInt(req.params.id));
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/db/stats', async (req, res) => {
    try {
      const { mongoClient } = req.app.locals.services;
      res.json(await mongoClient.getStats());
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // ─── Logs ───
  router.get('/logs', async (req, res) => {
    try {
      const { logReader } = req.app.locals.services;
      const lines = parseInt(req.query.lines as string) || 200;
      res.json(await logReader.getHistory(lines));
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // ─── Monitoring (proxy to GameServer HTTP API) ───
  router.get('/monitoring/status', async (req, res) => {
    try {
      const { httpClient } = req.app.locals.services;
      res.json(await httpClient.getStatus());
    } catch (e: any) {
      res.json({ cpu: 0, memory: 0, uptime: 0, onlinePlayers: 0, error: 'GameServer not reachable' });
    }
  });

  router.get('/monitoring/players', async (req, res) => {
    try {
      const { httpClient } = req.app.locals.services;
      res.json(await httpClient.getPlayers());
    } catch (e: any) {
      res.json([]);
    }
  });

  router.get('/monitoring/maps', async (req, res) => {
    try {
      const { httpClient } = req.app.locals.services;
      res.json(await httpClient.getMaps());
    } catch (e: any) {
      res.json([]);
    }
  });

  // ─── GM Operations ───
  router.post('/gm/command', async (req, res) => {
    try {
      const { httpClient } = req.app.locals.services;
      const result = await httpClient.sendGmCommand(req.body.command, req.body.targetPlayerId);
      res.json(result);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/gm/broadcast', async (req, res) => {
    try {
      const { httpClient } = req.app.locals.services;
      const result = await httpClient.broadcast(req.body.message);
      res.json(result);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/gm/kick', async (req, res) => {
    try {
      const { httpClient } = req.app.locals.services;
      const result = await httpClient.kickPlayer(req.body.accountId);
      res.json(result);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // ─── Scheduled Tasks ───
  router.get('/tasks', async (req, res) => {
    try {
      const { taskScheduler } = req.app.locals.services;
      res.json(await taskScheduler.list());
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/tasks', async (req, res) => {
    try {
      const { taskScheduler } = req.app.locals.services;
      const task = await taskScheduler.create(req.body);
      res.json(task);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/tasks/:id', async (req, res) => {
    try {
      const { taskScheduler } = req.app.locals.services;
      await taskScheduler.update(req.params.id, req.body);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.delete('/tasks/:id', async (req, res) => {
    try {
      const { taskScheduler } = req.app.locals.services;
      await taskScheduler.delete(req.params.id);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // ─── Maps ───
  router.get('/maps', async (req, res) => {
    try {
      const { mapReader } = req.app.locals.services;
      res.json(await mapReader.listMaps());
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // 地图详情 (必须在 /maps/:name 之前)
  router.get('/maps/:name/detail', async (req, res) => {
    try {
      const { mapReader } = req.app.locals.services;
      res.json(await mapReader.getMapDetail(req.params.name));
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/maps/:name', async (req, res) => {
    try {
      const { mapReader } = req.app.locals.services;
      res.json(await mapReader.readMap(req.params.name));
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/maps/:name', async (req, res) => {
    try {
      const { mapReader } = req.app.locals.services;
      await mapReader.writeMap(req.params.name, req.body);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.delete('/maps/:name', async (req, res) => {
    try {
      const { mapReader } = req.app.locals.services;
      await mapReader.deleteMap(req.params.name);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  // ─── Profiles ───
  router.get('/profiles', async (req, res) => {
    try {
      const { profileManager } = req.app.locals.services;
      res.json(await profileManager.list());
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.get('/profiles/:id', async (req, res) => {
    try {
      const { profileManager } = req.app.locals.services;
      const profile = await profileManager.get(req.params.id);
      if (!profile) return res.status(404).json({ error: 'Not found' });
      res.json(profile);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.post('/profiles', async (req, res) => {
    try {
      const { profileManager } = req.app.locals.services;
      const profile = await profileManager.create(req.body);
      res.json(profile);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.put('/profiles/:id', async (req, res) => {
    try {
      const { profileManager } = req.app.locals.services;
      const profile = await profileManager.update(req.params.id, req.body);
      res.json(profile);
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  router.delete('/profiles/:id', async (req, res) => {
    try {
      const { profileManager } = req.app.locals.services;
      await profileManager.delete(req.params.id);
      res.json({ success: true });
    } catch (e: any) {
      res.status(500).json({ error: e.message });
    }
  });

  return router;
}