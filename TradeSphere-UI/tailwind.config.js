/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        primary: '#3F51B5',
        secondary: '#E91E63',
        background: '#f3f4f6',
        surface: '#ffffff',
      }
    },
  },
  plugins: [],
}
