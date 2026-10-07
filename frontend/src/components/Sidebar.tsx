import React from 'react';
import { 
  LayoutDashboard, 
  Binary, 
  Gauge, 
  Network, 
  FileCode, 
  Disc, 
  PlaySquare, 
  LineChart, 
  Lock,
  Wrench,
  Cpu,
  Flame,
  Sliders,
  Activity,
  Layers,
  HardDrive,
  FileText
} from 'lucide-react';

export type TabType = 
  | 'dashboard' 
  | 'hardware'
  | 'analyzer' 
  | 'livedata' 
  | 'live-ecu-data'
  | 'topology' 
  | 'dbc' 
  | 'logger' 
  | 'replay' 
  | 'graphs' 
  | 'diagnostics' 
  | 'engine' 
  | 'dyno' 
  | 'calibration'
  | 'flash-analyzer'
  | 'mappacks'
  | 'ecu-engineering'
  | 'savvycan'
  | 'reports';

interface SidebarProps {
  activeTab: TabType;
  onSelectTab: (tab: TabType) => void;
}

export const Sidebar: React.FC<SidebarProps> = ({ activeTab, onSelectTab }) => {
  const navItems = [
    { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
    { id: 'ecu-engineering', label: 'ECU Engineering', icon: Cpu },
    { id: 'reports', label: 'PDF Reports Center', icon: FileText },
    { id: 'hardware', label: 'Hardware Manager', icon: HardDrive },
    { id: 'savvycan', label: 'SavvyCAN Studio', icon: Binary },
    { id: 'analyzer', label: 'CAN Analyzer', icon: Binary },
    { id: 'live-ecu-data', label: 'Live ECU Data', icon: Activity },
    { id: 'livedata', label: 'DBC Signals', icon: Gauge },
    { id: 'topology', label: 'Topology', icon: Network },
    { id: 'dbc', label: 'DBC Manager', icon: FileCode },
    { id: 'logger', label: 'CAN Logger', icon: Disc },
    { id: 'replay', label: 'CAN Replay', icon: PlaySquare },
    { id: 'graphs', label: 'Signal Graphs', icon: LineChart },
    { id: 'diagnostics', label: 'Diagnostics & UDS', icon: Wrench },
    { id: 'engine', label: 'Engine Controls', icon: Flame },
    { id: 'dyno', label: 'Dyno Analysis', icon: Activity },
    { id: 'calibration', label: 'ECU Calibration', icon: Sliders },
    { id: 'flash-analyzer', label: 'ECU Flash Analyzer', icon: Cpu },
    { id: 'mappacks', label: 'ECU Mappacks (2D/3D)', icon: Layers },
  ];

  return (
    <aside className="w-56 bg-can-panel border-r border-can-border flex flex-col justify-between select-none">
      <div className="p-3 space-y-1 overflow-y-auto">
        <div className="text-[10px] font-mono uppercase text-slate-500 font-semibold px-3 py-1.5 tracking-wider">
          Engineering Tools
        </div>
        {navItems.map(item => {
          const Icon = item.icon;
          const isActive = activeTab === item.id;
          return (
            <button
              key={item.id}
              onClick={() => onSelectTab(item.id as TabType)}
              className={`w-full flex items-center space-x-3 px-3 py-2 rounded text-xs font-medium transition-all ${
                isActive
                  ? 'bg-sky-600/20 text-sky-400 border border-sky-500/30 shadow-sm font-semibold'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-can-card border border-transparent'
              }`}
            >
              <Icon className={`w-4 h-4 ${isActive ? 'text-sky-400' : 'text-slate-400'}`} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </div>

      <div className="p-3 border-t border-can-border text-[11px] font-mono text-slate-500 flex justify-between items-center">
        <span>v5.0.0 (Phase 5)</span>
        <span className="w-2 h-2 rounded-full bg-emerald-500" title="Live ECU Data & Flash Analyzer Ready" />
      </div>
    </aside>
  );
};
