"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { chirp } from "./chirp";
import type { Species } from "./Bird";

/**
 * The flock.
 *
 * Eight birds — two hummingbirds, two finches, two wrens, a robin and a warbler — perch on the
 * branch tips and take turns crossing the page. Each flight is a cubic bezier with an arc layered on
 * top, so nothing travels in a straight line; the hummingbirds beat their wings the whole way and
 * trail sparks, the songbirds glide and then flick. Landing rustles the branch and knocks two leaves
 * loose. Birds blink, tilt their heads, and sing when the calls are on.
 *
 * A bird's point of reference is the invisible `data-perch` circle inside the branch art, so the
 * perch moves with the branch's sway.
 */

export type Perch = {
  id: string;
  side: "left" | "right";
  branchId: string;
  fallbackXRatio: number;
  fallbackYRatio: number;
};

export const PERCHES: Perch[] = [
  { id: "left-0", side: "left", branchId: "left-top", fallbackXRatio: 0.09, fallbackYRatio: 0.2 },
  { id: "left-1", side: "left", branchId: "left-top", fallbackXRatio: 0.05, fallbackYRatio: 0.16 },
  { id: "left-2", side: "left", branchId: "left-mid", fallbackXRatio: 0.08, fallbackYRatio: 0.5 },
  { id: "left-3", side: "left", branchId: "left-mid", fallbackXRatio: 0.06, fallbackYRatio: 0.58 },
  { id: "left-4", side: "left", branchId: "left-low", fallbackXRatio: 0.07, fallbackYRatio: 0.84 },
  { id: "right-0", side: "right", branchId: "right-top", fallbackXRatio: 0.91, fallbackYRatio: 0.21 },
  { id: "right-1", side: "right", branchId: "right-top", fallbackXRatio: 0.95, fallbackYRatio: 0.17 },
  { id: "right-2", side: "right", branchId: "right-mid", fallbackXRatio: 0.92, fallbackYRatio: 0.52 },
  { id: "right-3", side: "right", branchId: "right-mid", fallbackXRatio: 0.94, fallbackYRatio: 0.6 },
  { id: "right-4", side: "right", branchId: "right-low", fallbackXRatio: 0.93, fallbackYRatio: 0.85 }
];

const PERCH_BY_ID = Object.fromEntries(PERCHES.map((perch) => [perch.id, perch])) as Record<string, Perch>;
const DEBRIS = ["#b8cd49", "#8fc19a", "#f5b6bf", "#ffcf4a", "#f6572d"];

type Point = { x: number; y: number };

export type Bird = {
  id: number;
  species: Species;
  size: number;
  state: "perched" | "flying";
  perchId: string;
  targetPerchId: string;
  x: number;
  y: number;
  facing: 1 | -1;
  pitch: number;
  wingFlap: number;
  wingPhase: number;
  flightStart: number;
  flightDuration: number;
  startPt: Point;
  cp1: Point;
  cp2: Point;
  arcWaveAmp: number;
  arcWaveFreq: number;
  restUntil: number;
  headTilt: number;
  nextBlinkAt: number;
  blinkUntil: number;
  chirpAt: number;
  chirpId: number;
  seed: number;
};

export type Particle = {
  id: number;
  x: number;
  y: number;
  vx: number;
  vy: number;
  rotation: number;
  vRot: number;
  color: string;
  size: number;
  kind: "leaf" | "petal" | "sparkle";
  bornAt: number;
  lifeMs: number;
};

const ROSTER: Array<{ species: Species; perchId: string; baseSize: number; startFlying?: number; initialRestMs?: number }> = [
  { species: "hummingbird", perchId: "left-0", baseSize: 64, startFlying: 0.24 },
  { species: "finch", perchId: "right-2", baseSize: 60, startFlying: 0.56 },
  { species: "robin", perchId: "left-4", baseSize: 62, startFlying: 0.08 },
  { species: "hummingbird", perchId: "right-0", baseSize: 62, initialRestMs: 1400 },
  { species: "wren", perchId: "left-1", baseSize: 56, initialRestMs: 2600 },
  { species: "warbler", perchId: "right-1", baseSize: 58, initialRestMs: 3600 },
  { species: "finch", perchId: "left-2", baseSize: 58, initialRestMs: 4400 },
  { species: "wren", perchId: "right-4", baseSize: 56, initialRestMs: 2200 }
];

