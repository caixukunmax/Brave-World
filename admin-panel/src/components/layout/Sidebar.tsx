import { NavLink, Stack, Text, ThemeIcon, Divider } from '@mantine/core';
import { useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  IconDashboard,
  IconSettings,
  IconDatabase,
  IconEye,
  IconSwords,
  IconMap,
  IconCode,
  IconUser,
  IconServer,
} from '@tabler/icons-react';

const navItems = [
  { label: 'nav.dashboard', icon: IconDashboard, path: '/' },
  { label: 'nav.config', icon: IconSettings, path: '/config' },
  { label: 'nav.database', icon: IconDatabase, path: '/database' },
  { label: 'nav.monitoring', icon: IconEye, path: '/monitoring' },
  { label: 'nav.gm', icon: IconSwords, path: '/gm' },
  { label: 'nav.maps', icon: IconMap, path: '/maps' },
  { label: 'nav.dev', icon: IconCode, path: '/dev' },
];

const bottomItems = [
  { label: 'nav.profiles', icon: IconServer, path: '/profiles' },
  { label: 'nav.settings', icon: IconUser, path: '/settings' },
];

export function Sidebar() {
  const location = useLocation();
  const navigate = useNavigate();
  const { t } = useTranslation();

  const renderNavItems = (items: typeof navItems) =>
    items.map((item) => (
      <NavLink
        key={item.path}
        label={t(item.label)}
        leftSection={
          <ThemeIcon variant="light" size="sm">
            <item.icon size={16} />
          </ThemeIcon>
        }
        active={location.pathname === item.path}
        onClick={() => navigate(item.path)}
        variant="filled"
        styles={{ root: { borderRadius: 'var(--mantine-radius-sm)' } }}
      />
    ));

  return (
    <Stack h="100%" justify="space-between" gap={0}>
      <Stack gap={0}>
        <Text fw={700} size="lg" p="md" pb="xs">
          {t('app.title')}
        </Text>
        <Divider mb="sm" />
        <Stack gap={4} px={4}>
          {renderNavItems(navItems)}
        </Stack>
      </Stack>
      <Stack gap={4} px={4} pb="xs">
        <Divider mb="xs" />
        {renderNavItems(bottomItems)}
      </Stack>
    </Stack>
  );
}