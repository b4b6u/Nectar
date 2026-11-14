"use client";

import { useCallback, useEffect, useState } from "react";
import Bird from "./forest/Bird";
import { BRANCHES, LEAVES, VINE_LEFT, VINE_RIGHT } from "./forest/art";
import { useFlock } from "./forest/useFlock";

/**
 * The forest.
 *
 * Vines run down both edges of the viewport and six illustrated branches grow in from the sides,
 * swaying from the edge they are rooted in on three different tempos per side. Eight birds live on
 * the perches: they blink, tilt their heads, beat their wings and take turns flying from one branch
 * to a perch on the other side, arcing over the page. Each take-off and landing rustles the branch
 * and knocks leaves loose; hummingbirds trail sparks the whole way. Clicking a bird sends it on.
 *
 * The corner pill carries the flock, the calls, the pause and the theme.
 */

const THEME_KEY = "nectar:theme";

const BRANCH_IDS = {
  left: ["left-top", "left-mid", "left-low"],
  right: ["right-top", "right-mid", "right-low"]
} as const;

const LEAF_COLOURS = Object.keys(LEAVES);

/** Ambient leaves: fourteen of them, each on its own delay, speed, drift and size. */
const FALLING = Array.from({ length: 14 }, (_, i) => ({
  id: i,
  colour: LEAF_COLOURS[i % LEAF_COLOURS.length],
  left: `${(i * 7.3 + (i % 3) * 11) % 96}%`,
  delay: `${-(i * 1.37) % 12}s`,
  duration: `${9 + (i % 5) * 2.4}s`,
  drift: i % 2 === 0 ? 1 : -1,
  size: 16 * (0.7 + ((i * 13) % 10) / 14)
}));

