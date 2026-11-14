"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { BIRDS, BRANCHES, LEAVES, VINE_LEFT, VINE_RIGHT } from "./forest/art";

/**
 * The forest: vines down both edges of the viewport, six illustrated branches that grow in from the
 * sides and sway from the edge they are rooted in, hummingbirds perched along them, and leaves that
 * drift down past everything.
 *
 * A seventh bird works the whole document: it leaves the left branch at the top of the page and
 * lands on the right one at the bottom. That one is a scroll timeline (`animation-timeline:
 * scroll(root block)`), so its position is pinned to the reader rather than eased towards them;
 * browsers without scroll timelines get the same path driven from a rAF-throttled scroll listener.
 *
 * "Send flock" hops the perched birds off their branches and sends them across the page. "Calls"
 * toggles a synthesised forest — filtered noise for leaves plus scheduled chirps — so the site
 * ships no audio assets and nothing is fetched. "Pause" holds every wing, branch and leaf. "Theme"
 * flips the palette between cream paper and its night version.
 *
 * None of it autoplays: sound starts on a click, and the choice is remembered.
 */

const SOUND_KEY = "nectar:forest-sound";
const THEME_KEY = "nectar:theme";

type Sound = { stop: () => void; chirp: () => void };

function startForestSound(): Sound {
  const AudioCtor =
    window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AudioCtor) {
    throw new Error("Web Audio is not available in this browser.");
  }

  const ctx = new AudioCtor();
  // Safari and Chrome can hand back a suspended context even inside a click; resuming is cheap and
  // harmless when the context is already running.
  void ctx.resume();

  const master = ctx.createGain();
  master.gain.value = 0;
  master.connect(ctx.destination);

  // Leaves and wind: four seconds of noise, band-limited and looped, breathing under a slow LFO.
  const seconds = 4;
  const buffer = ctx.createBuffer(1, ctx.sampleRate * seconds, ctx.sampleRate);
  const data = buffer.getChannelData(0);
  let last = 0;
  for (let i = 0; i < data.length; i += 1) {
    const white = Math.random() * 2 - 1;
    // A little brown-noise integration takes the hiss off and leaves a soft rustle.
    last = (last + 0.02 * white) / 1.02;
    data[i] = last * 3.2;
  }

  const noise = ctx.createBufferSource();
  noise.buffer = buffer;
  noise.loop = true;

  const band = ctx.createBiquadFilter();
  band.type = "bandpass";
  band.frequency.value = 1400;
  band.Q.value = 0.45;

  const gusts = ctx.createGain();
  gusts.gain.value = 0.5;

  const lfo = ctx.createOscillator();
  const lfoDepth = ctx.createGain();
  lfo.frequency.value = 0.08;
  lfoDepth.gain.value = 0.28;
  lfo.connect(lfoDepth).connect(gusts.gain);

  noise.connect(band).connect(gusts).connect(master);
  noise.start();
  lfo.start();

  // A chirp: a short pitch arc, panned somewhere in the branches.
  const chirp = () => {
    const now = ctx.currentTime;
    const osc = ctx.createOscillator();
    const env = ctx.createGain();
    const pan = ctx.createStereoPanner();
    const base = 2400 + Math.random() * 1800;
    const length = 0.09 + Math.random() * 0.11;

    osc.type = "sine";
    osc.frequency.setValueAtTime(base, now);
    osc.frequency.exponentialRampToValueAtTime(base * (1.25 + Math.random() * 0.4), now + length * 0.45);
    osc.frequency.exponentialRampToValueAtTime(base * 0.85, now + length);

    env.gain.setValueAtTime(0.0001, now);
    env.gain.exponentialRampToValueAtTime(0.16, now + 0.015);
    env.gain.exponentialRampToValueAtTime(0.0001, now + length);

    pan.pan.value = Math.random() * 1.6 - 0.8;
    osc.connect(env).connect(pan).connect(master);
    osc.start(now);
    osc.stop(now + length + 0.02);
  };

  let chirpTimer = 0;
  const schedule = () => {
    chirp();
    chirpTimer = window.setTimeout(schedule, 1200 + Math.random() * 3600);
  };
  chirpTimer = window.setTimeout(schedule, 700);

  master.gain.setValueAtTime(0.0001, ctx.currentTime);
  master.gain.exponentialRampToValueAtTime(0.5, ctx.currentTime + 2.4);

  return {
    chirp,
    stop: () => {
      window.clearTimeout(chirpTimer);
      master.gain.cancelScheduledValues(ctx.currentTime);
      master.gain.setValueAtTime(master.gain.value, ctx.currentTime);
      master.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.6);
      window.setTimeout(() => {
        void ctx.close();
      }, 900);
    }
  };
}

const LEAF_COLOURS = Object.keys(LEAVES);

