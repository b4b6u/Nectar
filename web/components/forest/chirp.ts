"use client";

/**
 * Bird calls, synthesised. One shared AudioContext for the whole page; a hummingbird gets three
 * rising pings, every songbird gets a two-note warble that wavers before it falls away.
 */

let shared: AudioContext | null = null;

function audio(): AudioContext | null {
  if (typeof window === "undefined") return null;
  const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!Ctor) return null;
  if (!shared) shared = new Ctor();
  if (shared.state === "suspended") shared.resume().catch(() => {});
  return shared;
}

export function chirp(species: "hummingbird" | "songbird") {
  const ctx = audio();
  if (!ctx) return;

  const now = ctx.currentTime;
  const out = ctx.createGain();
  out.gain.setValueAtTime(0.06, now);
  out.connect(ctx.destination);

  if (species === "hummingbird") {
    [1950, 2450, 2950].forEach((frequency, index) => {
      const osc = ctx.createOscillator();
      const env = ctx.createGain();
      const at = now + index * 0.055;
      osc.type = "sine";
      osc.frequency.setValueAtTime(frequency, at);
      osc.frequency.exponentialRampToValueAtTime(frequency * 1.18, at + 0.05);
      env.gain.setValueAtTime(0.001, at);
      env.gain.linearRampToValueAtTime(0.85, at + 0.014);
      env.gain.exponentialRampToValueAtTime(0.001, at + 0.075);
      osc.connect(env);
      env.connect(out);
      osc.start(at);
      osc.stop(at + 0.085);
    });
    return;
  }

  const base = 1350 + Math.random() * 480;
  const osc = ctx.createOscillator();
  const env = ctx.createGain();
  osc.type = "sine";
  osc.frequency.setValueAtTime(base, now);
  osc.frequency.exponentialRampToValueAtTime(base * 1.32, now + 0.085);
  osc.frequency.setValueAtTime(base * 1.08, now + 0.12);
  osc.frequency.exponentialRampToValueAtTime(base * 1.46, now + 0.22);
  env.gain.setValueAtTime(0.001, now);
  env.gain.linearRampToValueAtTime(0.9, now + 0.025);
  env.gain.exponentialRampToValueAtTime(0.04, now + 0.105);
  env.gain.linearRampToValueAtTime(0.85, now + 0.14);
  env.gain.exponentialRampToValueAtTime(0.001, now + 0.25);
  osc.connect(env);
  env.connect(out);
  osc.start(now);
  osc.stop(now + 0.27);
}
