"use client";

/**
 * The hummingbird and the four songbirds, drawn from the same parts: feet, tail, body, belly and a
 * head group that tilts, plus a wing whose angle is handed in from the flock simulation. A flying
 * bird swaps the perched parts for the two spread wings and a tail that trails flat.
 */

export type Species = "hummingbird" | "finch" | "robin" | "wren" | "warbler";

type Palette = {
  back: string;
  belly: string;
  wingFront: string;
  wingBack: string;
  wingAccent: string;
  tail: string;
  beak: string;
  cheek: string;
  longBeak?: boolean;
  crest?: boolean;
};

export const SPECIES: Record<Species, Palette> = {
  hummingbird: {
    back: "#2e4163",
    belly: "#fff2d6",
    wingFront: "#3a547d",
    wingBack: "#22324d",
    wingAccent: "#98b9d5",
    tail: "#22324d",
    beak: "#163b36",
    cheek: "#f5b6bf",
    longBeak: true
  },
  finch: {
    back: "#ffcf4a",
    belly: "#fff2d6",
    wingFront: "#2b4f47",
    wingBack: "#163b36",
    wingAccent: "#ffcf4a",
    tail: "#163b36",
    beak: "#f6572d",
    cheek: "#f5b6bf",
    crest: true
  },
  robin: {
    back: "#2b4f47",
    belly: "#f6572d",
    wingFront: "#1f423b",
    wingBack: "#163b36",
    wingAccent: "#fff2d6",
    tail: "#163b36",
    beak: "#ffcf4a",
    cheek: "#ffcf4a"
  },
  wren: {
    back: "#9f94cc",
    belly: "#f5b6bf",
    wingFront: "#c9c2ea",
    wingBack: "#7b70a8",
    wingAccent: "#fff2d6",
    tail: "#7b70a8",
    beak: "#f6572d",
    cheek: "#ffcf4a"
  },
  warbler: {
    back: "#8fc19a",
    belly: "#fff2d6",
    wingFront: "#b8cd49",
    wingBack: "#3d7058",
    wingAccent: "#ffcf4a",
    tail: "#3d7058",
    beak: "#f6572d",
    cheek: "#f5b6bf",
    crest: true
  }
};

const INK = "#163b36";

type BirdProps = {
  species: Species;
  flying: boolean;
  wingFlap: number;
  headTilt?: number;
  blinking?: boolean;
  size?: number;
};

