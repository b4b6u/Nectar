"use client";

/**
 * Fig. 01 — a clone of the app, not an impression of it.
 *
 * Every measurement here was taken from Nectar's own XAML: the 1000 × 340 workspace surface with its
 * 10 px padding and 26 px corner, the 34 px header row, the four 239 px cards with 13/10 padding and
 * 18 px corners, the type scale (13.5 titles, 12 rows, 11.5 input, 10.5 chips, 9.5 meta, 48 timer),
 * and the brushed colours from Theme.xaml. The whole thing is drawn at that exact size and then
 * scaled down as one piece, so the proportions never change.
 *
 * It is a demo: tasks, note, timer and bell states live in React and nothing is stored or sent.
 */

import { useEffect, useMemo, useRef, useState } from "react";

const C = {
  shell: "#000000",
  shellText: "rgba(255,255,255,0.95)",
  shellMuted: "rgba(255,255,255,0.48)",
  shellHover: "rgba(255,255,255,0.10)",
  track: "#1C1F24",
  active: "#3B424B",
  mint: "#8DBA98",
  lilac: "#A39CD0",
  butter: "#D2CA72",
  sky: "#8CB1CD",
  ink: "rgba(27,36,28,0.95)",
  inkMuted: "rgba(27,36,28,0.54)",
  inkFaint: "rgba(27,36,28,0.35)",
  row: "rgba(255,255,255,0.55)",
  chip: "rgba(255,255,255,0.40)",
  chipActive: "rgba(255,255,255,0.90)",
  deepGreen: "#2E6B4A",
  trackDark: "rgba(27,36,28,0.20)",
  danger: "#F0A0A8"
};

const FONT = '"Segoe UI Variable Text","Segoe UI Variable","Segoe UI",system-ui,sans-serif';
const MONO = 'Consolas,"Cascadia Mono",ui-monospace,monospace';
const MARK = "/Nectar/Assets/nectar-mark.scale-400.png";

const WIDTH = 1000;
const HEIGHT = 340;
const CARD_W = 239;
const GAP = 8;
/** Below this the type stops being readable, so the window pans instead of shrinking further. */
const MIN_SCALE = 0.78;

type Task = { id: number; title: string; done: boolean; meta: string };
type Tab = "Workspace" | "Insights" | "Customize";

const SEED: Task[] = [
  { id: 1, title: "Send the deck to Priya", done: false, meta: "Added Mon 10 Nov" },
  { id: 2, title: "Book the studio for Thursday", done: false, meta: "Added Tue 11 Nov" },
  { id: 3, title: "Clear the inbox", done: true, meta: "Done" },
  { id: 4, title: "Pick a title for the launch note", done: false, meta: "Due Fri 14 Nov · 17:00" }
];

const AGENDA = [
  { range: "09:30 – 09:45", subject: "Stand-up", where: "Meet" },
  { range: "11:00 – 11:45", subject: "Design review", where: "Studio" },
  { range: "14:30 – 16:00", subject: "Ship the release", where: "Deep work" }
];

const mmss = (total: number) =>
  `${Math.floor(total / 60).toString().padStart(2, "0")}:${(total % 60).toString().padStart(2, "0")}`;

const meta: React.CSSProperties = {
  fontFamily: FONT,
  fontSize: 9.5,
  fontWeight: 600,
  letterSpacing: "0.12em",
  color: C.inkMuted,
  whiteSpace: "nowrap",
  overflow: "hidden",
  textOverflow: "ellipsis"
};

const cardStyle = (background: string): React.CSSProperties => ({
  position: "relative",
  width: CARD_W,
  flex: "0 0 auto",
  borderRadius: 18,
  padding: "10px 13px",
  background,
  display: "flex",
  flexDirection: "column",
  rowGap: 6,
  overflow: "hidden",
  color: C.ink,
  fontFamily: FONT,
  textAlign: "left"
});

