import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AppShell } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Sidebar } from './components/layout/Sidebar';
import { StatusBar } from './components/layout/StatusBar';
import { DashboardPage } from './pages/Dashboard/DashboardPage';
import { ProfileManagerPage } from './pages/Settings/ProfileManagerPage';
import { SettingsPage } from './pages/Settings/SettingsPage';
import { MapListPage } from './pages/MapTools/MapListPage';

function App() {
  const [opened, { toggle }] = useDisclosure();

  return (
    <BrowserRouter>
      <AppShell
        header={{ height: 0 }}
        navbar={{ width: 260, breakpoint: 'sm', collapsed: { mobile: !opened } }}
        footer={{ height: 32 }}
        padding="md"
      >
        <AppShell.Navbar p="xs">
          <Sidebar />
        </AppShell.Navbar>

        <AppShell.Main>
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/maps" element={<MapListPage />} />
            <Route path="/profiles" element={<ProfileManagerPage />} />
            <Route path="/settings" element={<SettingsPage />} />
          </Routes>
        </AppShell.Main>

        <AppShell.Footer>
          <StatusBar />
        </AppShell.Footer>
      </AppShell>
    </BrowserRouter>
  );
}

export default App;