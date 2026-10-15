import React, { Component, ErrorInfo, ReactNode } from 'react';
import { AlertOctagon, RotateCcw } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallbackTitle?: string;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    error: null,
    errorInfo: null,
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error, errorInfo: null };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Uncaught error caught by ErrorBoundary:', error, errorInfo);
    this.setState({ error, errorInfo });
  }

  public handleReset = () => {
    this.setState({ hasError: false, error: null, errorInfo: null });
  };

  public render() {
    if (this.state.hasError) {
      return (
        <div className="flex flex-col items-center justify-center min-h-[400px] h-full p-6 text-center font-sans">
          <div className="bg-rose-950/40 border border-rose-500/60 rounded-2xl p-8 max-w-lg w-full shadow-2xl space-y-4 animate-fadeIn">
            <div className="w-14 h-14 bg-rose-500/20 border border-rose-500/40 rounded-full flex items-center justify-center mx-auto text-rose-400">
              <AlertOctagon className="w-8 h-8" />
            </div>

            <h3 className="text-lg font-bold text-white tracking-wide">
              {this.props.fallbackTitle || 'Workspace Error Caught'}
            </h3>

            <p className="text-xs text-slate-300 leading-relaxed font-mono bg-slate-900/80 p-3 rounded-lg border border-slate-800 text-left overflow-x-auto select-all">
              {this.state.error?.message || 'An unexpected rendering exception was prevented.'}
            </p>

            <p className="text-[11px] text-slate-400">
              The application recovered safely without crashing the main diagnostic pipeline.
            </p>

            <div className="pt-2 flex justify-center space-x-3">
              <button
                onClick={this.handleReset}
                className="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-xs font-bold flex items-center space-x-2 transition-all shadow-md cursor-pointer"
              >
                <RotateCcw className="w-4 h-4" />
                <span>Recover Component</span>
              </button>
              <button
                onClick={() => window.location.reload()}
                className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-bold transition-all border border-slate-700 cursor-pointer"
              >
                Reload Web App
              </button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
