"use client";

/**
 * Fig. 01, playable. The hero used to be a screenshot of the workspace; this is the workspace —
 * the same four cards, the same tab strip, the same collapsed pill — with the parts that are worth
 * feeling (typing a task, ticking it off, starting the timer, hiding a card, changing the session
 * length) actually live. It writes nothing anywhere and stores nothing; refreshing reloads the
 * demo exactly as it ships.
 */

import { useEffect, useMemo, useState } from "react";

const INK = "#1B241C";
const MINT = "#8DBA98";
const LILAC = "#A39CD0";
const BUTTER = "#D2CA72";
const SKY = "#8CB1CD";
const SHELL = "#050607";
const MARK = "/Nectar/Assets/nectar-mark.scale-400.png";

type Task = { id: number; text: string; done: boolean };
type CardId = "tasks" | "timer" | "notes" | "events";

const CARDS: { id: CardId; name: string; colour: string }[] = [
  { id: "tasks", name: "Tasks", colour: MINT },
  { id: "timer", name: "Focus timer", colour: LILAC },
  { id: "notes", name: "Notepad", colour: BUTTER },
  { id: "events", name: "Events", colour: SKY }
];

const SEED_TASKS: Task[] = [
  { id: 1, text: "Send the deck to Priya", done: false },
  { id: 2, text: "Book the studio for Thursday", done: false },
  { id: 3, text: "Clear the inbox", done: true }
];

const EVENTS = [
  { time: "09:30", title: "Stand-up", meta: "15 min · Meet" },
  { time: "11:00", title: "Design review", meta: "45 min · Studio" },
  { time: "14:30", title: "Ship the release", meta: "Deep work" }
];

const INSIGHTS = {
  tasks: { label: "Tasks", unit: "done", values: [3, 5, 2, 6, 4, 7, 3] },
  focus: { label: "Focus", unit: "min", values: [50, 75, 25, 120, 60, 90, 45] }
};

const DAYS = ["M", "T", "W", "T", "F", "S", "S"];

const mmss = (seconds: number) =>
  `${Math.floor(seconds / 60).toString().padStart(2, "0")}:${(seconds % 60)
    .toString()
    .padStart(2, "0")}`;

