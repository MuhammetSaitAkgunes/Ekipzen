import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

export async function openOperationGuidance(label = "Gerçek işlemde kullandığım rehber sürümü", scope?: HTMLElement) {
  const view = scope ? within(scope) : screen;
  await waitFor(() => {
    if (!view.queryByRole("group", { name: label }) && !view.queryByText(label, { exact: true })) {
      throw new Error(`İşlem rehberi henüz hazır değil: ${label}`);
    }
  });
  const group = view.queryByRole("group", { name: label });
  if (group) await userEvent.click(within(group).getByRole("button", { name: /^Rehber/ }));
  const panel = await waitFor(() => (
    view.queryByText(label, { exact: true }) ?? view.getByText(label + " · seçildi", { exact: true })
  ).closest("aside")!);
  return panel;
}

export async function chooseOperationGuidance(label = "Gerçek işlemde kullandığım rehber sürümü", scope?: HTMLElement) {
  const panel = await openOperationGuidance(label, scope);
  await userEvent.click(await within(panel).findByRole("button", { name: "Bu rehber sürümünü işlemime bağla" }));
  await within(panel).findByRole("region", { name: "Seçilen adımın açıklaması" });
  const dialog = panel.closest("dialog");
  if (dialog) await userEvent.click(within(dialog).getByRole("button", { name: "Kapat" }));
}
