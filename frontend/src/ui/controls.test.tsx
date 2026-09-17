import { render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { Checkbox } from "./controls";

test("Checkbox tip'i rest prop'larla ezilemez", () => {
  // {...rest} type'tan SONRA yayılırsa, çağıran taraf kazara (ya da bir kütüphanenin
  // varsayılan prop'u yüzünden) type geçerse checkbox artık checkbox olarak render
  // edilmez. type her zaman checkbox olarak kalmalı.
  render(
    <Checkbox name="ornek" type={"text" as never}>
      Etiket
    </Checkbox>,
  );
  expect(screen.getByRole("checkbox", { name: "Etiket" })).toBeInTheDocument();
});
