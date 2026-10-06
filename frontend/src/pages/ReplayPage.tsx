import React, { useState } from 'react';
import { useCan } from '../context/CanContext';
import { api } from '../services/api';
import { PlaySquare, Play, Pause, Square, Upload, ShieldAlert, FastForward, RotateCcw, FileText } from 'lucide-react';
import { TabType } from '../components/Sidebar';

interface ReplayProps {
  onNavigateToTopology?: () => void;
}

export const ReplayPage: React.FC<ReplayProps> = ({ onNavigateToTopology }) => {
  const { replayStatus } = useCan();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);

  const speedMultipliers = [0.1, 0.5, 1.0, 2.0, 5.0, 10.0];

  const handlePlay = async () => {
    await api.playReplay();
  };

  const handlePause = async () => {
    await api.pauseReplay();
  };

  const handleStop = async () => {
    await api.stopReplay();
  };

  const handleSeek = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const index = parseInt(e.target.value, 10);
    await api.seekReplay(index);
  };

  const handleSetSpeed = async (multiplier: number) => {
    await api.setReplaySpeed(multiplier);
  };

  const handleGenerateSample = async () => {
    await api.generateSampleReplay();
  };

  const handleUploadCsv = async () => {
    if (!selectedFile) return;
    setUploading(true);
    try {
      await api.uploadCsvForReplay(selectedFile);
      setSelectedFile(null);
    } catch {
      // Ignore
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="p-6 space-y-6 overflow-y-auto h-full font-mono select-none">
      {/* Header */}
      <div className="border-b border-can-border pb-4">
        <div className="flex items-center space-x-2 text-white font-bold text-base font-sans">
          <PlaySquare className="w-5 h-5 text-sky-400" />
          <span>CAN REPLAY & SIMULATION PLAYER</span>
        </div>
        <p className="text-xs text-slate-400 font-sans mt-0.5">
          Play back recorded or simulated CAN traffic through the unified backend pipeline without physical CAN transmission.
        </p>
      </div>

      {/* Safety Banner */}
      <div className="bg-amber-500/10 border border-amber-500/30 rounded-lg p-3.5 flex items-center space-x-3 text-xs text-amber-300 font-sans">
        <ShieldAlert className="w-5 h-5 text-amber-400 shrink-0" />
        <span>
          <strong>SIMULATION MODE ONLY:</strong> Replayed frames are ingested exclusively by the internal software processing pipeline for analyzer, signal decoding, graphs, and network topology visualization. Replay frames are never transmitted onto real vehicle CAN buses.
        </span>
      </div>

      {/* Player Controls Deck */}
      <div className="bg-can-panel border border-can-border rounded-lg p-6 shadow-xl space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <span className="text-[10px] text-slate-500 uppercase tracking-wider block">CURRENT LOG SESSION</span>
            <span className="text-base font-bold text-white font-sans">
              {replayStatus?.isLoaded ? replayStatus.name : 'No Replay Session Loaded'}
            </span>
          </div>

          {/* Transport Buttons */}
          <div className="flex items-center space-x-2">
            {!replayStatus?.isPlaying ? (
              <button
                onClick={handlePlay}
                disabled={!replayStatus?.isLoaded}
                className="px-5 py-2 bg-sky-600 hover:bg-sky-500 disabled:opacity-40 text-white rounded font-bold font-sans flex items-center space-x-2 shadow-md transition-all text-xs"
              >
                <Play className="w-4 h-4" />
                <span>Play Simulation</span>
              </button>
            ) : (
              <button
                onClick={handlePause}
                className="px-5 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded font-bold font-sans flex items-center space-x-2 shadow-md transition-all text-xs"
              >
                <Pause className="w-4 h-4" />
                <span>Pause</span>
              </button>
            )}

            <button
              onClick={handleStop}
              disabled={!replayStatus?.isLoaded}
              className="px-4 py-2 bg-slate-800 hover:bg-rose-600/30 disabled:opacity-40 text-rose-300 border border-can-border rounded font-bold font-sans flex items-center space-x-1.5 transition-all text-xs"
            >
              <Square className="w-4 h-4" />
              <span>Stop</span>
            </button>
          </div>
        </div>

        {/* Timeline Scrub Bar */}
        <div className="space-y-2">
          <div className="flex justify-between text-xs text-slate-400">
            <span>
              Frame: <strong className="text-sky-400">{replayStatus?.currentIndex?.toLocaleString() ?? 0}</strong> / {replayStatus?.totalFrames?.toLocaleString() ?? 0}
            </span>
            <span>
              Progress: <strong className="text-white">{replayStatus?.progressPercent ?? 0}%</strong>
            </span>
          </div>

          <input
            type="range"
            min={0}
            max={Math.max((replayStatus?.totalFrames ?? 1) - 1, 0)}
            value={replayStatus?.currentIndex ?? 0}
            onChange={handleSeek}
            disabled={!replayStatus?.isLoaded}
            className="w-full h-2 bg-can-bg rounded-lg appearance-none cursor-pointer accent-sky-500 disabled:opacity-40"
          />
        </div>

        {/* Speed Multipliers */}
        <div className="flex items-center justify-between pt-2 border-t border-can-border text-xs">
          <span className="text-slate-400 font-sans">Playback Speed:</span>
          <div className="flex items-center space-x-1.5">
            {speedMultipliers.map(spd => (
              <button
                key={spd}
                onClick={() => handleSetSpeed(spd)}
                className={`px-3 py-1 rounded font-mono text-xs border transition-all ${
                  replayStatus?.speedMultiplier === spd
                    ? 'bg-sky-600 text-white border-sky-500 font-bold'
                    : 'bg-can-bg text-slate-400 border-can-border hover:text-white'
                }`}
              >
                {spd}x
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Replay Data Sources */}
      <div className="grid grid-cols-2 gap-4">
        {/* Built-in Sample Cycle */}
        <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow space-y-3">
          <h3 className="text-sm font-bold text-white font-sans flex items-center space-x-2">
            <PlaySquare className="w-4 h-4 text-emerald-400" />
            <span>Generate Vehicle Drive Cycle</span>
          </h3>
          <p className="text-xs text-slate-400 font-sans">
            Generates 30 seconds of realistic CAN traffic with dynamic acceleration, steering, braking, wheel speeds, and cluster indicators.
          </p>
          <button
            onClick={handleGenerateSample}
            className="px-4 py-2 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded text-xs font-bold font-sans flex items-center space-x-2"
          >
            <span>Load Driving Cycle Simulation</span>
          </button>
        </div>

        {/* Upload External CSV */}
        <div className="bg-can-panel border border-can-border rounded-lg p-5 shadow space-y-3">
          <h3 className="text-sm font-bold text-white font-sans flex items-center space-x-2">
            <Upload className="w-4 h-4 text-sky-400" />
            <span>Load External CAN CSV Log</span>
          </h3>
          <p className="text-xs text-slate-400 font-sans">
            Supports CSV logs with columns: TimestampUs, IdHex, IdDec, Type, RTR, DLC, DataHex.
          </p>
          <div className="flex items-center space-x-2">
            <input
              type="file"
              accept=".csv"
              id="csv-replay-input"
              className="hidden"
              onChange={e => setSelectedFile(e.target.files?.[0] || null)}
            />
            <label
              htmlFor="csv-replay-input"
              className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-can-border rounded text-xs cursor-pointer font-sans"
            >
              {selectedFile ? selectedFile.name : 'Select CSV File'}
            </label>
            {selectedFile && (
              <button
                onClick={handleUploadCsv}
                disabled={uploading}
                className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded text-xs font-bold font-sans"
              >
                {uploading ? 'Loading...' : 'Upload & Load'}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
