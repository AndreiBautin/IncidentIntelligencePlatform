#!/usr/bin/env node
// Fails on any High/Critical npm advisory that isn't in the accepted list below.
// Each accepted ID must be documented in docs/SECURITY.md with why it can't be fixed yet.
//
// Usage: npm audit --json | node scripts/ci/npm-audit-gate.mjs

const ACCEPTED_GHSA_IDS = new Set([
  // Transitive postcss bundled inside next/node_modules/postcss. Build-time only
  // (compiles this repo's own trusted CSS/Tailwind); no runtime attack surface.
  // No fix available without a Next.js 16 major upgrade. See docs/SECURITY.md.
  "GHSA-qx2v-qp2m-jg93",
  "GHSA-6g55-p6wh-862q",
  "GHSA-fxqj-rqcc-2cmp",
  "GHSA-r28c-9q8g-f849",
]);

let input = "";
process.stdin.on("data", (chunk) => (input += chunk));
process.stdin.on("end", () => {
  let report;
  try {
    report = JSON.parse(input);
  } catch {
    console.error("npm-audit-gate: could not parse npm audit JSON output");
    process.exit(1);
  }

  let failed = false;
  for (const [name, vuln] of Object.entries(report.vulnerabilities || {})) {
    if (vuln.severity !== "high" && vuln.severity !== "critical") continue;

    const ids = (vuln.via || [])
      .map((v) => (typeof v === "object" && v.url ? v.url.split("/").pop() : null))
      .filter(Boolean);

    const allAccepted = ids.length > 0 && ids.every((id) => ACCEPTED_GHSA_IDS.has(id));
    if (!allAccepted) {
      console.error(`npm-audit-gate: unaccepted ${vuln.severity} vulnerability in "${name}":`, ids.length ? ids : vuln.via);
      failed = true;
    }
  }

  if (failed) {
    console.error("npm-audit-gate: fix the finding above, or add its GHSA ID here with a documented reason in docs/SECURITY.md.");
    process.exit(1);
  }

  console.log("npm-audit-gate: no unaccepted High/Critical vulnerabilities.");
});
