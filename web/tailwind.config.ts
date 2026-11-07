import type { Config } from "tailwindcss";

/**
 * The palette is lifted from the app's own cards so the marketing page and the WinUI shell read
 * as the same product: sage Tasks, lavender Focus, mustard Notepad, steel-blue Events.
 */
const config: Config = {
  content: ["./app/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#000000",
        shell: "#08080a",
        cream: "#fff4e6",
        muted: "#a49c92",
        sage: "#8DBA98",
        lavender: "#A39CD0",
        mustard: "#D2CA72",
        steel: "#8CB1CD",
        nectar: "#F79A63"
      },
      fontFamily: {
        display: ['"Bricolage Grotesque"', "system-ui", "sans-serif"],
        sans: ['"Instrument Sans"', "system-ui", "sans-serif"],
        mono: ['"IBM Plex Mono"', "ui-monospace", "monospace"]
      },
      maxWidth: { shell: "1140px" },
      fontSize: {
        hero: ["clamp(2.75rem, 7vw, 6.5rem)", { lineHeight: "0.98", letterSpacing: "-0.045em" }]
      }
    }
  },
  plugins: []
};

export default config;
