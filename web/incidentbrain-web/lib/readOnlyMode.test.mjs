import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const src = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "api.ts"), "utf8");

function isReadOnlyMode(flag) {
  return flag === "true";
}

test("api.ts exports isReadOnlyMode and uses NEXT_PUBLIC_READ_ONLY", () => {
  assert.match(src, /export function isReadOnlyMode/);
  assert.match(src, /NEXT_PUBLIC_READ_ONLY/);
  assert.match(src, /isReadOnlyMode\(process\.env\.NEXT_PUBLIC_READ_ONLY\)/);
});

test("only the exact string true turns the dashboard read-only", () => {
  assert.equal(isReadOnlyMode("true"), true);
  assert.equal(isReadOnlyMode("TRUE"), false);
  assert.equal(isReadOnlyMode("1"), false);
  assert.equal(isReadOnlyMode(undefined), false);
  assert.equal(isReadOnlyMode(""), false);
});
