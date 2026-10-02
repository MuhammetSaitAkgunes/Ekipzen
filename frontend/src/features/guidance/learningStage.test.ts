import { expect, test } from "vitest";
import { learningStage } from "./learningStage";

test.each([
  [false, null, 0, "start"], [true, null, 0, "read"], [true, false, 0, "practice-review"],
  [false, true, 0, "practice-correct"], [false, null, 1, "operation-recorded"], [true, false, 3, "operation-recorded"],
  [false, true, 1, "practice-and-operation"],
] as const)("inceleme %s yanıt %s işlem %s ayrı aşama %s üretir", (read, correct, count, key) => {
  expect(learningStage("payroll", "approval", read, correct, count).key).toBe(key);
});
test("salt okuma adımı için yapay işlem kanıtı aranmaz", () => {
  const result = learningStage("attendance", "period-summary", true, true, 0);
  expect(result.key).toBe("reading-recorded"); expect(result.next).toContain("yapay işlem kanıtı beklenmez");
});
