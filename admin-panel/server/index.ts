import express from 'express';
import cors from 'cors';
import path from 'path';
import { createServerRoutes } from './routes/index';
import { ProfileManager } from './services/profile-manager';
import { ServerManager } from './services/server-manager';
import { BuildRunner } from './services/build-runner';
import { ConfigReader } from './services/config-reader';
import { ConfigWriter } from './services/config-writer';
import { ConfigDiff } from './services/config-diff';
import { ConfigValidator } from './services/config-validator';
import { MongoClient } from './services/mongo-client';
import { LogReader } from './services/log-reader';
import { HttpClient } from './services/http-client';
import { MapReader } from './services/map-reader';
import { TaskScheduler } from './services/task-scheduler';

const PORT = parseInt(process.env.SERVER_PORT || '3001', 10);
const DATA_DIR = process.env.DATA_DIR || path.resolve(__dirname, '..', '..', 'servercsharp', 'data');
const REPO_ROOT = process.env.REPO_ROOT || path.resolve(__dirname, '..', '..');

const app = express();
app.use(cors());
app.use(express.json());

// Initialize services
const profileManager = new ProfileManager(DATA_DIR);
const serverManager = new ServerManager();
const buildRunner = new BuildRunner(REPO_ROOT);
const configReader = new ConfigReader(DATA_DIR);
const configWriter = new ConfigWriter(DATA_DIR);
const configDiff = new ConfigDiff(DATA_DIR);
const configValidator = new ConfigValidator(DATA_DIR);
const mongoClient = new MongoClient();
const logReader = new LogReader(REPO_ROOT);
const httpClient = new HttpClient();
const mapReader = new MapReader(DATA_DIR);
const taskScheduler = new TaskScheduler(DATA_DIR);

// Make services available to routes
app.locals.services = {
  profileManager,
  serverManager,
  buildRunner,
  configReader,
  configWriter,
  configDiff,
  configValidator,
  mongoClient,
  logReader,
  httpClient,
  mapReader,
  taskScheduler,
};

// Register routes
app.use('/api', createServerRoutes());

// Health check
app.get('/health', (_req, res) => {
  res.json({ status: 'ok', uptime: process.uptime() });
});

app.listen(PORT, () => {
  console.log(`[Admin Backend] Running on http://localhost:${PORT}`);
  console.log(`[Admin Backend] Data dir: ${DATA_DIR}`);
  console.log(`[Admin Backend] Repo root: ${REPO_ROOT}`);
});