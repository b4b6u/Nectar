import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "You're in — Nectar",
  robots: { index: false, follow: false }
};

type CheckoutState =
  | { kind: "paid"; email: string | null }
  | { kind: "unpaid"; status: string }
  | { kind: "unverified" }
  | { kind: "missing" };

/**
 * Confirm the payment before handing over the installer, so a guessed or shared
 * /success?session_id= URL cannot stand in for a purchase. Uses the Stripe REST API directly —
 * no SDK, and the secret key never reaches the browser.
 */
async function verifySession(sessionId?: string): Promise<CheckoutState> {
  if (!sessionId) return { kind: "missing" };

  const key = process.env.STRIPE_SECRET_KEY;
  if (!key) return { kind: "unverified" };

  const response = await fetch(
    `https://api.stripe.com/v1/checkout/sessions/${encodeURIComponent(sessionId)}`,
    {
      headers: { Authorization: `Bearer ${key}` },
      cache: "no-store"
    }
  );

  if (!response.ok) return { kind: "unverified" };

  const session = (await response.json()) as {
    payment_status?: string;
    status?: string;
    customer_details?: { email?: string | null };
  };

  if (session.payment_status === "paid") {
    return { kind: "paid", email: session.customer_details?.email ?? null };
  }

  return { kind: "unpaid", status: session.payment_status ?? session.status ?? "unpaid" };
}

export default async function SuccessPage({
  searchParams
}: {
  searchParams: { [key: string]: string | string[] | undefined };
}) {
  const rawSession = searchParams.session_id;
  const state = await verifySession(Array.isArray(rawSession) ? rawSession[0] : rawSession);
  const unlocked = state.kind === "paid" || state.kind === "unverified";

  return (
    <main className="relative z-10 mx-auto flex min-h-screen max-w-2xl flex-col justify-center px-6 py-20">
      <p className="mb-6 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
        {unlocked ? "Order confirmed" : "Checkout"}
      </p>

      <h1 className="font-display text-4xl font-semibold sm:text-5xl">
        {unlocked ? "Your day is anchored." : "We could not confirm that payment."}
      </h1>

      <p className="mt-5 text-muted">
        {state.kind === "paid" && (
          <>
            Thanks{state.email ? `, ${state.email}` : ""} — your licence is active for a year.
            Install Nectar below; it updates itself from here on.
          </>
        )}
        {state.kind === "unverified" && (
          <>
            Payment received. This deployment has no Stripe key configured, so the receipt could
            not be checked here — keep your Stripe receipt as proof of purchase.
          </>
        )}
        {state.kind === "unpaid" && (
          <>
            Stripe reports this session as <strong>{state.status}</strong>. Nothing has been
            charged; you can start the checkout again from the home page.
          </>
        )}
        {state.kind === "missing" && (
          <>
            This page needs the <code>session_id</code> Stripe appends after checkout. Open the
            link from your receipt, or buy again from the home page.
          </>
        )}
      </p>

      {unlocked ? (
        <div className="mt-10 rounded-2xl border border-white/10 bg-white/[0.03] p-7">
          <h2 className="font-display text-xl font-semibold">1. Install the App Installer</h2>
          <p className="mt-2 text-sm text-muted">
            Downloading the <code>.appinstaller</code> file is the whole install: Windows reads it,
            fetches the signed MSIX, and keeps it up to date afterwards.
          </p>
          <a
            className="mt-5 inline-block rounded-full bg-nectar px-7 py-3.5 text-[15px] font-semibold text-[#201109] transition hover:-translate-y-px hover:bg-[#ffa970]"
            href="/downloads/nectar.appinstaller"
            download
          >
            Download Nectar
          </a>
          <p className="mt-4 text-xs text-muted">
            Windows may show &ldquo;Windows protected your PC&rdquo; or an unknown-publisher
            warning until this certificate builds SmartScreen reputation. Choose{" "}
            <strong>More info → Run anyway</strong> once.
          </p>

          <h2 className="mt-8 font-display text-xl font-semibold">2. Press Alt + N</h2>
          <p className="mt-2 text-sm text-muted">
            Nectar starts hidden by design. The notch at the top edge, the collapsed pill and the
            shortcut all open the same workspace.
          </p>
        </div>
      ) : (
        <Link
          className="mt-9 inline-block rounded-full border border-white/10 px-7 py-3.5 text-[15px] font-semibold text-cream transition hover:border-white/30"
          href="/"
        >
          Back to Nectar
        </Link>
      )}

      <p className="mt-10 text-xs text-muted">
        Licence terms are on the{" "}
        <Link className="underline decoration-white/30 underline-offset-4" href="/licence.html">
          licence page
        </Link>
        ; what Nectar stores on your PC is on the{" "}
        <Link className="underline decoration-white/30 underline-offset-4" href="/privacy.html">
          privacy page
        </Link>
        .
      </p>
    </main>
  );
}
