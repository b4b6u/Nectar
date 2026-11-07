// Nectar landing page — no dependencies, no build step.

// Replace this one value with your repository URL after you create the GitHub repo.
const REPO_URL = "https://github.com/";

const html = document.documentElement;
html.classList.add("js");

// Point every "GitHub / Read the source" link at the repository.
document.querySelectorAll("[data-repo]").forEach((link) => {
  if (REPO_URL && REPO_URL !== "https://github.com/") {
    link.href = REPO_URL;
    if (link.tagName === "A" && !link.hasAttribute("target")) {
      link.target = "_blank";
      link.rel = "noreferrer";
    }
  }
});

// The install snippet shows the same URL.
document.querySelectorAll(".repo-url").forEach((node) => {
  if (REPO_URL && REPO_URL !== "https://github.com/") {
    node.textContent = REPO_URL;
  }
});

// Sticky bar picks up a shadow once the page is scrolled.
const pillbar = document.querySelector(".pillbar");
const onScroll = () => pillbar && pillbar.classList.toggle("is-stuck", window.scrollY > 12);
onScroll();
window.addEventListener("scroll", onScroll, { passive: true });

// Mobile navigation.
const navToggle = document.getElementById("navToggle");
const nav = document.getElementById("nav");
if (navToggle && nav) {
  navToggle.addEventListener("click", () => {
    const open = nav.classList.toggle("is-open");
    navToggle.setAttribute("aria-expanded", String(open));
  });
  nav.addEventListener("click", (event) => {
    if (event.target instanceof HTMLAnchorElement) {
      nav.classList.remove("is-open");
      navToggle.setAttribute("aria-expanded", "false");
    }
  });
}

// Live notch demo: collapse the hero screenshot back into the pill.
const notchDemo = document.getElementById("notchDemo");
const heroShell = document.getElementById("heroShell");
if (notchDemo && heroShell) {
  const heroScreen = heroShell.querySelector(".shell__screen");
  notchDemo.addEventListener("click", () => {
    const collapsed = heroShell.classList.toggle("is-collapsed");
    notchDemo.setAttribute("aria-pressed", String(!collapsed));
    notchDemo.textContent = collapsed ? "Open the notch" : "Collapse the notch";
    if (heroScreen) heroScreen.setAttribute("aria-hidden", String(collapsed));
  });
}

// Scroll reveals, with a static fallback when motion is reduced.
const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const revealables = document.querySelectorAll(".reveal");

if (reduceMotion || !("IntersectionObserver" in window)) {
  revealables.forEach((node) => node.classList.add("in"));
} else {
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry, index) => {
        if (!entry.isIntersecting) return;
        const node = entry.target;
        const delay = Math.min(index * 70, 280);
        setTimeout(() => node.classList.add("in"), delay);
        observer.unobserve(node);
      });
    },
    { rootMargin: "0px 0px -8% 0px", threshold: 0.08 }
  );
  revealables.forEach((node) => observer.observe(node));
}
