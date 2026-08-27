/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./index.html", "./src/**/*.{js,jsx}"],
  theme: {
    extend: {
      colors: {
        panel: "#0f172a",
        border: "#1e293b",
      },
    },
  },
  plugins: [],
};