export default function AppDemo() {
  const wrapRef = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(1);

  const [tab, setTab] = useState<Tab>("Workspace");
  const [collapsed, setCollapsed] = useState(false);
  const [tasks, setTasks] = useState<Task[]>(SEED);
  const [draft, setDraft] = useState("");
  const [note, setNote] = useState(
    "Three things today, not thirty.\n\nThe deck first — everything else is a flower worth sniffing later."
  );
  const [minutes, setMinutes] = useState(25);
  const [seconds, setSeconds] = useState(25 * 60);
  const [running, setRunning] = useState(false);
  const [bells, setBells] = useState<number[]>([]);

  useEffect(() => {
    const element = wrapRef.current;
    if (!element) return;

    const measure = () => setScale(Math.max(MIN_SCALE, Math.min(1, element.clientWidth / WIDTH)));
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!running) return;
    const id = window.setInterval(() => setSeconds((value) => (value <= 1 ? 0 : value - 1)), 1000);
    return () => window.clearInterval(id);
  }, [running]);

  useEffect(() => {
    if (seconds === 0) setRunning(false);
  }, [seconds]);

  const open = tasks.filter((task) => !task.done);
  const done = tasks.length - open.length;
  const focusTitle = open[0]?.title ?? "Focus session";
  const words = useMemo(() => note.trim().split(/\s+/).filter(Boolean).length, [note]);
  const today = useMemo(
    () => new Date().toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short", year: "numeric" }),
    []
  );
  const todayShort = useMemo(
    () => new Date().toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short" }).toUpperCase(),
    []
  );

  const addTask = () => {
    const title = draft.trim();
    if (!title) return;
    setTasks((list) => [{ id: Date.now(), title, done: false, meta: "Added just now" }, ...list]);
    setDraft("");
  };

  const toggle = (id: number) =>
    setTasks((list) =>
      list.map((task) => (task.id === id ? { ...task, done: !task.done, meta: task.done ? "Added today" : "Done" } : task))
    );

  const remove = (id: number) => setTasks((list) => list.filter((task) => task.id !== id));

  const setLength = (next: number) => {
    setMinutes(next);
    setSeconds(next * 60);
    setRunning(false);
  };

  const progress = 1 - seconds / (minutes * 60);

  return (
    <div ref={wrapRef} style={{ width: "100%" }}>
      <div style={{ overflowX: "auto", overflowY: "hidden", borderRadius: 26 * scale }}>
        <div style={{ position: "relative", width: WIDTH * scale, height: HEIGHT * scale }}>
          <div
            style={{
              width: WIDTH,
              height: HEIGHT,
              transform: `scale(${scale})`,
              transformOrigin: "top left",
              position: "absolute",
              top: 0,
              left: 0
            }}
          >
          {collapsed ? (
            <button
              aria-label="Open the workspace"
              onClick={() => setCollapsed(false)}
              style={{
                width: 200,
                height: 30,
                margin: "0 auto",
                borderRadius: 15,
                background: C.shell,
                border: "none",
                padding: "0 12px",
                display: "flex",
                alignItems: "center",
                gap: 8,
                color: C.shellText,
                fontFamily: FONT,
                cursor: "pointer"
              }}
              type="button"
            >
              <img alt="" src={MARK} style={{ width: 20, height: 20, borderRadius: 5 }} />
              <span style={{ fontSize: 12.5, fontWeight: 600 }}>Nectar</span>
              <span style={{ marginLeft: "auto", fontSize: 11, fontFamily: MONO, color: C.shellMuted }}>
                {mmss(seconds)}
              </span>
            </button>
          ) : (
            <div
              style={{
                width: WIDTH,
                height: HEIGHT,
                borderRadius: 26,
                background: C.shell,
                padding: 10,
                display: "flex",
                flexDirection: "column",
                rowGap: GAP,
                fontFamily: FONT
              }}
            >
              {/* Header: brand, tabs, close — 34 px row, 6/0/2/0 padding. */}
              <div
                style={{
                  height: 34,
                  padding: "0 2px 0 6px",
                  display: "flex",
                  alignItems: "center",
                  columnGap: 8
                }}
              >
                <img alt="Nectar" src={MARK} style={{ width: 30, height: 30, borderRadius: 8 }} />
                <span style={{ fontSize: 16, fontWeight: 600, color: C.shellText }}>Nectar</span>

                <div
                  style={{
                    marginLeft: "auto",
                    display: "flex",
                    alignItems: "center",
                    gap: 2,
                    background: C.track,
                    borderRadius: 12,
                    padding: 3
                  }}
                >
                  {(["Workspace", "Insights", "Customize"] as Tab[]).map((name) => (
                    <button
                      key={name}
                      aria-pressed={tab === name}
                      onClick={() => setTab(name)}
                      style={{
                        padding: "4px 10px",
                        borderRadius: 9,
                        border: "none",
                        background: tab === name ? C.active : "transparent",
                        color: tab === name ? C.shellText : C.shellMuted,
                        fontFamily: FONT,
                        fontSize: 10.5,
                        fontWeight: 600,
                        cursor: "pointer"
                      }}
                      type="button"
                    >
                      {name}
                    </button>
                  ))}
                </div>

                <button
                  aria-label="Collapse to the notch pill"
                  onClick={() => setCollapsed(true)}
                  style={{
                    width: 26,
                    height: 26,
                    borderRadius: 13,
                    border: "none",
                    background: "transparent",
                    color: "rgba(255,255,255,0.72)",
                    fontSize: 11,
                    lineHeight: 1,
                    cursor: "pointer"
                  }}
                  type="button"
                >
                  ✕
                </button>
              </div>

              {tab === "Workspace" && (
                <div style={{ display: "flex", columnGap: GAP, flex: 1, minHeight: 0 }}>
                  {/* Tasks */}
                  <div style={cardStyle(C.mint)}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>▣</span>
                      <span style={{ fontSize: 13.5, fontWeight: 600 }}>Today&rsquo;s tasks</span>
                    </div>

                    <div style={{ display: "flex", gap: 6 }}>
                      <span
                        style={{
                          padding: "3px 8px",
                          borderRadius: 9,
                          background: C.chipActive,
                          fontSize: 10.5,
                          fontWeight: 600
                        }}
                      >
                        To do {open.length}
                      </span>
                      <span
                        style={{
                          padding: "3px 8px",
                          borderRadius: 9,
                          background: C.chip,
                          fontSize: 10.5,
                          fontWeight: 600,
                          color: C.inkMuted
                        }}
                      >
                        Completed {done}
                      </span>
                    
                    </div>

                    <div style={{ flex: 1, minHeight: 0, overflow: "hidden", display: "flex", flexDirection: "column", gap: 3 }}>
                      {tasks.map((task) => (
                        <div key={task.id}>
                          <div
                            style={{
                              display: "flex",
                              alignItems: "flex-start",
                              columnGap: 9,
                              padding: "5px 9px",
                              borderRadius: 10,
                              background: C.row
                            }}
                          >
                            <button
                              aria-label={task.done ? `Reopen ${task.title}` : `Complete ${task.title}`}
                              aria-pressed={task.done}
                              onClick={() => toggle(task.id)}
                              style={{
                                width: 18,
                                height: 18,
                                flex: "0 0 18px",
                                marginTop: 1,
                                borderRadius: 9,
                                border: `1.4px solid ${task.done ? C.ink : C.inkMuted}`,
                                background: task.done ? C.ink : "transparent",
                                color: "#fff",
                                fontSize: 9,
                                lineHeight: 1,
                                display: "grid",
                                placeItems: "center",
                                cursor: "pointer"
                              }}
                              type="button"
                            >
                              {task.done ? "✓" : ""}
                            </button>

                            <div style={{ minWidth: 0, flex: 1 }}>
                              <div
                                style={{
                                  fontSize: 12,
                                  fontWeight: 600,
                                  whiteSpace: "nowrap",
                                  overflow: "hidden",
                                  textOverflow: "ellipsis",
                                  textDecoration: task.done ? "line-through" : "none",
                                  opacity: task.done ? 0.5 : 1
                                }}
                              >
                                {task.title}
                              </div>
                              <div style={{ ...meta, marginTop: 2 }}>{task.meta}</div>
                            </div>

                            <button
                              aria-label={`Delete ${task.title}`}
                              onClick={() => remove(task.id)}
                              style={{
                                width: 20,
                                height: 20,
                                flex: "0 0 20px",
                                border: "none",
                                borderRadius: 10,
                                background: "transparent",
                                color: C.ink,
                                opacity: 0.35,
                                fontSize: 9,
                                lineHeight: 1,
                                cursor: "pointer"
                              }}
                              type="button"
                            >
                              ✕
                            </button>
                          </div>
                          <div
                            style={{
                              height: 1,
                              margin: "2px 4px 0",
                              borderTop: `1px dashed ${C.inkFaint}`
                            }}
                          />
                        </div>
                      ))}
                    </div>

                    <div style={{ display: "flex", alignItems: "center", columnGap: 6 }}>
                      <span style={{ fontSize: 10, color: C.inkMuted }} aria-hidden>+</span>
                      <input
                        aria-label="Add a task"
                        onChange={(event) => setDraft(event.target.value)}
                        onKeyDown={(event) => {
                          if (event.key === "Enter") addTask();
                        }}
                        placeholder="Add a task, press Enter"
                        style={{
                          flex: 1,
                          minWidth: 0,
                          border: "none",
                          outline: "none",
                          background: "transparent",
                          fontFamily: FONT,
                          fontSize: 11.5,
                          color: C.ink,
                          padding: 0
                        }}
                        value={draft}
                      />
                      <span
                        aria-hidden
                        style={{
                          width: 26,
                          height: 26,
                          borderRadius: 13,
                          background: "rgba(255,255,255,0.2)",
                          display: "grid",
                          placeItems: "center",
                          fontSize: 11
                        }}
                      >
                        ◔
                      </span>
                    </div>
                  </div>

                  {/* Focus timer */}
                  <div style={cardStyle(C.lilac)}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>◷</span>
                      <span
                        style={{
                          fontSize: 13.5,
                          fontWeight: 600,
                          whiteSpace: "nowrap",
                          overflow: "hidden",
                          textOverflow: "ellipsis"
                        }}
                      >
                        {focusTitle}
                      </span>
                    </div>

                    <div style={{ flex: 1, display: "flex", flexDirection: "column", justifyContent: "center", minHeight: 0 }}>
                      <div style={{ fontFamily: MONO, fontSize: 48, fontWeight: 700, letterSpacing: "0.03em", lineHeight: 1 }}>
                        {mmss(seconds)}
                      </div>
                      <div style={{ ...meta, marginTop: 4 }}>REMAINING</div>
                      <div
                        style={{
                          height: 3,
                          borderRadius: 2,
                          marginTop: 9,
                          background: C.trackDark,
                          overflow: "hidden"
                        }}
                      >
                        <div
                          style={{
                            height: "100%",
                            width: `${Math.min(100, Math.max(0, progress * 100))}%`,
                            background: C.deepGreen
                          }}
                        />
                      </div>
                    </div>

                    <div style={{ display: "flex", alignItems: "center", columnGap: 6 }}>
                      <button
                        onClick={() => setRunning((value) => !value)}
                        style={{
                          flex: 1,
                          padding: "7px 10px",
                          borderRadius: 14,
                          border: "none",
                          background: C.chipActive,
                          color: C.ink,
                          fontFamily: FONT,
                          fontSize: 12,
                          fontWeight: 600,
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                          gap: 6,
                          cursor: "pointer"
                        }}
                        type="button"
                      >
                        <span style={{ fontSize: 10 }} aria-hidden>{running ? "❚❚" : "▶"}</span>
                        {running ? "Pause" : "Start"}
                      </button>
                      <button
                        aria-label="Reset session"
                        onClick={() => setLength(minutes)}
                        style={{
                          width: 34,
                          height: 34,
                          borderRadius: 17,
                          border: "none",
                          background: C.chip,
                          color: C.ink,
                          fontSize: 12,
                          cursor: "pointer"
                        }}
                        type="button"
                      >
                        ↻
                      </button>
                    </div>

                    <button
                      onClick={() => setLength(({ 15: 25, 25: 45, 45: 50, 50: 15 } as Record<number, number>)[minutes] ?? 25)}
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 5,
                        padding: 0,
                        border: "none",
                        background: "transparent",
                        cursor: "pointer",
                        alignSelf: "flex-start"
                      }}
                      type="button"
                    >
                      <span style={{ fontSize: 10, color: C.inkMuted }} aria-hidden>◴</span>
                      <span style={meta}>SET TIME · {minutes} MIN</span>
                    </button>
                  </div>

                  {/* Notepad */}
                  <div style={cardStyle(C.butter)}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>✎</span>
                      <span style={{ fontSize: 13.5, fontWeight: 600 }}>Notepad</span>
                    </div>
                    <div style={meta}>{today}</div>
                    <textarea
                      aria-label="The scratch note"
                      onChange={(event) => setNote(event.target.value)}
                      placeholder="Think on paper. Ctrl + Enter turns a line into a task."
                      style={{
                        flex: 1,
                        minHeight: 0,
                        width: "100%",
                        resize: "none",
                        border: "none",
                        outline: "none",
                        background: "transparent",
                        fontFamily: FONT,
                        fontSize: 12.5,
                        lineHeight: 1.45,
                        color: C.ink,
                        padding: 0
                      }}
                      value={note}
                    />
                    <div style={{ display: "flex", alignItems: "center", columnGap: 6 }}>
                      <span style={meta}>{words} {words === 1 ? "WORD" : "WORDS"}</span>
                      <span style={{ marginLeft: "auto", ...meta }}>Saved</span>
                      <span
                        aria-hidden
                        style={{
                          width: 26,
                          height: 26,
                          borderRadius: 13,
                          background: "rgba(255,255,255,0.2)",
                          display: "grid",
                          placeItems: "center",
                          fontSize: 11
                        }}
                      >
                        ◔
                      </span>
                    </div>
                  </div>

                  {/* Events */}
                  <div style={cardStyle(C.sky)}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>▤</span>
                      <span style={{ fontSize: 13.5, fontWeight: 600 }}>Events</span>
                      <span style={{ marginLeft: "auto", fontSize: 10, color: C.inkMuted }} aria-hidden>⟳</span>
                    </div>
                    <div style={meta}>TODAY · {todayShort}</div>

                    <div style={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column", gap: 4, overflow: "hidden" }}>
                      {AGENDA.map((event, index) => (
                        <div
                          key={event.subject}
                          style={{
                            display: "flex",
                            alignItems: "center",
                            columnGap: 7,
                            padding: "5px 8px",
                            borderRadius: 9,
                            background: C.row
                          }}
                        >
                          <span style={{ ...meta, flex: "0 0 auto" }}>{event.range}</span>
                          <div style={{ minWidth: 0, flex: 1 }}>
                            <div
                              style={{
                                fontSize: 11.5,
                                fontWeight: 600,
                                whiteSpace: "nowrap",
                                overflow: "hidden",
                                textOverflow: "ellipsis"
                              }}
                            >
                              {event.subject}
                            </div>
                            <div style={meta}>{event.where}</div>
                          </div>
                          <button
                            aria-label={`Remind me about ${event.subject}`}
                            aria-pressed={bells.includes(index)}
                            onClick={() =>
                              setBells((list) => (list.includes(index) ? list.filter((item) => item !== index) : [...list, index]))
                            }
                            style={{
                              width: 22,
                              height: 22,
                              flex: "0 0 22px",
                              borderRadius: 11,
                              border: "none",
                              background: "transparent",
                              color: bells.includes(index) ? C.ink : C.inkMuted,
                              fontSize: 11,
                              lineHeight: 1,
                              cursor: "pointer"
                            }}
                            type="button"
                          >
                            ♪
                          </button>
                        </div>
                      ))}
                    </div>

                    <div style={meta}>No calendar linked</div>
                  </div>
                </div>
              )}

              {tab === "Insights" && (
                <div style={{ display: "flex", columnGap: GAP, flex: 1, minHeight: 0 }}>
                  <div style={{ ...cardStyle(C.mint), width: "auto", flex: "0 0 385px" }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>▦</span>
                      <span style={{ fontSize: 13.5, fontWeight: 600 }}>Momentum</span>
                    </div>
                    <div style={{ ...meta }}>LAST 7 DAYS</div>
                    <div style={{ flex: 1, display: "flex", flexDirection: "column", justifyContent: "center" }}>
                      <div style={{ fontFamily: MONO, fontSize: 48, fontWeight: 700, lineHeight: 1 }}>{done + 18}</div>
                      <div style={{ fontSize: 13, fontWeight: 600, marginTop: 4 }}>tasks completed</div>
                      <div style={{ ...meta, marginTop: 8, letterSpacing: "0.06em" }}>6 AHEAD OF THE WEEK BEFORE</div>
                    </div>
                    <div style={{ ...meta }}>7 H FOCUSED · 4 ACTIVE DAYS</div>
                  </div>

                  <div style={{ ...cardStyle(C.lilac), width: "auto", flex: 1 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>▥</span>
                      <span style={{ fontSize: 13.5, fontWeight: 600 }}>The week, two ways</span>
                      <span style={{ marginLeft: "auto", display: "flex", gap: 2, background: "rgba(255,255,255,0.35)", borderRadius: 9, padding: 2 }}>
                        {["Tasks", "Focus"].map((name) => (
                          <span
                            key={name}
                            style={{
                              padding: "2px 7px",
                              borderRadius: 7,
                              fontSize: 9.5,
                              fontWeight: 600,
                              letterSpacing: "0.08em",
                              background: name === "Tasks" ? C.chipActive : "transparent",
                              color: name === "Tasks" ? C.ink : C.inkMuted
                            }}
                          >
                            {name.toUpperCase()}
                          </span>
                        ))}
                      </span>
                    </div>
                    <div style={{ ...meta }}>{todayShort} — 7 DAYS</div>
                    <div style={{ flex: 1, display: "flex", alignItems: "flex-end", gap: 6, minHeight: 0 }}>
                      {[3, 5, 2, 6, 4, 7, 3].map((value, index) => (
                        <div key={index} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", gap: 3 }}>
                          <div style={{ width: "100%", height: 96, display: "flex", alignItems: "flex-end" }}>
                            <div style={{ position: "relative", width: "100%", height: `${(value / 7) * 100}%` }}>
                              <div style={{ position: "absolute", inset: 0, background: "rgba(27,36,28,0.14)" }} />
                              <div style={{ position: "absolute", inset: 0, background: "rgba(35,37,48,0.9)" }} />
                            </div>
                          </div>
                          <span style={{ ...meta, fontSize: 9 }}>{["M", "T", "W", "T", "F", "S", "S"][index]}</span>
                        </div>
                      ))}
                    </div>
                    <div style={{ display: "flex", gap: 10 }}>
                      <span style={meta}>■ COMPLETED</span>
                      <span style={{ ...meta, color: "rgba(27,36,28,0.3)" }}>■ PLANNED</span>
                    </div>
                  </div>
                </div>
              )}

              {tab === "Customize" && (
                <div style={{ display: "flex", columnGap: GAP, flex: 1, minHeight: 0 }}>
                  <div style={{ ...cardStyle(C.mint), width: "auto", flex: 1 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>⚙</span>
                      <span style={{ fontSize: 13.5, fontWeight: 600 }}>General</span>
                    </div>
                    <div style={{ ...meta }}>HOW NECTAR BEHAVES</div>
                    <ToggleRow label="Hover cards to lift them" />
                    <ToggleRow label="Show a banner when something fails" />
                    <div style={{ marginTop: "auto", display: "flex", alignItems: "center", gap: 6 }}>
                      <span style={{ ...meta }}>SUMMON</span>
                      <span style={{ padding: "3px 8px", borderRadius: 8, background: "rgba(255,255,255,0.5)", fontSize: 10.5, fontWeight: 600 }}>
                        Win + Shift + N
                      </span>
                    </div>
                  </div>

                  <div style={{ ...cardStyle(C.lilac), width: "auto", flex: 1 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>◷</span>
                      <span style={{ fontSize: 13.5, fontWeight: 600 }}>Focus</span>
                    </div>
                    <div style={{ ...meta }}>SESSION LENGTH</div>
                    <div style={{ display: "flex", gap: 4 }}>
                      {[15, 25, 45, 50].map((length) => (
                        <button
                          key={length}
                          aria-pressed={minutes === length}
                          onClick={() => setLength(length)}
                          style={{
                            padding: "4px 9px",
                            borderRadius: 8,
                            border: "none",
                            background: minutes === length ? C.chipActive : C.chip,
                            color: minutes === length ? C.ink : C.inkMuted,
                            fontFamily: FONT,
                            fontSize: 10.5,
                            fontWeight: 600,
                            cursor: "pointer"
                          }}
                          type="button"
                        >
                          {length}m
                        </button>
                      ))}
                    </div>
                    <ToggleRow label="Chime when a session ends" />
                    <ToggleRow label="Start the next session on its own" defaultOn={false} />
                  </div>

                  <div style={{ ...cardStyle(C.sky), width: "auto", flex: 1 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
                      <span style={{ fontSize: 12, color: C.inkMuted }} aria-hidden>▤</span>
                      <span style={{ fontSize: 13.5, fontWeight: 600 }}>Workspace</span>
                    </div>
                    <div style={{ ...meta }}>FOUR CARDS, ONE GLANCE</div>
                    {["Today's tasks", "Focus timer", "Notepad", "Events"].map((name) => (
                      <div key={name} style={{ display: "flex", alignItems: "center", gap: 8, padding: "3px 0" }}>
                        <span style={{ color: C.inkMuted, fontSize: 11 }} aria-hidden>⠿</span>
                        <span style={{ flex: 1, fontSize: 12, fontWeight: 600 }}>{name}</span>
                        <span style={meta}>SHOWN</span>
                      </div>
                    ))}
                    <div style={{ marginTop: "auto", ...meta }}>EVERYTHING SAVES ITSELF</div>
                  </div>
                </div>
              )}
            </div>
          )}
          </div>
        </div>
      </div>
    </div>
  );
}

function ToggleRow({ label, defaultOn = true }: { label: string; defaultOn?: boolean }) {
  const [on, setOn] = useState(defaultOn);

  return (
    <button
      aria-checked={on}
      aria-label={label}
      onClick={() => setOn((value) => !value)}
      role="switch"
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        width: "100%",
        padding: "3px 0",
        border: "none",
        background: "transparent",
        color: C.ink,
        fontFamily: FONT,
        cursor: "pointer"
      }}
      type="button"
    >
      <span style={{ flex: 1, textAlign: "left", fontSize: 12, fontWeight: 600 }}>{label}</span>
      <span
        style={{
          position: "relative",
          width: 26,
          height: 15,
          flex: "0 0 26px",
          borderRadius: 8,
          background: on ? C.deepGreen : "rgba(27,36,28,0.18)"
        }}
      >
        <span
          style={{
            position: "absolute",
            top: 2,
            left: on ? 13 : 2,
            width: 11,
            height: 11,
            borderRadius: 6,
            background: "#fff"
          }}
        />
      </span>
    </button>
  );
}
