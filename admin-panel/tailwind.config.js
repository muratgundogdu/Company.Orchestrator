/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: {
          DEFAULT: '#9e0d49',
          primary: '#9e0d49',
          dark: '#7a0a39',
          light: '#b8145a',
          50: '#fdf2f7',
          100: '#fce7f0',
        },
        content: {
          DEFAULT: '#414141',
          inverse: '#ffffff',
        },
      },
      fontFamily: {
        sans: ['"Klavika"', '"Inter"', '"Segoe UI"', 'Arial', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
