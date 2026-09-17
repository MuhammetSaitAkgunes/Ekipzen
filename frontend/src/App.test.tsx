import { render, screen } from "@testing-library/react";
import App from "./App";

test("oturum yokken kök adres landing sayfasını açar", async () => {
  localStorage.clear();
  render(<App />);
  expect(await screen.findByRole("heading", { level: 1 })).toHaveTextContent("Ekibinizi tek yerden");
});
