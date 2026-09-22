import { useEffect, useState } from 'react';
import { Group, Text, Badge } from '@mantine/core';
import { IconCircleFilled, IconServer } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import { api } from '../../lib/api';

export function StatusBar() {
  const { t } = useTranslation();
  const [statuses, setStatuses] = useState<any[]>([]);

  useEffect(() => {
    const fetch = async () => {
      try { setStatuses(await api.server.statuses()); } catch { /* */ }
    };
    fetch();
    const timer = setInterval(fetch, 3000);
    return () => clearInterval(timer);
  }, []);

  const running = statuses.filter(s => s.status === 'running');
  const statusColor = (s: string) => s === 'running' ? 'green' : s === 'error' ? 'red' : 'gray';

  return (
    <Group h="100%" px="md" justify="space-between" gap="xs">
      <Group gap="xs">
        <IconServer size={14} />
        <Text size="xs" c="dimmed">
          {running.length > 0
            ? `${running.length} server(s) running`
            : t('dashboard.stopped')}
        </Text>
        <Badge size="xs" variant="light" color="gray">{t('app.version')}</Badge>
      </Group>
      <Group gap="xs">
        {statuses.map(s => (
          <Group key={s.profileId} gap={4}>
            <IconCircleFilled size={6} color={`var(--mantine-color-${statusColor(s.status)}-6)`} />
            <Text size="xs" c="dimmed">{s.profileId}</Text>
          </Group>
        ))}
      </Group>
    </Group>
  );
}