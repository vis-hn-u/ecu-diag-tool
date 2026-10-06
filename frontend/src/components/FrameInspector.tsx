import React from 'react';
import { CanFrame } from '../types';
import { useCan } from '../context/CanContext';
import { Layers, X, Cpu, Radio, Send, ArrowDownRight, ArrowUpRight } from 'lucide-react';

interface FrameInspectorProps {
  frame: CanFrame;
  onClose?: () => void;
}

export const FrameInspector: React.FC<FrameInspectorProps> = ({ frame, onClose }) => {
  const { activeDbc } = useCan();

  const idHex = frame.isExtended
    ? `0x${frame.id.toString(16).toUpperCase().padStart(8, '0')}`
    : `0x${frame.id.toString(16).toUpperCase().padStart(3, '0')}`;

  const matchedMsg = activeDbc?.messages?.find(m => (m.cleanId ?? m.id) === (frame.id & 0x1FFFFFFF));
  const payloadLen = frame.actualLength ?? (frame.isCanFd ? Math.min(64, frame.data?.length || 0) : Math.min(8, frame.dlc));
  const chName = `CAN${(frame.channel ?? 0) + 1}`;

  return (
    <div className="bg-can-card border border-can-border rounded-lg p-4 font-mono text-xs shadow-xl space-y-4 max-h-[calc(100vh-140px)] overflow-y-auto select-none">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-can-border pb-2.5">
        <div className="flex items-center space-x-2">
          <Layers className="w-4 h-4 text-sky-400" />
          <span className="text-sm font-semibold text-white">FRAME INSPECTOR: {idHex}</span>
          {matchedMsg && (
            <span className="text-xs text-emerald-400 font-sans font-medium px-2 py-0.5 bg-emerald-500/10 rounded border border-emerald-500/20">
              {matchedMsg.name} ({matchedMsg.transmitter})
            </span>
          )}
        </div>
        {onClose && (
          <button onClick={onClose} className="text-slate-400 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Frame Attributes Grid */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-2 text-center text-xs">
        {/* Channel */}
        <div className="bg-can-bg p-2 rounded border border-can-border">
          <span className="text-slate-500 text-[10px] block">CHANNEL</span>
          <span className="text-sky-400 font-bold">{chName} (CH {frame.channel ?? 0})</span>
        </div>

        {/* Timestamp */}
        <div className="bg-can-bg p-2 rounded border border-can-border">
          <span className="text-slate-500 text-[10px] block">TIMESTAMP</span>
          <span className="text-slate-200 font-bold">{frame.timestampUs.toLocaleString()} µs</span>
        </div>

        {/* CAN ID (DEC) */}
        <div className="bg-can-bg p-2 rounded border border-can-border">
          <span className="text-slate-500 text-[10px] block">CAN ID (DEC)</span>
          <span className="text-sky-400 font-bold">{frame.id}</span>
        </div>

        {/* ID Type */}
        <div className="bg-can-bg p-2 rounded border border-can-border">
          <span className="text-slate-500 text-[10px] block">ID TYPE</span>
          <span className="text-slate-200 font-bold">{frame.isExtended ? '29-bit EXTENDED' : '11-bit STANDARD'}</span>
        </div>

        {/* Protocol */}
        <div className="bg-can-bg p-2 rounded border border-can-border">
          <span className="text-slate-500 text-[10px] block">PROTOCOL</span>
          <span className={`font-bold ${frame.isCanFd ? 'text-purple-400' : 'text-emerald-400'}`}>
            {frame.isCanFd ? 'CAN FD (Flexible Datarate)' : 'Classic CAN (2.0B)'}
          </span>
        </div>

        {/* DLC */}
        <div className="bg-can-bg p-2 rounded border border-can-border">
          <span className="text-slate-500 text-[10px] block">DLC CODE</span>
          <span className="text-emerald-400 font-bold">DLC {frame.dlc}</span>
        </div>

        {/* Payload Length */}
        <div className="bg-can-bg p-2 rounded border border-can-border">
          <span className="text-slate-500 text-[10px] block">PAYLOAD LENGTH</span>
          <span className="text-slate-200 font-bold">{payloadLen} / {frame.isCanFd ? '64' : '8'} Bytes</span>
        </div>

        {/* Direction */}
        <div className="bg-can-bg p-2 rounded border border-can-border">
          <span className="text-slate-500 text-[10px] block">DIRECTION</span>
          <span className={`font-bold flex items-center justify-center space-x-1 ${
            frame.direction === 1 || frame.direction === 'Tx' ? 'text-amber-400' : 'text-sky-400'
          }`}>
            {frame.direction === 1 || frame.direction === 'Tx' ? (
              <>
                <ArrowUpRight className="w-3 h-3" />
                <span>TX (Transmitted)</span>
              </>
            ) : (
              <>
                <ArrowDownRight className="w-3 h-3" />
                <span>RX (Received)</span>
              </>
            )}
          </span>
        </div>
      </div>

      {/* Protocol Flags Bar */}
      <div className="grid grid-cols-4 gap-2 text-center text-xs">
        <div className={`p-1.5 rounded border ${frame.bitRateSwitch ? 'bg-amber-500/20 text-amber-300 border-amber-500/40 font-bold' : 'bg-can-bg text-slate-500 border-can-border'}`}>
          <span className="text-[10px] block">BRS (Bit Rate Switch)</span>
          <span>{frame.bitRateSwitch ? 'ACTIVE (Fast Data)' : 'INACTIVE'}</span>
        </div>

        <div className={`p-1.5 rounded border ${frame.errorStateIndicator ? 'bg-rose-500/20 text-rose-300 border-rose-500/40 font-bold' : 'bg-can-bg text-slate-500 border-can-border'}`}>
          <span className="text-[10px] block">ESI (Error State)</span>
          <span>{frame.errorStateIndicator ? 'ERROR PASSIVE' : 'ACTIVE / NORMAL'}</span>
        </div>

        <div className={`p-1.5 rounded border ${frame.isRtr ? 'bg-amber-500/20 text-amber-300 border-amber-500/40 font-bold' : 'bg-can-bg text-slate-500 border-can-border'}`}>
          <span className="text-[10px] block">RTR (Remote Frame)</span>
          <span>{frame.isRtr ? 'REMOTE (1)' : 'DATA (0)'}</span>
        </div>

        <div className="p-1.5 rounded bg-can-bg text-slate-300 border border-can-border">
          <span className="text-[10px] text-slate-500 block">SOURCE / HARDWARE</span>
          <span className="truncate block font-bold text-sky-400">{frame.sourceId || 'Default Bus'}</span>
        </div>
      </div>

      {/* Byte Breakdown Table (Supports up to 64 bytes) */}
      <div>
        <div className="text-[11px] text-slate-400 font-semibold mb-1.5 flex items-center justify-between">
          <span>PAYLOAD BYTE BREAKDOWN (UP TO 64 BYTES)</span>
          <span className="text-slate-500 text-[10px]">Length: {payloadLen} Bytes</span>
        </div>
        <div className="overflow-x-auto max-h-56 overflow-y-auto border border-can-border rounded">
          <table className="w-full text-left border-collapse">
            <thead className="sticky top-0 bg-can-panel z-10">
              <tr className="text-slate-400 text-[10px] uppercase border-b border-can-border">
                <th className="p-1.5 border-r border-can-border w-16">Byte</th>
                <th className="p-1.5 border-r border-can-border w-16">Hex</th>
                <th className="p-1.5 border-r border-can-border w-16">Dec</th>
                <th className="p-1.5 border-r border-can-border w-28">Binary</th>
                <th className="p-1.5">ASCII</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-can-border">
              {Array.from({ length: Math.max(8, payloadLen) }).map((_, idx) => {
                const isPresent = idx < payloadLen;
                const val = isPresent && frame.data ? frame.data[idx] : 0;
                const hex = val.toString(16).toUpperCase().padStart(2, '0');
                const bin = val.toString(2).padStart(8, '0');
                const ascii = val >= 32 && val <= 126 ? String.fromCharCode(val) : '.';

                return (
                  <tr key={idx} className={`hover:bg-sky-500/5 ${isPresent ? 'text-slate-200' : 'text-slate-600'}`}>
                    <td className="p-1.5 border-r border-can-border font-semibold">Byte {idx}</td>
                    <td className={`p-1.5 border-r border-can-border font-bold ${isPresent ? 'text-sky-400' : 'text-slate-600'}`}>
                      0x{hex}
                    </td>
                    <td className="p-1.5 border-r border-can-border">{val}</td>
                    <td className="p-1.5 border-r border-can-border tracking-widest text-[11px]">{bin}</td>
                    <td className="p-1.5 text-slate-400">{ascii}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* Payload Binary Visual Matrix */}
      <div>
        <div className="text-[11px] text-slate-400 font-semibold mb-1.5 flex items-center justify-between">
          <span>BINARY PAYLOAD MATRIX</span>
          <span className="text-slate-500 text-[10px]">{Math.min(64, Math.max(8, payloadLen))} bytes displayed</span>
        </div>
        <div className="grid grid-cols-8 gap-1 p-2 bg-can-bg rounded border border-can-border max-h-64 overflow-y-auto">
          {Array.from({ length: Math.min(64, Math.max(8, payloadLen)) }).map((_, byteIdx) => {
            const isPresent = byteIdx < payloadLen;
            const val = isPresent && frame.data ? frame.data[byteIdx] : 0;

            return (
              <div key={byteIdx} className="space-y-0.5">
                <div className="text-[9px] text-slate-500 text-center">B{byteIdx}</div>
                <div className="grid grid-rows-8 gap-0.5">
                  {Array.from({ length: 8 }).map((_, bitIdx) => {
                    const bit = (val >> (7 - bitIdx)) & 1;
                    return (
                      <div
                        key={bitIdx}
                        className={`h-3.5 text-[8px] flex items-center justify-center rounded-sm font-bold ${
                          !isPresent
                            ? 'bg-slate-900 text-slate-700'
                            : bit === 1
                            ? 'bg-sky-600 text-white'
                            : 'bg-slate-800 text-slate-400'
                        }`}
                        title={`Byte ${byteIdx}, Bit ${7 - bitIdx} (BitIndex ${byteIdx * 8 + (7 - bitIdx)}): ${bit}`}
                      >
                        {bit}
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
