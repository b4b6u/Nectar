import type { Config } from "tailwindcss";

/**
 * The palette and type come from the poster-and-paper look: warm cream stock, a deep green ink,
 * one loud orange, and the summer colours the app's own cards already use (sage Tasks, lavender
 * Focus, mustard Notepad, steel-blue Events). Londrina carries the headlines, DM Sans the prose.
 */
const config: Config = {
  content: ["./app/**/*.{ts,tsx}", "./components/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        paper: "#fff2d6",
        paperDeep: "#f7e7c4",
        ink: "#163b36",
        inkSoft: "#2b4f47",
        muted: "#5b6d5c",
        orange: "#f6572d",
        red: "#d93e26",
        leaf: "#b8cd49",
        sun: "#ffcf4a",
        blush: "#f5b6bf",
        sage: "#8DBA98",
        lavender: "#A39CD0",
        mustard: "#D2CA72",
        steel: "#8CB1CD"
      },
      fontFamily: {
        display: ['"Londrina Solid"', '"Arial Black"', "system-ui", "sans-serif"],
        sans: ['"DM Sans"', "system-ui", "sans-serif"],
        mono: ['"IBM Plex Mono"', "ui-monospace", "monospace"]
      },
      maxWidth: { shell: "1180px" },
      fontSize: {
        hero: ["clamp(3.5rem, 11vw, 10rem)", { lineHeight: "0.92", letterSpacing: "0.005em" }],
        scene: ["clamp(2.5rem, 6.5vw, 5.5rem)", { lineHeight: "0.95", letterSpacing: "0.005em" }],
        shout: ["clamp(2rem, 4.5vw, 3.4rem)", { lineHeight: "1", letterSpacing: "0.01em" }]
      },
      boxShadow: {
        sticker: "4px 4px 0 #163b36",
        stickerLg: "7px 7px 0 #163b36",
        lift: "0 26px 60px -32px rgba(22, 59, 54, 0.55)"
      },
      borderRadius: { sticker: "18px" }
    }
  },
  plugins: []
};

export default config;
