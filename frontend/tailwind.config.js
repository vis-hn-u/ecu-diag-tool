/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        can: {
          bg: '#0B0F17',
          panel: '#111827',
          card: '#182234',
          border: '#1E293B',
          accent: '#0284C7',
          success: '#10B981',
          warning: '#F59E0B',
          danger: '#EF4444',
          pulse: '#38BDF8'
        }
      },
      fontFamily: {
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'Monaco', 'Consolas', '"Liberation Mono"', '"Courier New"', 'monospace'],
        sans: ['ui-sans-serif', 'system-ui', '-apple-system', 'BlinkMacSystemFont', '"Segoe UI"', 'Roboto', '"Helvetica Neue"', 'Arial', 'sans-serif']
      }
    },
  },
  plugins: [],
}