export default function Bird({ species, flying, wingFlap, headTilt = 0, blinking = false, size = 64 }: BirdProps) {
  const palette = SPECIES[species];
  const humming = species === "hummingbird";
  const wingAngle = flying ? wingFlap * (humming ? 58 : 48) - 18 : 0;
  const nearWingAngle = flying ? wingFlap * (humming ? 50 : 42) - 32 : 0;
  const wingSquash = flying ? 0.65 + Math.abs(wingFlap) * 0.45 : 1;

  return (
    <svg
      className="overflow-visible drop-shadow-[0_2px_0_rgba(22,59,54,0.22)]"
      height={(size * 68) / 80}
      viewBox="0 0 80 68"
      width={size}
    >
      {flying ? (
        <g>
          <g transform={`translate(39, 31) rotate(${wingAngle}) scale(1, ${wingSquash})`}>
            <path
              d={
                humming
                  ? "M 0 0 C -10 -16, -24 -26, -32 -22 C -26 -10, -14 -2, 0 3 Z"
                  : "M 0 0 C -8 -18, -22 -27, -33 -20 C -26 -8, -14 0, 0 4 Z"
              }
              fill={palette.wingBack}
              stroke={INK}
              strokeLinejoin="round"
              strokeWidth={2.2}
            />
          </g>
          <path
            d={humming ? "M 23 36 L 6 31 L 11 38 L 5 43 L 24 41 Z" : "M 24 35 L 5 30 L 10 37 L 6 43 L 25 41 Z"}
            fill={palette.tail}
            stroke={INK}
            strokeLinejoin="round"
            strokeWidth={2.2}
          />
          <path d="M 35 45 L 30 49 M 40 45 L 35 49" stroke={INK} strokeLinecap="round" strokeWidth={1.8} />
          <path
            d="M 22 36 C 24 24, 37 20, 49 23 C 56 24.5, 60 28, 60 33 C 60 41, 50 47, 38 46.5 C 29 46, 22 42, 22 36 Z"
            fill={palette.back}
            stroke={INK}
            strokeLinejoin="round"
            strokeWidth={2.4}
          />
          <path
            d="M 30 39 C 34 32, 46 30, 56 32 C 58 37, 50 46, 38 45.5 C 33 45.2, 30 42.5, 30 39 Z"
            fill={palette.belly}
          />
          {palette.crest ? (
            <path
              d="M 48 22 C 47 16, 52 15, 55 20 C 56 17, 59 18, 57 24"
              fill={palette.back}
              stroke={INK}
              strokeLinejoin="round"
              strokeWidth={2.1}
            />
          ) : null}
          {palette.longBeak ? (
            <path
              d="M 59 29.5 L 77 31.2 L 59 33.4 Z"
              fill={palette.beak}
              stroke={INK}
              strokeLinejoin="round"
              strokeWidth={1.8}
            />
          ) : (
            <path d="M 58 28.5 L 68 31.8 L 58 35 Z" fill={palette.beak} stroke={INK} strokeLinejoin="round" strokeWidth={2} />
          )}
          <ellipse cx={53.5} cy={34.8} fill={palette.cheek} opacity={0.9} rx={3.1} ry={2.1} />
          {blinking ? (
            <path d="M 51 30 Q 53.5 31.8, 56 30" fill="none" stroke={INK} strokeLinecap="round" strokeWidth={2} />
          ) : (
            <g>
              <circle cx={53.5} cy={29.6} fill={INK} r={2.8} />
              <circle cx={54.4} cy={28.7} fill="#fff2d6" r={1.05} />
            </g>
          )}
          <g transform={`translate(40, 34) rotate(${nearWingAngle}) scale(1, ${wingSquash})`}>
            <path
              d={
                humming
                  ? "M 2 0 C -6 -20, -24 -30, -34 -21 C -30 -10, -20 -4, -14 -1 C -19 2, -22 6, -15 7 C -8 8, -2 5, 2 3 Z"
                  : "M 3 0 C -4 -19, -20 -29, -32 -19 C -27 -8, -19 -2, -12 0 C -17 3, -18 8, -10 8 C -4 8, 1 5, 3 2 Z"
              }
              fill={palette.wingFront}
              stroke={INK}
              strokeLinejoin="round"
              strokeWidth={2.3}
            />
            <path
              d="M -6 -5 C -13 -12, -21 -16, -26 -15"
              fill="none"
              opacity={0.9}
              stroke={palette.wingAccent}
              strokeLinecap="round"
              strokeWidth={2.3}
            />
          </g>
        </g>
      ) : (
        <g>
          <g stroke={INK} strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.1}>
            <path d="M 37 48 L 36 55 M 33 56 L 39 56" />
            <path d="M 44 48 L 44 55 M 41 56 L 47 56" />
          </g>
          <path
            d={species === "wren" ? "M 25 40 L 9 25 L 14 34 L 11 38 L 26 45 Z" : "M 26 42 L 10 51 L 16 53 L 14 57 L 29 48 Z"}
            fill={palette.tail}
            stroke={INK}
            strokeLinejoin="round"
            strokeWidth={2.2}
          />
          <path
            d="M 24 41 C 23 28, 32 18, 44 18 C 54 18, 60 25, 59 35 C 58 45, 49 51, 38 50.5 C 29 50, 24.5 46.5, 24 41 Z"
            fill={palette.back}
            stroke={INK}
            strokeLinejoin="round"
            strokeWidth={2.4}
          />
          <path
            d="M 35 49.5 C 46 50.5, 56 44, 57.5 34 C 58 29, 55 25, 49 25.5 C 43 26, 39 32, 36 39 C 34.5 43, 34 47.5, 35 49.5 Z"
            fill={palette.belly}
          />
          <g transform={`rotate(${headTilt}, 48, 28)`}>
            {palette.crest ? (
              <path
                d="M 43 18 C 42 12, 47 11, 50 16 C 52 13, 55 15, 53 20"
                fill={palette.back}
                stroke={INK}
                strokeLinejoin="round"
                strokeWidth={2.1}
              />
            ) : null}
            {palette.longBeak ? (
              <path
                d="M 57.5 25.5 L 75 24.5 L 58 29.2 Z"
                fill={palette.beak}
                stroke={INK}
                strokeLinejoin="round"
                strokeWidth={1.8}
              />
            ) : (
              <path d="M 57 25 L 66.5 27.5 L 57.2 31 Z" fill={palette.beak} stroke={INK} strokeLinejoin="round" strokeWidth={2} />
            )}
            <ellipse cx={51.5} cy={31.2} fill={palette.cheek} opacity={0.92} rx={3.2} ry={2.2} />
            {blinking ? (
              <path d="M 49 26.5 Q 51.5 28.4, 54 26.5" fill="none" stroke={INK} strokeLinecap="round" strokeWidth={2} />
            ) : (
              <g>
                <circle cx={51.5} cy={26.2} fill={INK} r={2.9} />
                <circle cx={52.5} cy={25.2} fill="#fff2d6" r={1.1} />
              </g>
            )}
          </g>
          <g transform={`translate(34, 34) rotate(${wingFlap * 7})`}>
            <path
              d="M 10 -1 C 3 -4, -9 -1, -13 8 C -8 13, 3 13, 11 7 C 13 5, 13 1, 10 -1 Z"
              fill={palette.wingFront}
              stroke={INK}
              strokeLinejoin="round"
              strokeWidth={2.3}
            />
            <path
              d="M 5 3 C 0 3, -5 5, -9 7"
              fill="none"
              opacity={0.9}
              stroke={palette.wingAccent}
              strokeLinecap="round"
              strokeWidth={2.1}
            />
          </g>
        </g>
      )}
    </svg>
  );
}