function cubic(a: Point, b: Point, c: Point, d: Point, t: number): Point {
  const inv = 1 - t;
  const inv2 = inv * inv;
  const t2 = t * t;
  return {
    x: inv2 * inv * a.x + 3 * inv2 * t * b.x + 3 * inv * t2 * c.x + t2 * t * d.x,
    y: inv2 * inv * a.y + 3 * inv2 * t * b.y + 3 * inv * t2 * c.y + t2 * t * d.y
  };
}

function ease(t: number) {
  return t < 0.5 ? 2 * t * t * (1.25 - 0.5 * t) : 1 - Math.pow(-2 * t + 2, 2.35) / 2;
}

export function useFlock() {
  const birds = useRef<Bird[]>([]);
  const particles = useRef<Particle[]>([]);
  const idSeq = useRef(1);
  const callsRef = useRef(false);
  const stillRef = useRef(false);
  const sparkleAt = useRef<Record<number, number>>({});

  const [paused, setPaused] = useState(false);
  const [callsOn, setCallsOn] = useState(false);
  const [rustling, setRustling] = useState<string | null>(null);
  const [, bump] = useState(0);

  useEffect(() => {
    callsRef.current = callsOn;
  }, [callsOn]);

  useEffect(() => {
    stillRef.current = paused;
  }, [paused]);

  const speak = useCallback((species: Species) => {
    if (!callsRef.current) return;
    chirp(species === "hummingbird" ? "hummingbird" : "songbird");
  }, []);

  const perchPoint = useCallback((perchId: string): Point => {
    const element = document.querySelector<SVGCircleElement>(`[data-perch="${perchId}"]`);
    if (element) {
      const box = element.getBoundingClientRect();
      if (box.width > 0 || box.height > 0 || box.left !== 0 || box.top !== 0) {
        return { x: box.left + box.width / 2, y: box.top + box.height / 2 };
      }
    }
    const fallback = PERCH_BY_ID[perchId] ?? PERCHES[0];
    const width = typeof window !== "undefined" ? window.innerWidth : 1280;
    const height = typeof window !== "undefined" ? window.innerHeight : 800;
    return { x: fallback.fallbackXRatio * width, y: fallback.fallbackYRatio * height };
  }, []);

  const rustle = useCallback((branchId: string) => {
    setRustling(branchId);
    window.setTimeout(() => setRustling((current) => (current === branchId ? null : current)), 580);
  }, []);

  const spawnDebris = useCallback((at: Point, direction: number, count = 3) => {
    const bornAt = performance.now();
    for (let i = 0; i < count; i += 1) {
      const petal = Math.random() > 0.55;
      particles.current.push({
        id: idSeq.current++,
        x: at.x + (Math.random() - 0.5) * 18,
        y: at.y + (Math.random() - 0.5) * 12,
        vx: direction * (16 + Math.random() * 28) + (Math.random() - 0.5) * 18,
        vy: -14 + Math.random() * 24,
        rotation: Math.random() * 360,
        vRot: (Math.random() - 0.5) * 160,
        color: petal ? DEBRIS[2 + Math.floor(Math.random() * 3)] : DEBRIS[Math.floor(Math.random() * 2)],
        size: petal ? 7 + Math.random() * 4 : 9 + Math.random() * 5,
        kind: petal ? "petal" : "leaf",
        bornAt,
        lifeMs: 2400 + Math.random() * 1400
      });
    }
    if (particles.current.length > 36) particles.current = particles.current.slice(-36);
  }, []);

  const sparkle = useCallback((x: number, y: number, color = "#ffcf4a") => {
    particles.current.push({
      id: idSeq.current++,
      x: x + (Math.random() - 0.5) * 10,
      y: y + (Math.random() - 0.5) * 8,
      vx: (Math.random() - 0.5) * 16,
      vy: 6 + Math.random() * 16,
      rotation: Math.random() * 45,
      vRot: 40,
      color,
      size: 4.5 + Math.random() * 3,
      kind: "sparkle",
      bornAt: performance.now(),
      lifeMs: 700 + Math.random() * 400
    });
  }, []);

  const nextPerch = useCallback((fromPerchId: string, birdId: number) => {
    const side = (PERCH_BY_ID[fromPerchId]?.side ?? "left") === "left" ? "right" : "left";
    const taken = new Set(
      birds.current.filter((bird) => bird.id !== birdId).map((bird) => (bird.state === "flying" ? bird.targetPerchId : bird.perchId))
    );
    const free = PERCHES.filter((perch) => perch.side === side && !taken.has(perch.id));
    const options = free.length > 0 ? free : PERCHES.filter((perch) => perch.side === side);
    return options[Math.floor(Math.random() * options.length)].id;
  }, []);

  const launch = useCallback(
    (bird: Bird, now: number, options?: { initialProgress?: number; playSound?: boolean }) => {
      const from = perchPoint(bird.perchId);
      const targetId = nextPerch(bird.perchId, bird.id);
      const to = perchPoint(targetId);
      const headingRight = to.x >= from.x;
      const width = typeof window !== "undefined" ? window.innerWidth : 1280;
      const height = typeof window !== "undefined" ? window.innerHeight : 800;
      const dx = to.x - from.x;
      const lift = -(55 + Math.random() * 95);
      const drop = (Math.random() > 0.45 ? -1 : 1) * (30 + Math.random() * 75);
      const clampY = (value: number) => Math.max(76, Math.min(height - 70, value));

      const cp1 = { x: from.x + dx * (0.28 + Math.random() * 0.12), y: clampY(from.y + lift) };
      const cp2 = { x: from.x + dx * (0.62 + Math.random() * 0.14), y: clampY(to.y + drop) };
      const speed = bird.species === "hummingbird" ? 310 : 255;
      const raw = (Math.max(380, Math.abs(dx)) / speed) * 1000;
      const duration = Math.max(2500, Math.min(5400, raw)) * (0.88 + Math.random() * 0.28);
      const progress = options?.initialProgress ?? 0;

      bird.state = "flying";
      bird.targetPerchId = targetId;
      bird.startPt = from;
      bird.cp1 = cp1;
      bird.cp2 = cp2;
      bird.flightDuration = duration;
      bird.flightStart = now - duration * progress;
      bird.facing = headingRight ? 1 : -1;
      bird.arcWaveAmp = bird.species === "hummingbird" ? 10 : 16 + Math.random() * 14;
      bird.arcWaveFreq = bird.species === "hummingbird" ? 3.2 : 2.1 + Math.random() * 1.1;

      if (progress === 0) {
        const perch = PERCH_BY_ID[bird.perchId];
        if (perch) rustle(perch.branchId);
        spawnDebris(from, headingRight ? 1 : -1, width < 640 ? 2 : 3);
        if (options?.playSound) speak(bird.species);
      }
    },
    [nextPerch, perchPoint, rustle, spawnDebris, speak]
  );

  // Build the roster once the branch art is in the document, so perch lookups have something to hit.
  useEffect(() => {
    const now = performance.now();
    const scale = window.innerWidth < 640 ? 0.78 : 1;

    const flock: Bird[] = ROSTER.map((entry, index) => {
      const at = perchPoint(entry.perchId);
      const side = PERCH_BY_ID[entry.perchId].side;
      return {
        id: index + 1,
        species: entry.species,
        size: Math.round(entry.baseSize * scale),
        state: "perched",
        perchId: entry.perchId,
        targetPerchId: entry.perchId,
        x: at.x,
        y: at.y,
        facing: side === "left" ? 1 : -1,
        pitch: 0,
        wingFlap: 0,
        wingPhase: Math.random() * Math.PI * 2,
        flightStart: now,
        flightDuration: 3600,
        startPt: at,
        cp1: at,
        cp2: at,
        arcWaveAmp: 12,
        arcWaveFreq: 2.4,
        restUntil: now + (entry.initialRestMs ?? 3000),
        headTilt: 0,
        nextBlinkAt: now + 1500 + Math.random() * 3500,
        blinkUntil: 0,
        chirpAt: now + 2200 + Math.random() * 5000,
        chirpId: 0,
        seed: index * 1.7 + Math.random() * 3
      };
    });

    birds.current = flock;

    const first = window.requestAnimationFrame(() => {
      const stamp = performance.now();
      flock.forEach((bird, index) => {
        const at = perchPoint(bird.perchId);
        bird.x = at.x;
        bird.y = at.y;
        const start = ROSTER[index].startFlying;
        if (start !== undefined && !stillRef.current) launch(bird, stamp, { initialProgress: start, playSound: false });
      });
      bump((value) => value + 1);
    });

    return () => window.cancelAnimationFrame(first);
  }, [launch, perchPoint]);

  // The simulation.
  useEffect(() => {
    let frame = 0;
    let previous = performance.now();

    const step = (now: number) => {
      const dt = Math.min(0.05, (now - previous) / 1000);
      previous = now;

      if (!stillRef.current) {
        const flock = birds.current;

        for (let i = 0; i < flock.length; i += 1) {
          const bird = flock[i];

          if (now >= bird.nextBlinkAt) {
            bird.blinkUntil = now + 135;
            bird.nextBlinkAt = now + 2600 + Math.random() * 4400;
          }

          if (bird.state === "perched") {
            const at = perchPoint(bird.perchId);
            const side = PERCH_BY_ID[bird.perchId]?.side ?? "left";
            bird.x = at.x;
            bird.y = at.y + Math.sin(now * 0.0042 + bird.seed) * 1.1;
            bird.facing = side === "left" ? 1 : -1;
            bird.pitch = 0;
            bird.wingFlap = Math.sin(now * 0.005 + bird.seed) * 0.35;

            const tilt = Math.sin(now * 0.0014 + bird.seed * 2.1);
            bird.headTilt = tilt > 0.72 ? 8 : tilt < -0.78 ? -7 : 0;

            if (now >= bird.chirpAt) {
              bird.chirpId += 1;
              bird.chirpAt = now + 5200 + Math.random() * 7500;
              if (Math.random() > 0.55) speak(bird.species);
            }

            if (now >= bird.restUntil) launch(bird, now, { playSound: true });
          } else {
            const progress = (now - bird.flightStart) / bird.flightDuration;
            const at = perchPoint(bird.targetPerchId);

            if (progress >= 1) {
              bird.state = "perched";
              bird.perchId = bird.targetPerchId;
              bird.x = at.x;
              bird.y = at.y;
              bird.pitch = 0;
              bird.wingFlap = 0;
              bird.facing = PERCH_BY_ID[bird.perchId]?.side === "left" ? 1 : -1;
              bird.restUntil = now + 2200 + Math.random() * 4400;
              const perch = PERCH_BY_ID[bird.perchId];
              if (perch) rustle(perch.branchId);
              spawnDebris(at, bird.facing, 2);
            } else {
              const eased = ease(Math.max(0, Math.min(1, progress)));
              const easedNext = ease(Math.min(1, progress + 0.02));
              const here = cubic(bird.startPt, bird.cp1, bird.cp2, at, eased);
              const next = cubic(bird.startPt, bird.cp1, bird.cp2, at, easedNext);
              const envelope = Math.sin(progress * Math.PI);
              const wave = Math.sin(progress * Math.PI * bird.arcWaveFreq + bird.seed) * bird.arcWaveAmp * envelope;
              const waveNext =
                Math.sin(Math.min(1, progress + 0.02) * Math.PI * bird.arcWaveFreq + bird.seed) *
                bird.arcWaveAmp *
                Math.sin(Math.min(1, progress + 0.02) * Math.PI);

              bird.x = here.x;
              bird.y = here.y + wave;

              const stepX = next.x - here.x;
              const stepY = next.y + waveNext - (here.y + wave);
              if (Math.abs(stepX) > 0.01) bird.facing = stepX >= 0 ? 1 : -1;

              const slope = (Math.atan2(stepY, Math.max(0.5, Math.abs(stepX))) * 180) / Math.PI;
              const droop = progress > 0.85 ? -((progress - 0.85) / 0.15) * 14 : 0;
              bird.pitch = Math.max(-26, Math.min(26, slope + droop));

              if (bird.species === "hummingbird") {
                bird.wingPhase += dt * 44;
                bird.wingFlap = Math.sin(bird.wingPhase);
                const last = sparkleAt.current[bird.id] ?? 0;
                if (now - last > 135) {
                  sparkleAt.current[bird.id] = now;
                  sparkle(bird.x - bird.facing * 14, bird.y + 2, Math.random() > 0.45 ? "#ffcf4a" : "#f5b6bf");
                }
              } else {
                const flick = Math.sin(progress * Math.PI * 5 + bird.seed);
                if (progress > 0.22 && progress < 0.78 && flick < -0.58) {
                  bird.wingFlap += (-0.28 - bird.wingFlap) * Math.min(1, dt * 14);
                } else {
                  bird.wingPhase += dt * 23;
                  bird.wingFlap = Math.sin(bird.wingPhase);
                }
              }
            }
          }
        }

        const alive: Particle[] = [];
        for (let i = 0; i < particles.current.length; i += 1) {
          const particle = particles.current[i];
          const age = now - particle.bornAt;
          if (age >= particle.lifeMs) continue;
          if (particle.kind === "sparkle") {
            particle.x += particle.vx * dt;
            particle.y += particle.vy * dt;
          } else {
            particle.x += (particle.vx + Math.sin(age * 0.004 + particle.id) * 26) * dt;
            particle.vy = Math.min(46, particle.vy + 22 * dt);
            particle.y += particle.vy * dt;
            particle.vx *= 0.988;
            particle.rotation += particle.vRot * dt;
          }
          alive.push(particle);
        }
        particles.current = alive;
      }

      bump((value) => (value + 1) % 100000);
      frame = window.requestAnimationFrame(step);
    };

    frame = window.requestAnimationFrame(step);
    return () => window.cancelAnimationFrame(frame);
  }, [launch, perchPoint, rustle, sparkle, spawnDebris, speak]);

  // Resize: sizes follow the breakpoint, perched birds re-seat themselves.
  useEffect(() => {
    const onResize = () => {
      const scale = window.innerWidth < 640 ? 0.78 : 1;
      birds.current.forEach((bird) => {
        bird.size = Math.round((bird.species === "hummingbird" ? 64 : 58) * scale);
        if (bird.state === "perched") {
          const at = perchPoint(bird.perchId);
          bird.x = at.x;
          bird.y = at.y;
        }
      });
      bump((value) => value + 1);
    };
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, [perchPoint]);

  // Click a bird: send it on, or make it flutter if it is already in the air.
  const poke = useCallback(
    (id: number) => {
      const bird = birds.current.find((entry) => entry.id === id);
      if (!bird) return;
      const now = performance.now();
      if (stillRef.current) setPaused(false);

      if (bird.state === "perched") {
        launch(bird, now, { playSound: true });
        speak(bird.species);
      } else {
        bird.chirpId += 1;
        for (let i = 0; i < 4; i += 1) sparkle(bird.x, bird.y, i % 2 === 0 ? "#ffcf4a" : "#f6572d");
        speak(bird.species);
      }
      bump((value) => value + 1);
    },
    [launch, sparkle, speak]
  );

  const sendFlock = useCallback(() => {
    if (stillRef.current) setPaused(false);
    const perched = birds.current.filter((bird) => bird.state === "perched");
    perched.forEach((bird, index) => {
      window.setTimeout(() => {
        if (bird.state === "perched") launch(bird, performance.now(), { playSound: index === 0 });
      }, index * 180);
    });
    if (perched.length === 0) birds.current.forEach((bird) => sparkle(bird.x, bird.y, "#ffcf4a"));
    speak("hummingbird");
  }, [launch, sparkle, speak]);

  return {
    birds: birds.current,
    particles: particles.current,
    rustling,
    paused,
    setPaused,
    callsOn,
    setCallsOn,
    poke,
    sendFlock
  };
}
