/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#f0f9ff',
          100: '#e0f2fe',
          500: '#0ea5e9',
          600: '#0284c7',
          700: '#0369a1',
          900: '#0c4a6e',
        },
        audit: {
          green: '#10b981',
          amber: '#f59e0b',
          red: '#ef4444',
          dark: '#0f172a'
        }
      }
    },
  },
  plugins: [],
}
