import { Stack, Title, Card, Text, Switch, Select } from '@mantine/core';
import { useTranslation } from 'react-i18next';

export function SettingsPage() {
  const { t, i18n } = useTranslation();

  const changeLanguage = (lng: string | null) => {
    if (lng) {
      i18n.changeLanguage(lng);
    }
  };

  return (
    <Stack gap="md">
      <Title order={2}>{t('settings.title')}</Title>

      <Card shadow="sm" padding="lg" radius="md" withBorder>
        <Text fw={600} mb="md">{t('settings.appearance')}</Text>
        <Select
          label={t('settings.theme')}
          data={[
            { value: 'dark', label: t('settings.dark') },
            { value: 'light', label: t('settings.light') },
            { value: 'system', label: t('settings.system') },
          ]}
          defaultValue="dark"
          mb="md"
        />
        <Select
          label={t('settings.language')}
          data={[
            { value: 'zh-CN', label: t('settings.chinese') },
            { value: 'en', label: t('settings.english') },
          ]}
          defaultValue={i18n.language}
          onChange={changeLanguage}
          mb="md"
        />
      </Card>

      <Card shadow="sm" padding="lg" radius="md" withBorder>
        <Text fw={600} mb="md">{t('settings.serverMonitoring')}</Text>
        <Switch
          label={t('settings.autoStartMonitor')}
          defaultChecked={false}
          mb="sm"
        />
        <Switch
          label={t('settings.notifyOnCrash')}
          defaultChecked={true}
          mb="sm"
        />
      </Card>

      <Card shadow="sm" padding="lg" radius="md" withBorder>
        <Text fw={600} mb="md">{t('settings.about')}</Text>
        <Text size="sm" c="dimmed">{t('app.title')} {t('app.version')}</Text>
        <Text size="sm" c="dimmed">{t('settings.aboutText')}</Text>
      </Card>
    </Stack>
  );
}