import type { Config } from "tailwindcss";

/**
 * The palette and type come from the poster-and-paper look: warm cream stock, a deep green ink, one
 * loud orange, and the summer colours the app's own cards already use.
 *
 * Every colour resolves to a CSS variable triplet, so the whole site can flip to its night palette
 * without a single `dark:` class: `text-ink` and `bg-paper/70` keep working because the variables
 * under them change. `inkSolid` and `paperSolid` are deliberately fixed — a button filled with
 * yellow needs dark ink whether the page around it is cream or midnight.
 */
const config: Config = {
  content: ["./app/**/*.{ts,tsx}", "./components/**/*.{ts,tsx}"],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        paper: "rgb(var(--paper-rgb) / <alpha-value>)",
        paperDeep: "rgb(var(--paper-deep-rgb) / <alpha-value>)",
        ink: "rgb(var(--ink-rgb) / <alpha-value>)",
        inkSoft: "rgb(var(--ink-soft-rgb) / <alpha-value>)",
        muted: "rgb(var(--muted-rgb) / <alpha-value>)",
        orange: "rgb(var(--orange-rgb) / <alpha-value>)",
        red: "rgb(var(--red-rgb) / <alpha-value>)",
        leaf: "rgb(var(--leaf-rgb) / <alpha-value>)",
        sun: "rgb(var(--sun-rgb) / <alpha-value>)",
        blush: "rgb(var(--blush-rgb) / <alpha-value>)",
        sage: "#8DBA98",
        lavender: "#A39CD0",
        mustard: "#D2CA72",
        steel: "#8CB1CD",
        inkSolid: "#163b36",
        paperSolid: "#fff2d6"
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
        sticker: "4px 4px 0 rgb(var(--ink-rgb))",
        stickerLg: "7px 7px 0 rgb(var(--ink-rgb))",
        lift: "0 26px 60px -32px rgb(var(--ink-rgb) / 0.55)"
      },
      borderRadius: { sticker: "18px" }
    }
  },
  plugins: []
};

export default config;