export default function AppDemo() {
  const [tab, setTab] = useState<"workspace" | "insights" | "customize">("workspace");
  const [collapsed, setCollapsed] = useState(false);

  const [tasks, setTasks] = useState<Task[]>(SEED_TASKS);
  const [draft, setDraft] = useState("");
  const [note, setNote] = useState(
    "Three things today, not thirty.\n\nThe deck first — everything else is a flower worth sniffing later."
  );

  const [minutes, setMinutes] = useState(25);
  const [seconds, setSeconds] = useState(25 * 60);
  const [running, setRunning] = useState(false);

  const [visible, setVisible] = useState<Record<CardId, boolean>>({
    tasks: true,
    timer: true,
    notes: true,
    events: true
  });
  const [hover, setHover] = useState(true);

  const [series, setSeries] = useState<"tasks" | "focus">("tasks");
  const [bells, setBells] = useState<string[]>([]);
  const [showPill, setShowPill] = useState(true);
  const [chime, setChime] = useState(true);
  const [tray, setTray] = useState(true);

  useEffect(() => {
    if (!running) return;
    const id = window.setInterval(() => setSeconds((s) => (s <= 1 ? 0 : s - 1)), 1000);
    return () => window.clearInterval(id);
  }, [running]);

  useEffect(() => {
    if (seconds === 0) setRunning(false);
  }, [seconds]);

  const open = tasks.filter((task) => !task.done).length;
  const words = useMemo(() => note.trim().split(/\s+/).filter(Boolean).length, [note]);
  const data = INSIGHTS[series];
  const peak = Math.max(...data.values);
  const total = INSIGHTS.tasks.values.reduce((sum, value) => sum + value, 0);

  const addTask = () => {
    const text = draft.trim();
    if (!text) return;
    setTasks((list) => [...list, { id: Date.now(), text, done: false }]);
    setDraft("");
  };

  const pickMinutes = (next: number) => {
    setMinutes(next);
    setSeconds(next * 60);
    setRunning(false);
  };

  const toggleTask = (id: number) =>
    setTasks((list) => list.map((task) => (task.id === id ? { ...task, done: !task.done } : task)));

  const toggleBell = (time: string) =>
    setBells((list) => (list.includes(time) ? list.filter((item) => item !== time) : [...list, time]));

  return (
    <div className="text-inkSolid">
      <div
        className="overflow-hidden rounded-[24px] border-2 border-ink p-2 shadow-stickerLg"
        style={{ background: SHELL }}
      >
        {collapsed ? (
          <button
            aria-label="Open the Nectar demo"
            className="mx-auto flex w-full max-w-[320px] items-center gap-2 rounded-full px-4 py-1.5 text-[11px] font-semibold text-white/90 transition-colors hover:bg-white/10"
            onClick={() => setCollapsed(false)}
            type="button"
          >
            <img alt="" className="h-4 w-4 rounded-[5px]" height={16} src={MARK} width={16} />
            <span>Nectar</span>
            <span className="ml-auto font-mono tabular-nums">{mmss(seconds)}</span>
            <span aria-hidden className="text-white/40">
              &#9662;
            </span>
          </button>
        ) : (
          <>
            <div className="flex items-center gap-2 px-1 pb-2 pt-0.5">
              <img alt="Nectar" className="h-6 w-6 rounded-[8px]" height={24} src={MARK} width={24} />
              <span className="text-[13px] font-bold text-white">Nectar</span>

              <span className="ml-auto flex items-center gap-0.5 rounded-full bg-white/10 p-0.5">
                {(["workspace", "insights", "customize"] as const).map((name) => (
                  <button
                    key={name}
                    aria-pressed={tab === name}
                    className={`rounded-full px-2.5 py-1 text-[10px] font-semibold capitalize transition-colors ${
                      tab === name ? "bg-white text-[#141414]" : "text-white/60 hover:text-white"
                    }`}
                    onClick={() => setTab(name)}
                    type="button"
                  >
                    {name}
                  </button>
                ))}
              </span>

              {showPill && (
                <button
                  aria-label="Collapse the demo to the notch pill"
                  className="rounded-full p-1 text-white/45 transition-colors hover:bg-white/10 hover:text-white"
                  onClick={() => setCollapsed(true)}
                  type="button"
                >
                  <span aria-hidden className="text-[11px]">
                    &#9652;
                  </span>
                </button>
              )}
            </div>

            {tab === "workspace" && (
              <div className="grid min-h-[190px] grid-cols-2 gap-1.5 md:grid-cols-4">
                {CARDS.filter((card) => visible[card.id]).map((card) => (
                  <div
                    key={card.id}
                    className="flex min-h-[180px] flex-col rounded-[18px] p-2"
                    style={{ background: card.colour }}
                  >
                    {card.id === "tasks" && (
                      <>
                        <div className="flex items-baseline justify-between text-[9.5px] font-bold uppercase tracking-[0.14em]">
                          <span>Tasks</span>
                          <span className="opacity-60">{open} open</span>
                        </div>
                        <form
                          onSubmit={(event) => {
                            event.preventDefault();
                            addTask();
                          }}
                        >
                          <input
                            aria-label="Add a task"
                            className="mt-1.5 w-full rounded-md bg-white/70 px-2 py-1 text-[11px] outline-none placeholder:text-inkSolid/40 focus:bg-white"
                            onChange={(event) => setDraft(event.target.value)}
                            placeholder="+ Add a task, press Enter"
                            value={draft}
                          />
                        </form>
                        <ul className="mt-1.5 space-y-1">
                          {tasks.map((task) => (
                            <li key={task.id}>
                              <button
                                aria-pressed={task.done}
                                className="flex w-full items-center gap-1.5 text-left text-[11px] leading-tight"
                                onClick={() => toggleTask(task.id)}
                                type="button"
                              >
                                <span
                                  className={`mt-px h-3 w-3 shrink-0 rounded-full border border-inkSolid/50 ${
                                    task.done ? "bg-inkSolid" : "bg-white/40"
                                  }`}
                                />
                                <span className={task.done ? "text-inkSolid/45 line-through" : ""}>
                                  {task.text}
                                </span>
                              </button>
                            </li>
                          ))}
                        </ul>
                      </>
                    )}
                    {card.id === "timer" && (
                      <>
                        <div className="text-[9.5px] font-bold uppercase tracking-[0.14em]">Focus timer</div>
                        <div className="mt-2 font-mono text-[26px] font-bold leading-none tabular-nums">
                          {mmss(seconds)}
                        </div>
                        <div className="mt-1 text-[9.5px] opacity-70">
                          {seconds === 0 ? "Chime — session done" : running ? "Running" : "Paused"} ·{" "}
                          {minutes} min
                        </div>
                        <div className="mt-auto flex items-center gap-1 pt-2">
                          <button
                            aria-label="Take five minutes off the session"
                            className="rounded-full bg-white/55 px-1.5 py-0.5 text-[9.5px] font-semibold hover:bg-white"
                            onClick={() => setSeconds((s) => Math.max(0, s - 300))}
                            type="button"
                          >
                            &minus;5
                          </button>
                          <button
                            className="rounded-full bg-inkSolid px-2.5 py-0.5 text-[9.5px] font-semibold text-white hover:bg-black"
                            onClick={() => setRunning((value) => !value)}
                            type="button"
                          >
                            {running ? "Pause" : "Start"}
                          </button>
                          <button
                            aria-label="Add five minutes to the session"
                            className="rounded-full bg-white/55 px-1.5 py-0.5 text-[9.5px] font-semibold hover:bg-white"
                            onClick={() => setSeconds((s) => s + 300)}
                            type="button"
                          >
                            +5
                          </button>
                          <button
                            className="ml-auto text-[9px] uppercase tracking-[0.1em] underline decoration-dotted opacity-60 hover:opacity-100"
                            onClick={() => pickMinutes(minutes)}
                            type="button"
                          >
                            reset
                          </button>
                        </div>
                      </>
                    )}

                    {card.id === "notes" && (
                      <>
                        <div className="flex items-baseline justify-between text-[9.5px] font-bold uppercase tracking-[0.14em]">
                          <span>Notepad</span>
                          <span className="opacity-60">autosave</span>
                        </div>
                        <textarea
                          aria-label="A scratch note"
                          className="mt-1.5 h-full min-h-[104px] w-full resize-none rounded-md bg-white/65 p-1.5 text-[11px] leading-snug outline-none focus:bg-white"
                          onChange={(event) => setNote(event.target.value)}
                          value={note}
                        />
                        <div className="mt-1 flex items-center justify-between text-[9px] uppercase tracking-[0.1em] opacity-60">
                          <span>{words} words</span>
                          <span>Saved</span>
                        </div>
                      </>
                    )}

                    {card.id === "events" && (
                      <>
                        <div className="flex items-baseline justify-between text-[9.5px] font-bold uppercase tracking-[0.14em]">
                          <span>Events</span>
                          <span className="opacity-60">today</span>
                        </div>
                        <ul className="mt-1.5 space-y-1.5">
                          {EVENTS.map((event) => (
                            <li key={event.time} className="flex items-start gap-1.5">
                              <span className="font-mono text-[9.5px] leading-4 opacity-70">{event.time}</span>
                              <span className="min-w-0 flex-1">
                                <span className="block truncate text-[11px] font-semibold leading-4">
                                  {event.title}
                                </span>
                                <span className="block truncate text-[9.5px] opacity-60">{event.meta}</span>
                              </span>
                              <button
                                aria-label={`Ring a reminder for ${event.title}`}
                                aria-pressed={bells.includes(event.time)}
                                className={`rounded-full px-1 text-[11px] leading-4 ${
                                  bells.includes(event.time) ? "bg-inkSolid text-white" : "bg-white/50"
                                }`}
                                onClick={() => toggleBell(event.time)}
                                type="button"
                              >
                                &#9834;
                              </button>
                            </li>
                          ))}
                        </ul>
                        <p className="mt-auto pt-2 text-[9px] uppercase tracking-[0.1em] opacity-55">
                          One linked calendar
                        </p>
                      </>
                    )}
                  </div>
                ))}
              </div>
            )}
            {tab === "insights" && (
              <div className="grid gap-1.5 md:grid-cols-[0.9fr_1.4fr]">
                <div className="flex min-h-[180px] flex-col rounded-[18px] p-2.5" style={{ background: MINT }}>
                  <div className="flex items-baseline justify-between text-[9.5px] font-bold uppercase tracking-[0.14em]">
                    <span>Insights</span>
                    <span className="opacity-60">7 days</span>
                  </div>
                  <div className="mt-2 flex items-end gap-2">
                    <span className="text-[38px] font-bold leading-none">{total}</span>
                    <span className="pb-1 text-[10px] leading-tight opacity-70">tasks<br />done</span>
                  </div>
                  <p className="mt-1 text-[10px] leading-snug opacity-75">
                    Ahead of last week by 6. Thursday was the loudest day.
                  </p>
                  <p className="mt-auto pt-2 text-[9px] uppercase tracking-[0.1em] opacity-55">
                    4 active days · 7 h focused
                  </p>
                </div>

                <div className="min-h-[180px] rounded-[18px] p-2.5" style={{ background: LILAC }}>
                  <div className="flex items-center justify-between text-[9.5px] font-bold uppercase tracking-[0.14em]">
                    <span>The week, two ways</span>
                    <span className="flex rounded-full bg-white/50 p-0.5">
                      {(["tasks", "focus"] as const).map((key) => (
                        <button
                          key={key}
                          aria-pressed={series === key}
                          className={`rounded-full px-2 py-0.5 text-[9px] capitalize ${
                            series === key ? "bg-inkSolid text-white" : "text-inkSolid/70"
                          }`}
                          onClick={() => setSeries(key)}
                          type="button"
                        >
                          {key}
                        </button>
                      ))}
                    </span>
                  </div>

                  <div className="mt-2 flex h-[86px] items-end gap-1">
                    {data.values.map((value, index) => (
                      <div key={`${series}-${index}`} className="flex flex-1 flex-col items-center gap-1">
                        <span className="text-[8px] tabular-nums opacity-60">{value}</span>
                        <span
                          className="w-full rounded-t-[3px] bg-inkSolid"
                          style={{ height: `${Math.max(4, (value / peak) * 62)}px` }}
                        />
                        <span className="text-[8px] uppercase opacity-55">{DAYS[index]}</span>
                      </div>
                    ))}
                  </div>

                  <p className="mt-1 text-[9px] uppercase tracking-[0.1em] opacity-55">
                    {data.label} per day · drawn from SQLite
                  </p>
                </div>
              </div>
            )}

            {tab === "customize" && (
              <div className="grid gap-1.5 md:grid-cols-3">
                <div className="rounded-[18px] p-2.5" style={{ background: MINT }}>
                  <div className="text-[9.5px] font-bold uppercase tracking-[0.14em]">General</div>
                  <ToggleRow label="Hover to open" onChange={setHover} value={hover} />
                  <ToggleRow label="Notch pill" onChange={setShowPill} value={showPill} />
                  <p className="mt-2 text-[9.5px] leading-snug opacity-70">
                    Off keeps Nectar out of sight: the shortcut or the top edge opens it.
                  </p>
                  <span className="mt-2 inline-flex items-center gap-1 rounded-md bg-white/50 px-1.5 py-0.5 text-[9.5px] font-semibold">
                    Alt + N
                  </span>
                </div>

                <div className="rounded-[18px] p-2.5" style={{ background: LILAC }}>
                  <div className="text-[9.5px] font-bold uppercase tracking-[0.14em]">Focus</div>
                  <p className="mt-1.5 text-[9.5px] leading-snug opacity-70">Session length</p>
                  <div className="mt-1 flex gap-1">
                    {[15, 25, 50].map((value) => (
                      <button
                        key={value}
                        aria-pressed={minutes === value}
                        className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                          minutes === value ? "bg-inkSolid text-white" : "bg-white/55 hover:bg-white"
                        }`}
                        onClick={() => pickMinutes(value)}
                        type="button"
                      >
                        {value} min
                      </button>
                    ))}
                  </div>
                  <ToggleRow label="Completion chime" onChange={setChime} value={chime} />
                  <ToggleRow label="Expiry tray" onChange={setTray} value={tray} />
                </div>

                <div className="rounded-[18px] p-2.5" style={{ background: SKY }}>
                  <div className="text-[9.5px] font-bold uppercase tracking-[0.14em]">Customize workspace</div>
                  <p className="mt-1 text-[9.5px] opacity-70">Toggle to show or hide.</p>
                  {CARDS.map((card) => (
                    <ToggleRow
                      key={card.id}
                      label={card.name}
                      onChange={(value) => setVisible((state) => ({ ...state, [card.id]: value }))}
                      value={visible[card.id]}
                    />
                  ))}
                  <p className="mt-1 text-[9px] uppercase tracking-[0.1em] opacity-55">
                    Hidden cards keep their content
                  </p>
                </div>
              </div>
            )}
          </>
        )}
      </div>

      <p className="mt-2 text-center text-[10px] font-medium uppercase tracking-[0.14em] text-muted">
        A live demo &mdash; type, tick, start the timer, hide a card. Nothing you do here is saved.
      </p>
    </div>
  );
}

/** The app's own switch: a small track that fills in when it is on. */
function ToggleRow({
  label,
  value,
  onChange
}: {
  label: string;
  value: boolean;
  onChange: (next: boolean) => void;
}) {
  return (
    <div className="mt-1.5 flex items-center justify-between gap-2">
      <span className="text-[10px] leading-tight">{label}</span>
      <button
        aria-checked={value}
        aria-label={label}
        className={`relative h-4 w-7 shrink-0 rounded-full border border-inkSolid/45 transition-colors ${
          value ? "bg-inkSolid" : "bg-white/55"
        }`}
        onClick={() => onChange(!value)}
        role="switch"
        type="button"
      >
        <span
          className={`absolute top-[2px] h-[10px] w-[10px] rounded-full bg-white transition-all ${
            value ? "left-[14px]" : "left-[3px]"
          }`}
        />
      </button>
    </div>
  );
}
