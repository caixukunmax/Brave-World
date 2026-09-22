import { useEffect, useState, useCallback } from 'react';
import {
  Stack, Title, Button, Table, Text, Group, Modal, TextInput, NumberInput, Switch, ActionIcon, Card,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { useTranslation } from 'react-i18next';
import { IconPlus, IconTrash, IconEdit, IconCheck } from '@tabler/icons-react';
import { api } from '../../lib/api';

interface Profile {
  id: string;
  name: string;
  repoRoot: string;
  serverExePath: string;
  mongoHost: string;
  mongoPort: number;
  mongoDb: string;
  httpApiPort: number;
  gatewayPort: number;
  autoRestart: boolean;
}

const emptyProfile: Profile = {
  id: '', name: '', repoRoot: 'C:\\code\\codeBrave-World-node2',
  serverExePath: 'servercsharp\\src\\GameServer\\bin\\Debug\\net8.0\\GameServer.exe',
  mongoHost: '127.0.0.1', mongoPort: 27017, mongoDb: 'tslua2',
  httpApiPort: 8890, gatewayPort: 8889, autoRestart: false,
};

export function ProfileManagerPage() {
  const { t } = useTranslation();
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [editing, setEditing] = useState<Profile>(emptyProfile);
  const [opened, { open, close }] = useDisclosure(false);
  const [loading, setLoading] = useState(false);

  const fetchProfiles = useCallback(async () => {
    try {
      const data = await api.profiles.list();
      setProfiles(data);
    } catch { /* */ }
  }, []);

  useEffect(() => { fetchProfiles(); }, [fetchProfiles]);

  const handleSave = async (profile: Profile) => {
    setLoading(true);
    try {
      if (profiles.find(p => p.id === profile.id)) {
        await api.profiles.update(profile.id, profile);
      } else {
        await api.profiles.create(profile);
      }
      await fetchProfiles();
      close();
    } catch (e: any) {
      console.error(e);
    }
    setLoading(false);
  };

  const handleDelete = async (id: string) => {
    try {
      await api.profiles.delete(id);
      await fetchProfiles();
    } catch (e: any) {
      console.error(e);
    }
  };

  const handleEdit = (profile: Profile) => {
    setEditing({ ...profile });
    open();
  };

  const handleNew = () => {
    setEditing({ ...emptyProfile });
    open();
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Title order={2}>{t('settings.profileManager')}</Title>
        <Button leftSection={<IconPlus size={16} />} onClick={handleNew}>
          {t('settings.addProfile')}
        </Button>
      </Group>

      {profiles.length === 0 ? (
        <Card shadow="sm" padding="lg" radius="md" withBorder>
          <Text c="dimmed" ta="center">{t('settings.noProfiles')}</Text>
        </Card>
      ) : (
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>{t('config.name')}</Table.Th>
              <Table.Th>MongoDB</Table.Th>
              <Table.Th>{t('settings.gatewayPort')}</Table.Th>
              <Table.Th>{t('settings.httpApiPort')}</Table.Th>
              <Table.Th>{t('settings.autoRestart')}</Table.Th>
              <Table.Th>{t('config.actions')}</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {profiles.map((p) => (
              <Table.Tr key={p.id}>
                <Table.Td><Text fw={500}>{p.name}</Text><Text size="xs" c="dimmed">{p.id}</Text></Table.Td>
                <Table.Td>{p.mongoHost}:{p.mongoPort}/{p.mongoDb}</Table.Td>
                <Table.Td>:{p.gatewayPort}</Table.Td>
                <Table.Td>:{p.httpApiPort}</Table.Td>
                <Table.Td>{p.autoRestart ? <IconCheck size={16} color="green" /> : '—'}</Table.Td>
                <Table.Td>
                  <Group gap="xs">
                    <ActionIcon variant="light" color="blue" onClick={() => handleEdit(p)}><IconEdit size={16} /></ActionIcon>
                    <ActionIcon variant="light" color="red" onClick={() => handleDelete(p.id)}><IconTrash size={16} /></ActionIcon>
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}

      <Modal opened={opened} onClose={close} title={editing.id ? t('settings.editProfile') : t('settings.newProfile')} size="lg">
        <Stack gap="md">
          <TextInput label={t('settings.profileId')} value={editing.id}
            onChange={(e) => setEditing({ ...editing, id: e.target.value })} disabled={!!editing.id} />
          <TextInput label={t('settings.displayName')} value={editing.name}
            onChange={(e) => setEditing({ ...editing, name: e.target.value })} />
          <TextInput label={t('settings.repoRoot')} value={editing.repoRoot}
            onChange={(e) => setEditing({ ...editing, repoRoot: e.target.value })} />
          <TextInput label={t('settings.serverExePath')} value={editing.serverExePath}
            onChange={(e) => setEditing({ ...editing, serverExePath: e.target.value })} />
          <TextInput label={t('settings.mongoHost')} value={editing.mongoHost}
            onChange={(e) => setEditing({ ...editing, mongoHost: e.target.value })} />
          <NumberInput label={t('settings.mongoPort')} value={editing.mongoPort}
            onChange={(v) => setEditing({ ...editing, mongoPort: Number(v) })} />
          <TextInput label={t('settings.mongoDb')} value={editing.mongoDb}
            onChange={(e) => setEditing({ ...editing, mongoDb: e.target.value })} />
          <NumberInput label={t('settings.httpApiPort')} value={editing.httpApiPort}
            onChange={(v) => setEditing({ ...editing, httpApiPort: Number(v) })} />
          <NumberInput label={t('settings.gatewayPort')} value={editing.gatewayPort}
            onChange={(v) => setEditing({ ...editing, gatewayPort: Number(v) })} />
          <Switch label={t('settings.autoRestart')} checked={editing.autoRestart}
            onChange={(e) => setEditing({ ...editing, autoRestart: e.currentTarget.checked })} />
          <Group justify="flex-end" mt="md">
            <Button variant="outline" onClick={close}>{t('common.cancel')}</Button>
            <Button onClick={() => handleSave(editing)} loading={loading}>{t('common.save')}</Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}