export default function ForestJourney() {
  const { birds, particles, rustling, paused, setPaused, callsOn, setCallsOn, poke, sendFlock } = useFlock();
  const [dark, setDark] = useState(false);

  useEffect(() => {
    setDark(document.documentElement.classList.contains("dark"));
  }, []);

  const toggleTheme = useCallback(() => {
    const next = !document.documentElement.classList.contains("dark");
    document.documentElement.classList.toggle("dark", next);
    setDark(next);
    try {
      window.localStorage.setItem(THEME_KEY, next ? "dark" : "light");
    } catch {
      /* ignore */
    }
  }, []);

  const now = performance.now();

  return (
    <>
      {/* Branches and vines. Decorative: nothing here takes a click. */}
      <div aria-hidden className="pointer-events-none fixed inset-0 z-20 select-none overflow-hidden">
        <svg
          className="branch-shadow absolute bottom-0 left-0 top-0 h-screen w-[9px] sm:w-[14px] lg:w-[18px] xl:w-[24px]"
          dangerouslySetInnerHTML={{ __html: VINE_LEFT }}
          preserveAspectRatio="none"
          viewBox="0 0 36 1000"
        />
        <svg
          className="branch-shadow absolute bottom-0 right-0 top-0 h-screen w-[9px] sm:w-[14px] lg:w-[18px] xl:w-[24px]"
          dangerouslySetInnerHTML={{ __html: VINE_RIGHT }}
          preserveAspectRatio="none"
          viewBox="0 0 36 1000"
        />

        {(["left", "right"] as const).map((side) =>
          BRANCHES[side].map((branch, index) => (
            <div
              key={`${side}-${index}`}
              className={`branch-${side}-${["a", "b", "c"][index]} ${paused ? "branch-paused" : ""} absolute ${
                side === "left" ? "-left-1" : "-right-1"
              } ${branch.width} ${branch.placement}`}
            >
              <div
                className={side === "left" ? "origin-left transition-transform duration-300" : "origin-right transition-transform duration-300"}
                style={rustling === BRANCH_IDS[side][index] ? { animation: "branch-rustle 0.55s ease-out" } : undefined}
              >
                <svg
                  className="branch-shadow h-auto w-full overflow-visible"
                  dangerouslySetInnerHTML={{ __html: branch.art }}
                  viewBox={branch.viewBox}
                />
              </div>
            </div>
          ))
        )}
      </div>

      {/* Leaves, petals, sparks and the birds themselves. */}
      <div className="pointer-events-none fixed inset-0 z-30 select-none overflow-hidden">
        {FALLING.map((leaf) => (
          <span
            aria-hidden
            className="leaf pointer-events-none absolute top-0"
            key={leaf.id}
            style={
              {
                left: leaf.left,
                width: `${leaf.size.toFixed(1)}px`,
                animationDelay: leaf.delay,
                animationDuration: leaf.duration,
                animationPlayState: paused ? "paused" : "running",
                "--drift": `${leaf.drift}`
              } as React.CSSProperties
            }
          >
            <svg
              dangerouslySetInnerHTML={{ __html: LEAVES[leaf.colour] }}
              height="100%"
              viewBox="0 0 20 12"
              width="100%"
            />
          </span>
        ))}

        <div aria-hidden>
          {particles.map((particle) => {
            const life = Math.min(1, (now - particle.bornAt) / particle.lifeMs);
            const opacity = life < 0.15 ? life / 0.15 : Math.max(0, 1 - (life - 0.55) / 0.45);
            return (
              <div
                className="absolute left-0 top-0"
                key={particle.id}
                style={{
                  transform: `translate3d(${particle.x}px, ${particle.y}px, 0) rotate(${particle.rotation}deg)`,
                  opacity,
                  willChange: "transform, opacity"
                }}
              >
                {particle.kind === "sparkle" ? (
                  <svg height={particle.size * 2} viewBox="0 0 12 12" width={particle.size * 2}>
                    <path
                      d="M 6 0 L 7.4 4.6 L 12 6 L 7.4 7.4 L 6 12 L 4.6 7.4 L 0 6 L 4.6 4.6 Z"
                      fill={particle.color}
                      stroke="#163b36"
                      strokeWidth={0.9}
                    />
                  </svg>
                ) : (
                  <svg height={particle.size} viewBox="0 0 20 12" width={particle.size * 1.6}>
                    <path
                      d="M 1 6 C 5 0.5, 14 0.5, 19 6 C 14 11.5, 5 11.5, 1 6 Z"
                      fill={particle.color}
                      stroke="#163b36"
                      strokeWidth={1.6}
                    />
                  </svg>
                )}
              </div>
            );
          })}
        </div>

        {birds.map((bird) => {
          const flying = bird.state === "flying";
          const height = (bird.size * 68) / 80;
          const offsetX = bird.size * 0.5;
          const offsetY = flying ? height * 0.5 : height * 0.8235;
          const blinking = now < bird.blinkUntil;

          return (
            <div
              className="pointer-events-auto absolute left-0 top-0 cursor-pointer"
              key={bird.id}
              onClick={() => poke(bird.id)}
              style={{
                width: bird.size,
                height,
                transform: `translate3d(${bird.x - offsetX}px, ${bird.y - offsetY}px, 0)`,
                willChange: "transform"
              }}
              title={flying ? "Click for a mid-air flutter!" : "Click to send this bird across!"}
            >
              {bird.chirpId > 0 && !flying ? (
                <span
                  className="chirp-note pointer-events-none absolute -top-3 left-1/2 font-display text-[16px] text-orange drop-shadow-[1px_1px_0_#163b36]"
                  key={`${bird.id}-${bird.chirpId}`}
                >
                  ♪
                </span>
              ) : null}
              <div style={{ transform: `scaleX(${bird.facing}) rotate(${bird.pitch}deg)`, transformOrigin: "center center" }}>
                <div className="transition-transform duration-150 hover:scale-110">
                  <Bird
                    blinking={blinking}
                    flying={flying}
                    headTilt={bird.headTilt}
                    size={bird.size}
                    species={bird.species}
                    wingFlap={bird.wingFlap}
                  />
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* The controls. */}
      <div
        aria-label="Forest controls"
        className="fixed bottom-4 right-4 z-40 flex items-center gap-1.5 rounded-full border-2 border-ink bg-paper/95 px-2.5 py-1.5 text-[11px] font-bold uppercase tracking-[0.14em] text-ink shadow-sticker backdrop-blur sm:bottom-6 sm:right-6"
        role="group"
      >
        <button
          className="flex items-center gap-1.5 rounded-full border border-ink bg-sun px-2.5 py-1 text-inkSolid transition-transform hover:-translate-y-0.5 hover:bg-leaf active:translate-y-0"
          onClick={sendFlock}
          title="Send perched birds flying from one side to the other"
          type="button"
        >
          <span aria-hidden>↗</span>
          <span className="hidden sm:inline">Send flock</span>
          <span className="sm:hidden">Flock</span>
        </button>
        <button
          aria-pressed={callsOn}
          className={`flex items-center gap-1 rounded-full border border-ink px-2.5 py-1 transition-colors ${
            callsOn ? "bg-orange text-paperSolid" : "bg-paperDeep text-ink hover:bg-blush"
          }`}
          onClick={() => setCallsOn(!callsOn)}
          title="Toggle gentle bird calls"
          type="button"
        >
          <span aria-hidden>♪</span>
          <span className="hidden sm:inline">{callsOn ? "Calls on" : "Calls off"}</span>
        </button>
        <button
          aria-pressed={paused}
          className="rounded-full border border-ink bg-paper px-2.5 py-1 text-ink transition-colors hover:bg-paperDeep"
          onClick={() => setPaused(!paused)}
          title={paused ? "Resume birds & branch sway" : "Pause birds & branch sway"}
          type="button"
        >
          {paused ? "Play" : "Pause"}
        </button>
        <button
          aria-label={dark ? "Switch to light mode" : "Switch to dark mode"}
          aria-pressed={dark}
          className="flex h-6 w-6 items-center justify-center rounded-full border border-ink bg-paper text-[13px] leading-none transition-colors hover:bg-paperDeep"
          onClick={toggleTheme}
          title={dark ? "Switch to light mode" : "Switch to dark mode"}
          type="button"
        >
          <span aria-hidden>{dark ? "\u2600" : "\u263E"}</span>
        </button>
      </div>
    </>
  );
}
