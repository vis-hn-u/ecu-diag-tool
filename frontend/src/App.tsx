import React, { useState } from 'react';
import { CanProvider } from './context/CanContext';
import { Header } from './components/Header';
import { Sidebar, TabType } from './components/Sidebar';
import { DataFlowMonitor } from './components/DataFlowMonitor';
import { DashboardPage } from './pages/DashboardPage';
import { CanAnalyzerPage } from './pages/CanAnalyzerPage';
import { LiveDataPage } from './pages/LiveDataPage';
import { LiveEcuDataPage } from './pages/LiveEcuDataPage';
import { TopologyPage } from './pages/TopologyPage';
import { DbcManagerPage } from './pages/DbcManagerPage';
import { LoggerPage } from './pages/LoggerPage';
import { ReplayPage } from './pages/ReplayPage';
import { GraphsPage } from './pages/GraphsPage';
import { DiagnosticsWorkspacePage } from './pages/DiagnosticsWorkspacePage';
import { EnginePage } from './pages/EnginePage';
import { DynoPage } from './pages/DynoPage';
import { CalibrationPage } from './pages/CalibrationPage';
import { FlashAnalyzerPage } from './pages/FlashAnalyzerPage';
import { HardwareManagerPage } from './pages/HardwareManagerPage';
import { MappackViewerPage } from './pages/MappackViewerPage';
import { ReportsPage } from './pages/ReportsPage';
import { EcuEngineeringPage } from './pages/EcuEngineeringPage';
import { SavvyCanAnalysisPage } from './pages/SavvyCanAnalysisPage';

import { ErrorBoundary } from './components/ErrorBoundary';

export const AppContent: React.FC = () => {
  const [activeTab, setActiveTab] = useState<TabType>('dashboard');
  const [graphSignal, setGraphSignal] = useState<string | undefined>(undefined);

  const handleNavigateToGraphs = (sigName: string) => {
    setGraphSignal(sigName);
    setActiveTab('graphs');
  };

  return (
    <div className="flex flex-col h-screen w-screen bg-[#0B0F17] text-slate-100 overflow-hidden font-sans">
      <Header onNavigateToHardware={() => setActiveTab('hardware')} />
      <div className="px-4 pt-2 bg-[#0B0F17]">
        <DataFlowMonitor />
      </div>
      <div className="flex flex-1 overflow-hidden">
        <Sidebar activeTab={activeTab} onSelectTab={setActiveTab} />
        <main className="flex-1 overflow-hidden bg-[#0A0E17]">
          <ErrorBoundary fallbackTitle={`${activeTab.toUpperCase()} Workspace Exception`}>
            {activeTab === 'dashboard' && <DashboardPage onNavigate={setActiveTab} />}
            {activeTab === 'ecu-engineering' && <EcuEngineeringPage />}
            {activeTab === 'reports' && <ReportsPage />}
            {activeTab === 'hardware' && <HardwareManagerPage />}
            {activeTab === 'savvycan' && <SavvyCanAnalysisPage />}
            {activeTab === 'analyzer' && <CanAnalyzerPage />}
            {activeTab === 'live-ecu-data' && <LiveEcuDataPage />}
            {activeTab === 'livedata' && <LiveDataPage onNavigateToGraphs={handleNavigateToGraphs} />}
            {activeTab === 'topology' && <TopologyPage />}
            {activeTab === 'dbc' && <DbcManagerPage />}
            {activeTab === 'logger' && <LoggerPage onNavigateToReplay={() => setActiveTab('replay')} />}
            {activeTab === 'replay' && <ReplayPage onNavigateToTopology={() => setActiveTab('topology')} />}
            {activeTab === 'graphs' && <GraphsPage initialSignal={graphSignal} />}
            {activeTab === 'diagnostics' && <DiagnosticsWorkspacePage />}
            {activeTab === 'engine' && <EnginePage />}
            {activeTab === 'dyno' && <DynoPage />}
            {activeTab === 'calibration' && <CalibrationPage />}
            {activeTab === 'flash-analyzer' && <FlashAnalyzerPage />}
            {activeTab === 'mappacks' && <MappackViewerPage />}
          </ErrorBoundary>
        </main>
      </div>
    </div>
  );
};

export const App: React.FC = () => {
  return (
    <CanProvider>
      <AppContent />
    </CanProvider>
  );
};

export default App;
