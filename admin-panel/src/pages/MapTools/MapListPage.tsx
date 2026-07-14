import { useEffect, useState, useCallback } from 'react';
import {
  Stack, Title, Group, Button, Card, Text, SimpleGrid, Badge, Modal,
  Table, ActionIcon, Tooltip, Paper, Divider, Box, Collapse, Loader,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { useTranslation } from 'react-i18next';
import { IconTrash, IconEye, IconMap, IconRefresh, IconAlertTriangle } from '@tabler/icons-react';
import { api } from '../../lib/api';

const TERRAIN_NAMES: Record<string, string> = {
  '0': '普通', '1': '水域', '2': '草地', '3': '沙地', '4': '岩石',
  '5': '雪地', '6': '沼泽', '7': '岩浆', '8': '神圣地', '9': '地形墙', '10': '空气墙',
};

const TERRAIN_COLORS: Record<string, string> = {
  '0': '#c8c8c8', '1': '#4a90d9', '2': '#7ec850', '3': '#e8d44d', '4': '#888888',
  '5': '#e8e8e8', '6': '#6b4c8a', '7': '#e8461e', '8': '#ffd700', '9': '#444444', '10': '#000000',
};

export function MapListPage() {
  const { t } = useTranslation();
  const [maps, setMaps] = useState<any[]>([]);
  const [selectedMap, setSelectedMap] = useState<any>(null);
  const [detail, setDetail] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [deleteOpened, { open: openDelete, close: closeDelete }] = useDisclosure(false);

  const fetchMaps = useCallback(async () => {
    setLoading(true);
    try { setMaps(await api.maps.list()); } catch { /* */ }
    setLoading(false);
  }, []);

  useEffect(() => { fetchMaps(); }, [fetchMaps]);

  const handleViewDetail = async (map: any) => {
    setSelectedMap(map);
    setDetailLoading(true);
    try {
      const d = await api.maps.detail(map.map_name);
      setDetail(d);
    } catch { /* */ }
    setDetailLoading(false);
  };

  const handleDelete = async () => {
    if (!selectedMap) return;
    try {
      await api.maps.delete(selectedMap.map_name);
      closeDelete();
      setSelectedMap(null);
      setDetail(null);
      await fetchMaps();
    } catch (e: any) {
      console.error(e);
    }
  };

  const confirmDelete = (map: any) => {
    setSelectedMap(map);
    openDelete();
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Title order={2}>{t('maps.title')}</Title>
        <Button leftSection={<IconRefresh size={16} />} variant="light" onClick={fetchMaps} loading={loading}>
          {t('common.refresh')}
        </Button>
      </Group>

      {maps.length === 0 ? (
        <Card shadow="sm" padding="lg" radius="md" withBorder>
          <Text c="dimmed" ta="center">{t('common.noData')}</Text>
        </Card>
      ) : (
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          {maps.map((map) => (
            <Card
              key={map.map_name}
              shadow="sm"
              padding="lg"
              radius="md"
              withBorder
              style={{ cursor: 'pointer', borderColor: selectedMap?.map_name === map.map_name ? 'var(--mantine-color-blue-5)' : undefined }}
              onClick={() => handleViewDetail(map)}
            >
              <Group justify="space-between" mb="xs">
                <Group gap="xs">
                  <IconMap size={20} />
                  <Text fw={600}>{map.display_name || map.map_name}</Text>
                </Group>
                <Group gap="xs">
                  <Tooltip label={t('common.view')}>
                    <ActionIcon variant="light" color="blue" onClick={(e) => { e.stopPropagation(); handleViewDetail(map); }}>
                      <IconEye size={16} />
                    </ActionIcon>
                  </Tooltip>
                  <Tooltip label={t('common.delete')}>
                    <ActionIcon variant="light" color="red" onClick={(e) => { e.stopPropagation(); confirmDelete(map); }}>
                      <IconTrash size={16} />
                    </ActionIcon>
                  </Tooltip>
                </Group>
              </Group>
              <Group gap="xs">
                <Badge size="sm" variant="light">{map.width}×{map.height}</Badge>
                <Badge size="sm" variant="light" color="gray">ID: {map.map_name}</Badge>
                <Text size="xs" c="dimmed">({map.spawn_x},{map.spawn_y})</Text>
              </Group>
            </Card>
          ))}
        </SimpleGrid>
      )}

      {/* Detail panel */}
      {selectedMap && (
        <Paper shadow="sm" p="md" radius="md" withBorder>
          <Group justify="space-between" mb="md">
            <Title order={4}>{selectedMap.display_name || selectedMap.map_name}</Title>
            <Button color="red" variant="light" size="xs" leftSection={<IconTrash size={14} />} onClick={() => confirmDelete(selectedMap)}>
              {t('common.delete')}
            </Button>
          </Group>

          {detailLoading ? (
            <Group justify="center" py="xl"><Loader size="sm" /></Group>
          ) : detail ? (
            <Stack gap="md">
              <SimpleGrid cols={{ base: 2, md: 4 }}>
                <Box>
                  <Text size="xs" c="dimmed">{t('maps.width')}</Text>
                  <Text fw={600}>{detail.width}</Text>
                </Box>
                <Box>
                  <Text size="xs" c="dimmed">{t('maps.height')}</Text>
                  <Text fw={600}>{detail.height}</Text>
                </Box>
                <Box>
                  <Text size="xs" c="dimmed">{t('maps.spawnX')}</Text>
                  <Text fw={600}>{detail.spawn_x}</Text>
                </Box>
                <Box>
                  <Text size="xs" c="dimmed">{t('maps.spawnY')}</Text>
                  <Text fw={600}>{detail.spawn_y}</Text>
                </Box>
                <Box>
                  <Text size="xs" c="dimmed">Cells</Text>
                  <Text fw={600}>{detail.cellCount}</Text>
                </Box>
                <Box>
                  <Text size="xs" c="dimmed">File</Text>
                  <Text fw={600}>{detail.fileSizeFormatted}</Text>
                </Box>
              </SimpleGrid>

              <Divider />

              {/* Terrain distribution */}
              <Box>
                <Text size="sm" fw={600} mb="xs">{t('maps.terrainPalette')}</Text>
                <Group gap="xs">
                  {Object.entries(detail.terrainStats || {}).map(([tid, count]) => (
                    <Badge
                      key={tid}
                      size="sm"
                      variant="filled"
                      leftSection={
                        <Box w={10} h={10} style={{ borderRadius: 2, background: TERRAIN_COLORS[tid] || '#666' }} />
                      }
                    >
                      {TERRAIN_NAMES[tid] || tid}: {count as number}
                    </Badge>
                  ))}
                </Group>
              </Box>

              {/* Spawn points */}
              {detail.monsters?.length > 0 && (
                <Box>
                  <Text size="sm" fw={600} mb="xs">{t('maps.spawnPointEditor')} ({detail.monsters.length})</Text>
                  <Table striped highlightOnHover>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>ID</Table.Th>
                        <Table.Th>Monster ID</Table.Th>
                        <Table.Th>X</Table.Th>
                        <Table.Th>Y</Table.Th>
                        <Table.Th>Respawn</Table.Th>
                        <Table.Th>AI</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {detail.monsters.map((m: any) => (
                        <Table.Tr key={m.id}>
                          <Table.Td>{m.id}</Table.Td>
                          <Table.Td>{m.monster_id}</Table.Td>
                          <Table.Td>{m.x}</Table.Td>
                          <Table.Td>{m.y}</Table.Td>
                          <Table.Td>{m.respawn_time}s</Table.Td>
                          <Table.Td>{m.ai_id}</Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Box>
              )}

              {/* NPCs */}
              {detail.npcs?.length > 0 && (
                <Box>
                  <Text size="sm" fw={600} mb="xs">{t('maps.npcPlacement')} ({detail.npcs.length})</Text>
                  <Table striped highlightOnHover>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>ID</Table.Th>
                        <Table.Th>NPC ID</Table.Th>
                        <Table.Th>X</Table.Th>
                        <Table.Th>Y</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {detail.npcs.map((n: any) => (
                        <Table.Tr key={n.id}>
                          <Table.Td>{n.id}</Table.Td>
                          <Table.Td>{n.npc_id}</Table.Td>
                          <Table.Td>{n.x}</Table.Td>
                          <Table.Td>{n.y}</Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Box>
              )}
            </Stack>
          ) : null}
        </Paper>
      )}

      {/* Delete confirmation */}
      <Modal opened={deleteOpened} onClose={closeDelete} title={t('common.confirm')} size="sm">
        <Stack gap="md">
          <Group>
            <IconAlertTriangle size={24} color="var(--mantine-color-red-6)" />
            <Text>
              {t('maps.deleteConfirm')}: <b>{selectedMap?.display_name || selectedMap?.map_name}</b>?
            </Text>
          </Group>
          <Text size="xs" c="dimmed">
            {t('maps.deleteWarning')}
          </Text>
          <Group justify="flex-end">
            <Button variant="outline" onClick={closeDelete}>{t('common.cancel')}</Button>
            <Button color="red" onClick={handleDelete}>{t('common.delete')}</Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}