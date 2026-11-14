"use client";

import { useCallback, useEffect, useRef, useState } from "react";

/**
 * The forest layer: two branches, a hummingbird that crosses the site from the left branch to the
 * right one as the page scrolls, and a small procedural forest soundtrack behind a button.
 *
 * The flight is a scroll-driven CSS animation (`animation-timeline: scroll(root block)`), which
 * parks the bird against the exact scroll position rather than easing towards it. Browsers without
 * scroll timelines get the same path driven from a rAF-throttled scroll listener.
 *
 * The sound is synthesised with the Web Audio API — filtered noise for leaves and wind, plus
 * scheduled chirps — so the site ships no audio assets and nothing is fetched at runtime. It never
 * starts by itself: the button is the user gesture, and the preference is remembered.
 */

const SOUND_KEY = "nectar:forest-sound";

type Sound = { stop: () => void };

function startForestSound(): Sound {
  const AudioCtor =
    window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AudioCtor) {
    throw new Error("Web Audio is not available in this browser.");
  }

  const ctx = new AudioCtor();
  // Safari and Chrome can hand back a suspended context even inside a click; a resume is cheap and
  // harmless when the context is already running.
  void ctx.resume();
  const master = ctx.createGain();
  master.gain.value = 0;
  master.connect(ctx.destination);

  // Leaves and wind: 4 s of noise, band-limited and looped, breathing under a slow LFO.
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

  // Chirps: a short pitch arc, panned somewhere in the branches, on a loose schedule.
  let chirpTimer = 0;
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

    chirpTimer = window.setTimeout(chirp, 900 + Math.random() * 3200);
  };

  chirpTimer = window.setTimeout(chirp, 700);

  // Fade in rather than starting at full volume.
  master.gain.setValueAtTime(0.0001, ctx.currentTime);
  master.gain.exponentialRampToValueAtTime(0.5, ctx.currentTime + 2.4);

  return {
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

export default function ForestJourney() {
  const birdRef = useRef<HTMLDivElement | null>(null);
  const soundRef = useRef<Sound | null>(null);
  const [soundOn, setSoundOn] = useState(false);
  const [soundBlocked, setSoundBlocked] = useState(false);

  // Scroll timeline support check happens once, then the fallback keeps the bird in step.
  useEffect(() => {
    const bird = birdRef.current;
    if (!bird) return;

    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduced) {
      bird.style.transform = "translate3d(calc(100vw - 120px), 0, 0)";
      return;
    }

    const supportsTimeline =
      typeof CSS !== "undefined" &&
      typeof CSS.supports === "function" &&
      CSS.supports("animation-timeline: scroll()");

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
      const travel = window.innerWidth - 128;
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

  useEffect(() => {
    return () => {
      soundRef.current?.stop();
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
        /* private mode: the choice just does not persist */
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

  return (
    <>
      {/* Branches + bird. Decorative: nothing here takes a click. */}
      <div aria-hidden className="pointer-events-none fixed inset-0 z-[6] overflow-hidden">
        {/* Left branch, the one the bird leaves */}
        <svg
          className="forest-ink absolute left-0 top-[46vh] h-[220px] w-[190px] -translate-y-1/2 text-ink sm:h-[280px] sm:w-[240px]"
          fill="none"
          viewBox="0 0 240 280"
        >
          <path d="M-10 150h70l40-26h56" stroke="currentColor" strokeWidth="7" strokeLinecap="round" />
          <path d="M60 150l-14 44h-30" stroke="currentColor" strokeWidth="6" strokeLinecap="round" />
          <path d="M96 124l16-40h44" stroke="currentColor" strokeWidth="6" strokeLinecap="round" />
          <ellipse cx="86" cy="104" rx="17" ry="8" fill="currentColor" opacity="0.9" transform="rotate(-24 86 104)" />
          <ellipse cx="150" cy="88" rx="16" ry="7" fill="currentColor" opacity="0.75" transform="rotate(12 150 88)" />
          <ellipse cx="40" cy="186" rx="18" ry="8" fill="currentColor" opacity="0.85" transform="rotate(18 40 186)" />
          <ellipse cx="20" cy="150" rx="14" ry="7" fill="currentColor" opacity="0.6" transform="rotate(-8 20 150)" />
        </svg>

        {/* Right branch, the one it arrives at */}
        <svg
          className="forest-ink absolute right-0 top-[46vh] h-[220px] w-[190px] -translate-y-1/2 -scale-x-100 text-ink sm:h-[280px] sm:w-[240px]"
          fill="none"
          viewBox="0 0 240 280"
        >
          <path d="M-10 150h70l40-26h56" stroke="currentColor" strokeWidth="7" strokeLinecap="round" />
          <path d="M60 150l-14 44h-30" stroke="currentColor" strokeWidth="6" strokeLinecap="round" />
          <path d="M96 124l16-40h44" stroke="currentColor" strokeWidth="6" strokeLinecap="round" />
          <ellipse cx="86" cy="104" rx="17" ry="8" fill="currentColor" opacity="0.9" transform="rotate(-24 86 104)" />
          <ellipse cx="150" cy="88" rx="16" ry="7" fill="currentColor" opacity="0.75" transform="rotate(12 150 88)" />
          <ellipse cx="40" cy="186" rx="18" ry="8" fill="currentColor" opacity="0.85" transform="rotate(18 40 186)" />
        </svg>

        <div ref={birdRef} className="bird absolute left-0 top-[46vh]">
          <svg
            className="forest-ink h-[54px] w-[64px] text-ink sm:h-[68px] sm:w-[80px]"
            viewBox="0 0 80 68"
            fill="none"
          >
            {/* body + tail */}
            <path
              d="M22 34c0-9 8-16 18-16 8 0 14 4 17 10l15 6-15 5c-3 7-9 11-17 11-10 0-18-7-18-16z"
              fill="currentColor"
            />
            <path d="M22 34l-14 9 4-9-4-9 14 9z" fill="currentColor" opacity="0.85" />
            <path d="M58 30l16-9-2 9 2 9-16-9z" fill="#f6572d" />
            <circle cx="40" cy="28" r="2.4" fill="#fff2d6" />
            {/* wing, flapping */}
            <path
              className="bird__wing"
              d="M38 32c6-14 18-20 26-18-4 9-10 16-20 20z"
              fill="currentColor"
              opacity="0.9"
            />
          </svg>
        </div>
      </div>

      {/* Sound control: a real gesture, off until pressed. */}
      <div className="fixed bottom-4 right-4 z-40 flex items-center gap-2 sm:bottom-6 sm:right-6">
        {soundBlocked ? (
          <p className="m-0 rounded-full border-2 border-ink bg-paper px-3 py-1.5 text-[10px] font-bold uppercase tracking-[0.14em]">
            Audio unavailable
          </p>
        ) : null}
        <button
          aria-pressed={soundOn}
          className="flex items-center gap-2 rounded-full border-2 border-ink bg-paper px-4 py-2 text-[10.5px] font-bold uppercase tracking-[0.16em] shadow-sticker transition-transform hover:-translate-y-0.5 hover:bg-sun"
          onClick={toggleSound}
          type="button"
        >
          <span aria-hidden className={soundOn ? "text-orange" : "text-ink/35"}>
            {soundOn ? "\u266B" : "\u266A"}
          </span>
          {soundOn ? "Forest on" : "Forest sound"}
        </button>
      </div>
    </>
  );
}
