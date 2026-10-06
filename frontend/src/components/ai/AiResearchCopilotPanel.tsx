import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { CopilotResponse, ResearchDocument, KnowledgeGraphNode, KnowledgeGraphEdge } from '../../types';
import { 
  Sparkles, 
  Send, 
  ShieldCheck, 
  AlertCircle, 
  Search, 
  Layers, 
  FileText, 
  ExternalLink, 
  Cpu, 
  CheckCircle2, 
  Activity,
  Network
} from 'lucide-react';

export const AiResearchCopilotPanel: React.FC = () => {
  const [prompt, setPrompt] = useState('Explain cylinder 1 misfire DTC P0301 with freeze frame data');
  const [domain, setDomain] = useState('DTC');
  const [response, setResponse] = useState<CopilotResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<'copilot' | 'research' | 'graph'>('copilot');

  const [docs, setDocs] = useState<ResearchDocument[]>([]);
  const [nodes, setNodes] = useState<KnowledgeGraphNode[]>([]);
  const [edges, setEdges] = useState<KnowledgeGraphEdge[]>([]);

  useEffect(() => {
    loadResearchData();
  }, []);

  const loadResearchData = async () => {
    try {
      const [d, n, e] = await Promise.all([
        api.getResearchDocuments(),
        api.getKnowledgeGraphNodes(),
        api.getKnowledgeGraphEdges()
      ]);
      setDocs(d);
      setNodes(n);
      setEdges(e);
    } catch { }
  };

  const handleAskCopilot = async () => {
    if (!prompt.trim()) return;
    setLoading(true);
    try {
      const res = await api.queryCopilot({
        domain,
        userPrompt: prompt,
        ecuName: 'ECM'
      });
      setResponse(res);
    } catch { }
    finally { setLoading(false); }
  };

  return (
    <div className="flex flex-col h-full bg-[#0A0E17] text-slate-200 font-mono text-xs overflow-hidden">
      {/* Top Bar */}
      <div className="bg-[#101726] border-b border-slate-800 p-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center space-x-2">
          <div className="p-1.5 bg-indigo-500/10 border border-indigo-500/30 rounded text-indigo-400">
            <Sparkles className="w-4 h-4" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-white tracking-wide">AI ENGINEERING COPILOT & AUTOMOTIVE RESEARCH ENGINE</h2>
            <p className="text-[11px] text-slate-400">Evidence-grounded engineering reasoning, OEM technical documentation & Global Knowledge Graph</p>
          </div>
        </div>

        <div className="flex border border-slate-800 rounded-lg overflow-hidden bg-slate-900/60 p-0.5">
          <button
            onClick={() => setActiveTab('copilot')}
            className={`px-3 py-1 rounded text-xs font-semibold transition-all ${
              activeTab === 'copilot' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-white'
            }`}
          >
            Engineering Copilot
          </button>
          <button
            onClick={() => setActiveTab('research')}
            className={`px-3 py-1 rounded text-xs font-semibold transition-all ${
              activeTab === 'research' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-white'
            }`}
          >
            Research Index ({docs.length})
          </button>
          <button
            onClick={() => setActiveTab('graph')}
            className={`px-3 py-1 rounded text-xs font-semibold transition-all ${
              activeTab === 'graph' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-white'
            }`}
          >
            Knowledge Graph ({nodes.length})
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-4">
        {activeTab === 'copilot' && (
          <div className="max-w-4xl mx-auto space-y-4">
            {/* Prompt Card */}
            <div className="bg-[#101726] border border-slate-800 p-4 rounded-xl shadow-lg space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-white uppercase tracking-wider flex items-center space-x-1.5">
                  <Sparkles className="w-3.5 h-3.5 text-indigo-400" />
                  <span>Ask AI Engineering Copilot</span>
                </span>
                <select
                  value={domain}
                  onChange={e => setDomain(e.target.value)}
                  className="bg-slate-900 border border-slate-800 rounded px-2.5 py-1 text-xs text-slate-300 font-mono focus:outline-none"
                >
                  <option value="DTC">DTC & Root Cause Reasoning</option>
                  <option value="CAN">CAN Reverse Engineering</option>
                  <option value="Flash">Flash & Checksum Architecture</option>
                  <option value="General">General Automotive Diagnostics</option>
                </select>
              </div>

              <div className="flex space-x-2">
                <textarea
                  rows={2}
                  value={prompt}
                  onChange={e => setPrompt(e.target.value)}
                  placeholder="Ask a technical automotive question..."
                  className="flex-1 bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 font-mono resize-none"
                />
                <button
                  onClick={handleAskCopilot}
                  disabled={loading}
                  className="px-4 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg font-bold flex items-center justify-center space-x-1.5 shadow-md shadow-indigo-900/30"
                >
                  {loading ? <Activity className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                  <span>Analyze</span>
                </button>
              </div>
            </div>

            {/* Response View */}
            {response && (
              <div className="bg-[#101726] border border-slate-800 p-5 rounded-xl shadow-lg space-y-4 animate-fadeIn">
                <div className="flex items-center justify-between border-b border-slate-800 pb-3">
                  <div>
                    <span className="text-[10px] text-slate-400 uppercase font-semibold">DOMAIN: {response.domain}</span>
                    <h3 className="text-sm font-bold text-white mt-0.5">{response.conclusion}</h3>
                  </div>
                  <div className="flex items-center space-x-2">
                    <span className="text-[10px] text-slate-500">Confidence:</span>
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-950 text-emerald-300 border border-emerald-800">
                      {response.confidence}
                    </span>
                  </div>
                </div>

                {/* Supporting Evidence */}
                <div>
                  <h4 className="text-xs font-bold text-sky-400 mb-1.5 flex items-center space-x-1">
                    <CheckCircle2 className="w-3.5 h-3.5" />
                    <span>Supporting Empirical Evidence</span>
                  </h4>
                  <ul className="space-y-1 pl-4 list-disc text-slate-300 text-xs">
                    {response.supportingEvidence.map((ev, i) => (
                      <li key={i}>{ev}</li>
                    ))}
                  </ul>
                </div>

                {/* Alternative Explanation */}
                <div className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg">
                  <div className="text-xs font-semibold text-amber-400 mb-1">Alternative Potential Explanation</div>
                  <div className="text-slate-300">{response.alternativeExplanation}</div>
                </div>

                {/* Physical Verification Checklist */}
                <div className="bg-indigo-950/20 border border-indigo-500/30 p-3 rounded-lg">
                  <div className="text-xs font-semibold text-indigo-300 mb-1">Recommended Physical Verification Checklist</div>
                  <pre className="text-[11px] text-slate-300 whitespace-pre-line font-mono">{response.verificationProcedure}</pre>
                </div>

                <div className="text-[10px] text-slate-500 border-t border-slate-800 pt-2 flex justify-between">
                  <span>Provenance: {response.sourceProvenance}</span>
                  <span className="text-amber-500">{response.safetyAdvisory}</span>
                </div>
              </div>
            )}
          </div>
        )}

        {activeTab === 'research' && (
          <div className="space-y-3">
            <h3 className="text-xs font-bold text-white uppercase tracking-wider mb-2">
              Indexed OEM & Semiconductor Technical Documentation
            </h3>
            <div className="grid grid-cols-2 gap-3">
              {docs.map(doc => (
                <div key={doc.id} className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-sky-300 font-bold text-xs">{doc.title}</span>
                    <span className="px-1.5 py-0.5 rounded text-[9px] bg-slate-800 text-slate-400">{doc.authority}</span>
                  </div>
                  <p className="text-[11px] text-slate-300">{doc.contentSummary}</p>
                  <div className="flex items-center justify-between text-[10px] text-slate-500 pt-1 border-t border-slate-800/40">
                    <span>Target: {doc.targetEcuFamily} ({doc.targetVehicle})</span>
                    <span>Source: {doc.sourceName}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {activeTab === 'graph' && (
          <div className="space-y-4">
            <h3 className="text-xs font-bold text-white uppercase tracking-wider mb-2">
              Global Knowledge Graph (OEM → Platform → Engine → ECU → MCU → Bus)
            </h3>
            <div className="grid grid-cols-3 gap-3">
              {nodes.map(node => (
                <div key={node.id} className="bg-slate-900/60 border border-slate-800 p-3 rounded-lg">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-white">{node.label}</span>
                    <span className="px-1.5 py-0.5 rounded text-[9px] bg-indigo-950 text-indigo-300 border border-indigo-800">{node.type}</span>
                  </div>
                  <div className="text-[10px] text-slate-500 mt-1">ID: {node.id}</div>
                  {Object.entries(node.attributes).length > 0 && (
                    <div className="text-[10px] text-slate-400 space-y-0.5 mt-2 border-t border-slate-800/60 pt-1">
                      {Object.entries(node.attributes).map(([k, v]) => (
                        <div key={k} className="flex justify-between">
                          <span>{k}:</span>
                          <span className="text-white font-mono">{v}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