/** Leaves drift down the viewport on their own clocks, each with a little side-to-side drift. */
const FALLING = Array.from({ length: 14 }, (_, i) => {
  const colour = LEAF_COLOURS[i % LEAF_COLOURS.length];
  return {
    id: i,
    colour,
    left: `${(i * 7.3 + (i % 3) * 11) % 96}%`,
    delay: `${-(i * 1.37) % 12}s`,
    duration: `${9 + (i % 5) * 2.4}s`,
    drift: `${i % 2 === 0 ? 1 : -1}`,
    scale: 0.7 + ((i * 13) % 10) / 14
  };
});

export default function ForestJourney() {
  const travellerRef = useRef<HTMLDivElement | null>(null);
  const layerRef = useRef<HTMLDivElement | null>(null);
  const soundRef = useRef<Sound | null>(null);
  const sentTimer = useRef(0);
  const [soundOn, setSoundOn] = useState(false);
  const [soundBlocked, setSoundBlocked] = useState(false);
  const [paused, setPaused] = useState(false);
  const [dark, setDark] = useState(false);
  const [sent, setSent] = useState(false);

  // The traveller: a scroll timeline when the browser has one, otherwise the same path on rAF.
  useEffect(() => {
    const bird = travellerRef.current;
    if (!bird) return;

    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduced) {
      bird.style.transform = "translate3d(calc(100vw - 96px), 0, 0)";
      return;
    }

    const supportsTimeline =
      typeof CSS !== "undefined" && typeof CSS.supports === "function" && CSS.supports("animation-timeline: scroll()");

    if (supportsTimeline) {
      bird.classList.add("bird--scroll-timeline");
      return;
    }

    let frame = 0;
    const place = () => {
      frame = 0;
      const doc = document.documentElement;
      const span = Math.max(1, doc.scrollHeight - window.innerHeight);
      const p = Math.min(1, Math.max(0, window.scrollY / span));
      const travel = window.innerWidth - 96;
      const lift = Math.sin(p * Math.PI * 3) * 26;
      bird.style.transform = `translate3d(${(p * travel).toFixed(1)}px, ${lift.toFixed(1)}px, 0) rotate(${(
        Math.sin(p * Math.PI * 6) * 5
      ).toFixed(1)}deg)`;
    };
    const onScroll = () => {
      if (!frame) frame = window.requestAnimationFrame(place);
    };

    place();
    window.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll);
    return () => {
      window.removeEventListener("scroll", onScroll);
      window.removeEventListener("resize", onScroll);
      if (frame) window.cancelAnimationFrame(frame);
    };
  }, []);

  // Wing beats. Each bird's last group inside the body carries the wing; nudging its rotate keeps
  // the art exactly as drawn while letting it flap.
  useEffect(() => {
    const layer = layerRef.current;
    if (!layer) return;

    const wings = Array.from(layer.querySelectorAll<SVGGElement>(".bird-svg > g > g:last-child")).map((wing, i) => ({
      wing,
      original: wing.getAttribute("transform") ?? "",
      phase: (i * 1.7) % (Math.PI * 2),
      speed: 0.0042 + (i % 3) * 0.0009
    }));

    if (!wings.length) return;

    let raf = 0;
    const still = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (still) return;

    const tick = (now: number) => {
      if (!layer.classList.contains("is-paused")) {
        for (const { wing, original, phase, speed } of wings) {
          const angle = Math.sin(now * speed + phase) * 16;
          wing.setAttribute("transform", original.replace(/rotate\(([-\d.]+)\)/, `rotate(${angle.toFixed(2)})`));
        }
      }
      raf = window.requestAnimationFrame(tick);
    };
    raf = window.requestAnimationFrame(tick);
    return () => window.cancelAnimationFrame(raf);
  }, []);

  // Restore the remembered theme. The bootstrap script in the layout normally sets the class
  // before paint; this only syncs React's copy so the button's label matches.
  useEffect(() => {
    setDark(document.documentElement.classList.contains("dark"));
  }, []);

  useEffect(() => {
    document.documentElement.classList.toggle("is-paused", paused);
    layerRef.current?.classList.toggle("is-paused", paused);
  }, [paused]);

  useEffect(() => {
    return () => {
      soundRef.current?.stop();
      window.clearTimeout(sentTimer.current);
      document.documentElement.classList.remove("is-paused");
    };
  }, []);

  const toggleSound = useCallback(() => {
    if (soundOn) {
      soundRef.current?.stop();
      soundRef.current = null;
      setSoundOn(false);
      try {
        window.localStorage.setItem(SOUND_KEY, "off");
      } catch {
        /* the choice just does not persist */
      }
      return;
    }

    try {
      soundRef.current = startForestSound();
      setSoundOn(true);
      setSoundBlocked(false);
      try {
        window.localStorage.setItem(SOUND_KEY, "on");
      } catch {
        /* ignore */
      }
    } catch {
      setSoundBlocked(true);
    }
  }, [soundOn]);

  const sendFlock = useCallback(() => {
    window.clearTimeout(sentTimer.current);
    setSent(false);
    window.requestAnimationFrame(() => {
      setSent(true);
      sentTimer.current = window.setTimeout(() => setSent(false), 3400);
    });
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

  const perch = useCallback(
    (side: "left" | "right", index: number) => {
      const spots: Array<[number, number]> = [
        [64, 42],
        [46, 38],
        [58, 44]
      ];
      const [x, y] = spots[index % spots.length];
      return {
        left: `${side === "left" ? x : 100 - x}%`,
        top: `${y}%`
      };
    },
    []
  );

  return (
    <>
      <div
        aria-hidden
        className="pointer-events-none fixed inset-0 z-20 select-none overflow-hidden"
        ref={layerRef}
      >
        {/* Vines down both edges of the viewport. */}
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

        {/* Branches, with a bird or two perched along them. */}
        {(["left", "right"] as const).map((side) =>
          BRANCHES[side].map((branch, index) => (
            <div
              key={`${side}-${index}`}
              className={`branch-${side}-${["a", "b", "c"][index]} absolute ${side === "left" ? "-left-1" : "-right-1"} ${
                branch.width
              } ${branch.placement}`}
            >
              <div className={side === "left" ? "origin-left transition-transform duration-300" : "origin-right transition-transform duration-300"}>
                <svg
                  className="branch-shadow h-auto w-full overflow-visible"
                  dangerouslySetInnerHTML={{ __html: branch.art }}
                  viewBox={branch.viewBox}
                />
              </div>

              {/* Perched birds sit on the branch, so they ride its sway. */}
                <button
                  className={`pointer-events-auto absolute ${sent ? "flying" : ""} transition-transform duration-150 hover:scale-110 ${
                    side === "left" ? "text-left" : ""
                  }`}
                  onClick={() => soundRef.current?.chirp()}
                  style={
                    {
                      ...perch(side, index),
                      "--fly-x": side === "left" ? "86vw" : "-82vw",
                      "--fly-y": "-42vh",
                      "--fly-dir-x": side === "left" ? "14%" : "-14%",
                      "--fly-delay": `${index * 0.14}s`
                    } as React.CSSProperties
                  }
                  tabIndex={-1}
                  title="Chirp"
                  type="button"
                >
                  <svg
                    className="bird-svg h-auto w-[34px] sm:w-[42px] lg:w-[48px]"
                    dangerouslySetInnerHTML={{ __html: BIRDS[(index + (side === "right" ? 2 : 0)) % BIRDS.length] }}
                    viewBox="0 0 80 68"
                  />
                </button>
            </div>
          ))
        )}

        {/* Leaves drifting down past the page. */}
        {FALLING.map((leaf) => (
          <span
            aria-hidden
            className="leaf pointer-events-none absolute top-0"
            key={leaf.id}
            style={
              {
                left: leaf.left,
                animationDelay: leaf.delay,
                animationDuration: leaf.duration,
                width: `${(16 * leaf.scale).toFixed(1)}px`,
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

        {/* The traveller: left branch at the top of the page, right branch at the bottom. */}
        <div ref={travellerRef} className="absolute left-0 top-[46vh]">
          <svg
            className="bird-svg forest-ink h-auto w-[44px] sm:w-[56px] lg:w-[64px]"
            dangerouslySetInnerHTML={{ __html: BIRDS[0] }}
            viewBox="0 0 80 68"
          />
        </div>
      </div>

      {/* Controls: flock, sound, motion and light, all as one paper pill in the corner. */}
      <div
        className="fixed bottom-4 right-4 z-40 flex items-center gap-1.5 rounded-full border-2 border-ink bg-paper/95 px-2 py-1.5 text-[11px] font-bold uppercase tracking-[0.14em] text-ink shadow-sticker backdrop-blur sm:bottom-6 sm:right-6 sm:px-2.5"
        role="group"
        aria-label="Forest controls"
      >
        <button
          className="flex items-center gap-1.5 rounded-full border border-ink bg-sun px-2.5 py-1 text-inkSolid transition-transform hover:-translate-y-0.5 hover:bg-leaf active:translate-y-0"
          onClick={sendFlock}
          title="Send the perched birds flying from one side to the other"
          type="button"
        >
          <span aria-hidden>↗</span>
          <span className="hidden sm:inline">Send flock</span>
          <span className="sm:hidden">Flock</span>
        </button>
        <button
          aria-pressed={soundOn}
          className={`flex items-center gap-1 rounded-full border border-ink px-2.5 py-1 transition-colors ${
            soundOn ? "bg-orange text-paperSolid" : "bg-paperDeep text-ink hover:bg-blush"
          }`}
          onClick={toggleSound}
          title={soundOn ? "Mute the bird calls" : "Play gentle bird calls"}
          type="button"
        >
          <span aria-hidden>♪</span>
          <span className="hidden sm:inline">{soundOn ? "Calls on" : "Calls off"}</span>
        </button>
        <button
          aria-pressed={paused}
          className="rounded-full border border-ink bg-paper px-2.5 py-1 text-ink transition-colors hover:bg-paperDeep"
          onClick={() => setPaused((value) => !value)}
          title={paused ? "Resume the branches, wings and leaves" : "Pause the branches, wings and leaves"}
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
        <span aria-live="polite" className="sr-only">
          {sent ? "Flock away." : soundBlocked ? "Audio is unavailable in this browser." : ""}
        </span>
      </div>
    </>
  );
}
