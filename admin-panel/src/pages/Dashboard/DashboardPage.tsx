import { useEffect, useState, useCallback, useRef } from 'react';
import {
  Stack, Title, Group, Button, Card, Text, SimpleGrid, Badge, Paper,
  Select, ActionIcon, Tooltip, Divider, Box,
} from '@mantine/core';
import {
  IconPlayerPlay, IconPlayerStop, IconRefresh, IconHammer, IconFlame,
  IconPlus, IconServer, IconCircleFilled, IconRotateClockwise,
} from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { api } from '../../lib/api';

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [profiles, setProfiles] = useState<any[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [statuses, setStatuses] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [outputLines, setOutputLines] = useState<string[]>([]);
  const outputRef = useRef<HTMLDivElement>(null);

  // Load profiles
  const loadProfiles = useCallback(async () => {
    try {
      const list = await api.profiles.list();
      setProfiles(list);
      if (list.length > 0 && !selectedId) {
        setSelectedId(list[0].id);
      }
    } catch { /* */ }
  }, [selectedId]);

  useEffect(() => { loadProfiles(); }, []);

  // Poll all server statuses
  const pollStatuses = useCallback(async () => {
    try {
      const list = await api.server.statuses();
      setStatuses(list);
    } catch { /* */ }
  }, []);

  useEffect(() => {
    pollStatuses();
    const timer = setInterval(pollStatuses, 3000);
    return () => clearInterval(timer);
  }, [pollStatuses]);

  const selectedProfile = profiles.find(p => p.id === selectedId);
  const currentStatus = statuses.find(s => s.profileId === selectedId) || { status: 'stopped', cpu: 0, memory: 0, uptime: 0, pid: null };
  const runningCount = statuses.filter(s => s.status === 'running').length;

  // Poll output when a profile is selected (always, even during build/stop)
  useEffect(() => {
    if (!selectedId) {
      setOutputLines([]);
      return;
    }
    const fetchOutput = async () => {
      try {
        const res = await api.server.output(selectedId);
        setOutputLines(res.lines || []);
      } catch { /* */ }
    };
    fetchOutput();
    const timer = setInterval(fetchOutput, 2000);
    return () => clearInterval(timer);
  }, [selectedId]);

  // Auto-scroll output
  useEffect(() => {
    if (outputRef.current) {
      outputRef.current.scrollTop = outputRef.current.scrollHeight;
    }
  }, [outputLines]);

  const handleStart = async () => {
    if (!selectedId) return;
    setLoading(true);
    try { await api.server.start(selectedId); } catch (e: any) { console.error(e); }
    setLoading(false);
  };

  const handleStop = async () => {
    if (!selectedId) return;
    setLoading(true);
    try { await api.server.stop(selectedId); } catch (e: any) { console.error(e); }
    setLoading(false);
  };

  const handleRestart = async () => {
    if (!selectedId) return;
    setLoading(true);
    try { await api.server.restart(selectedId); } catch (e: any) { console.error(e); }
    setLoading(false);
  };

  const handleReload = async () => {
    if (!selectedId) return;
    try { await api.server.command('reload', selectedId); } catch (e: any) { console.error(e); }
  };

  const handleFullRestart = async () => {
    if (!selectedId) return;
    setLoading(true);
    try {
      await api.build.restart(selectedId);
      pollStatuses();
    } catch (e: any) { console.error(e); }
    setLoading(false);
  };

  const statusColor = (s: string) => s === 'running' ? 'green' : s === 'error' ? 'red' : 'gray';

  // No profiles yet
  if (profiles.length === 0) {
    return (
      <Stack gap="md" align="center" mt={80}>
        <IconServer size={64} stroke={1} style={{ opacity: 0.3 }} />
        <Title order={3} c="dimmed">{t('settings.noProfiles')}</Title>
        <Button leftSection={<IconPlus size={16} />} onClick={() => navigate('/profiles')}>
          {t('settings.addProfile')}
        </Button>
      </Stack>
    );
  }

  return (
    <Stack gap="md">
      {/* Top bar: Profile selector + actions */}
      <Group justify="space-between" wrap="wrap">
        <Group>
          <Title order={2}>{t('dashboard.title')}</Title>
          {runningCount > 0 && (
            <Badge color="green" variant="light" size="lg">{runningCount} running</Badge>
          )}
        </Group>
        <Group>
          <Select
            size="md"
            placeholder={t('settings.profileManager')}
            data={profiles.map(p => ({
              value: p.id,
              label: `${p.name} (${p.id})`,
            }))}
            value={selectedId}
            onChange={(v) => setSelectedId(v)}
            leftSection={<IconServer size={16} />}
            style={{ minWidth: 220 }}
          />
          <Tooltip label={t('settings.addProfile')}>
            <ActionIcon variant="light" size="lg" onClick={() => navigate('/profiles')}>
              <IconPlus size={16} />
            </ActionIcon>
          </Tooltip>
        </Group>
      </Group>

      <Divider />

      {/* Actions for selected profile */}
      <Group>
        <Button
          leftSection={<IconPlayerPlay size={16} />}
          color="green"
          onClick={handleStart}
          loading={loading}
          disabled={!selectedId || currentStatus.status === 'running'}
        >
          {t('dashboard.startServer')}
        </Button>
        <Button
          leftSection={<IconPlayerStop size={16} />}
          color="red"
          onClick={handleStop}
          disabled={!selectedId || currentStatus.status !== 'running'}
        >
          {t('dashboard.stopServer')}
        </Button>
        <Button
          leftSection={<IconRefresh size={16} />}
          variant="light"
          onClick={handleRestart}
          loading={loading}
          disabled={!selectedId || currentStatus.status !== 'running'}
        >
          {t('dashboard.restart')}
        </Button>
        <Button
          leftSection={<IconFlame size={16} />}
          variant="subtle"
          onClick={handleReload}
          disabled={!selectedId || currentStatus.status !== 'running'}
        >
          {t('dashboard.hotReload')}
        </Button>
        <Button
          leftSection={<IconHammer size={16} />}
          variant="outline"
          onClick={async () => { try { await api.build.run('all'); } catch (e) { console.error(e); } }}
        >
          {t('dashboard.buildAll')}
        </Button>
        <Button
          leftSection={<IconRotateClockwise size={16} />}
          color="orange"
          variant="filled"
          onClick={handleFullRestart}
          loading={loading}
          disabled={!selectedId}
        >
          {t('dashboard.fullRestart')}
        </Button>
      </Group>

      {/* Selected profile info */}
      {selectedProfile && (
        <Text size="sm" c="dimmed">
          {t('settings.profileManager')}: <b>{selectedProfile.name}</b> — Gateway :{selectedProfile.gatewayPort} | API :{selectedProfile.httpApiPort} | MongoDB {selectedProfile.mongoHost}:{selectedProfile.mongoPort}/{selectedProfile.mongoDb}
        </Text>
      )}

      {/* Status cards for selected server */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }}>
        <Card shadow="sm" padding="lg" radius="md" withBorder>
          <Text size="xs" c="dimmed" tt="uppercase" fw={700}>{t('dashboard.serverStatus')}</Text>
          <Group gap="xs" mt="xs">
            <IconCircleFilled size={10} color={`var(--mantine-color-${statusColor(currentStatus.status)}-6)`} />
            <Badge color={statusColor(currentStatus.status)} variant="light" size="lg">
              {t(`dashboard.${currentStatus.status}`)}
            </Badge>
            {currentStatus.status === 'running' && (
              <Text size="xs" c="dimmed">{t('dashboard.uptime')}: {Math.floor(currentStatus.uptime / 60)}m</Text>
            )}
          </Group>
          {currentStatus.pid && <Text size="xs" c="dimmed" mt={4}>PID: {currentStatus.pid}</Text>}
        </Card>
        <Card shadow="sm" padding="lg" radius="md" withBorder>
          <Text size="xs" c="dimmed" tt="uppercase" fw={700}>{t('dashboard.cpuUsage')}</Text>
          <Text fz="h2" fw={700} mt="xs">{currentStatus.cpu || '--'}%</Text>
        </Card>
        <Card shadow="sm" padding="lg" radius="md" withBorder>
          <Text size="xs" c="dimmed" tt="uppercase" fw={700}>{t('dashboard.memory')}</Text>
          <Text fz="h2" fw={700} mt="xs">{currentStatus.memory || '--'} MB</Text>
        </Card>
        <Card shadow="sm" padding="lg" radius="md" withBorder>
          <Text size="xs" c="dimmed" tt="uppercase" fw={700}>{t('dashboard.onlinePlayers')}</Text>
          <Text fz="h2" fw={700} mt="xs">--</Text>
        </Card>
      </SimpleGrid>

      {/* All servers overview */}
      {statuses.length > 0 && (
        <Paper shadow="sm" p="md" radius="md" withBorder>
          <Text size="sm" fw={600} mb="xs">{t('statusbar.server')} Overview</Text>
          <SimpleGrid cols={{ base: 2, sm: 3, md: 6 }}>
            {statuses.map(s => (
              <Box key={s.profileId} p="xs" style={{
                border: `1px solid var(--mantine-color-${s.profileId === selectedId ? 'blue' : 'dark'}-4)`,
                borderRadius: 'var(--mantine-radius-sm)',
                cursor: 'pointer',
              }} onClick={() => setSelectedId(s.profileId)}>
                <Group gap={6} mb={2}>
                  <IconCircleFilled size={8} color={`var(--mantine-color-${statusColor(s.status)}-6)`} />
                  <Text size="xs" fw={600}>{s.profileId}</Text>
                </Group>
                <Text size="xs" c="dimmed">{t(`dashboard.${s.status}`)}</Text>
              </Box>
            ))}
          </SimpleGrid>
        </Paper>
      )}

      {/* Server output console */}
      <Paper shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" mb="xs">
          <Text size="sm" fw={600}>
            {t('dashboard.recentEvents')}
            {currentStatus.status === 'running' && (
              <Text span size="xs" c="dimmed" ml="sm">({outputLines.length} lines)</Text>
            )}
          </Text>
          <Text size="xs" c="dimmed">
            {selectedId} — {currentStatus.status === 'running' ? '● Live' : '○ Stopped'}
          </Text>
        </Group>
        <Box
          ref={outputRef}
          style={{
            background: '#0a0a0a',
            borderRadius: 'var(--mantine-radius-sm)',
            padding: '12px',
            maxHeight: 320,
            minHeight: 120,
            overflow: 'auto',
            fontFamily: '"Cascadia Code", "Fira Code", "JetBrains Mono", Consolas, monospace',
            fontSize: 12,
            lineHeight: 1.6,
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-all',
          }}
        >
          {outputLines.length === 0 ? (
            <Text c="dimmed" size="xs" style={{ fontFamily: 'inherit' }}>
              {currentStatus.status === 'running'
                ? 'Waiting for output...'
                : t('dashboard.noEvents')}
            </Text>
          ) : (
            outputLines.map((line, i) => (
              <Text
                key={i}
                span
                style={{
                  display: 'block',
                  color: line.startsWith('[STDERR]') || line.includes('[ERR]') ? '#ff6b6b'
                    : line.includes('[WRN]') ? '#ffd43b'
                    : line.includes('[DBG]') ? '#868e96'
                    : '#ced4da',
                }}
              >
                {line}
              </Text>
            ))
          )}
        </Box>
      </Paper>
    </Stack>
  );
}