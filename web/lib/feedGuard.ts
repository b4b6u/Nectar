/**
 * Feed guard: request identity, rate limiting, ETags and structured logging.
 *
 * Deliberately dependency-free and free of Next.js types so the route can import it and a plain
 * `tsc` + `node --test` suite can exercise it without a server.
 */

export interface RateLimitDecision {
  allowed: boolean;
  remaining: number;
  retryAfterSeconds: number;
}

/**
 * Per-key sliding window. Cheap, allocation-light and bounded: the map is capped so a spray of
 * spoofed client keys cannot grow memory without limit.
 *
 * This is per-instance state. On Vercel each serverless instance keeps its own counters, which is
 * enough to stop a single client hammering one instance; global protection is what the CDN cache
 * (s-maxage) and, for anything larger, Vercel's WAF or a shared Redis limiter are for.
 */
export class RateLimiter {
  private readonly hits = new Map<string, number[]>();
  private readonly maxKeys: number;

  constructor(
    private readonly limit: number,
    private readonly windowMs: number,
    maxKeys = 5000
  ) {
    this.maxKeys = maxKeys;
  }

  take(key: string, now = Date.now()): RateLimitDecision {
    const cutoff = now - this.windowMs;
    const recent = (this.hits.get(key) ?? []).filter((stamp) => stamp > cutoff);

    if (recent.length >= this.limit) {
      this.hits.set(key, recent);
      const oldest = recent[0] ?? now;
      const retryMs = Math.max(0, oldest + this.windowMs - now);
      return {
        allowed: false,
        remaining: 0,
        retryAfterSeconds: Math.max(1, Math.ceil(retryMs / 1000))
      };
    }

    recent.push(now);
    this.hits.set(key, recent);

    if (this.hits.size > this.maxKeys) {
      const oldestKey = this.hits.keys().next().value;
      if (oldestKey !== undefined) {
        this.hits.delete(oldestKey);
      }
    }

    return {
      allowed: true,
      remaining: Math.max(0, this.limit - recent.length),
      retryAfterSeconds: 0
    };
  }
}

/** Trusted first hop of the forwarded chain, then the platform header, then a shared bucket. */
export function clientKey(request: Request): string {
  const forwarded = request.headers.get("x-forwarded-for");
  if (forwarded) {
    const first = forwarded.split(",")[0]?.trim();
    if (first) {
      return first;
    }
  }

  const realIp = request.headers.get("x-real-ip");
  return realIp && realIp.trim() ? realIp.trim() : "unknown";
}

/** Stable, short ETag input. FNV-1a is enough: this identifies a feed body, it is not a signature. */
export function fingerprint(value: string): string {
  let hash = 0x811c9dc5;
  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }

  return hash.toString(16).padStart(8, "0");
}

export function newRequestId(): string {
  const cryptoApi = globalThis.crypto;
  if (cryptoApi && typeof cryptoApi.randomUUID === "function") {
    return cryptoApi.randomUUID();
  }

  return `req-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}
