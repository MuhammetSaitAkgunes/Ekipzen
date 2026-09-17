import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { expect, test } from "vitest";
import { Badge, Button, ButtonLink, Checkbox, Field, Input, Logo, usePageMeta } from ".";

test("Button varsayılan olarak type=button ve varyant sınıfları taşır", () => {
  render(<Button variant="secondary" size="lg">Kaydet</Button>);
  const btn = screen.getByRole("button", { name: "Kaydet" });
  expect(btn).toHaveAttribute("type", "button");
  expect(btn).toHaveClass("ez-btn", "ez-btn--secondary", "ez-btn--lg");
});

test("ButtonLink router linki üretir", () => {
  render(<MemoryRouter><ButtonLink to="/iletisim">Demo iste</ButtonLink></MemoryRouter>);
  expect(screen.getByRole("link", { name: "Demo iste" })).toHaveAttribute("href", "/iletisim");
});

test("Field label, hint ve hatayı kontrole bağlar", () => {
  render(
    <Field label="E-posta" hint="İş adresiniz" error="Geçersiz adres">
      <Input type="email" />
    </Field>,
  );
  const input = screen.getByLabelText("E-posta");
  expect(input).toHaveAttribute("aria-invalid", "true");
  const describedBy = input.getAttribute("aria-describedby")!.split(" ");
  expect(describedBy).toHaveLength(2);
  expect(screen.getByRole("alert")).toHaveTextContent("Geçersiz adres");
});

test("Field verilen id'yi korur ve required'ı ezmez", () => {
  render(<Field label="Şifre" id="login-password"><Input type="password" required /></Field>);
  const input = screen.getByLabelText("Şifre");
  expect(input).toHaveAttribute("id", "login-password");
  expect(input).toBeRequired();
});

test("Checkbox etiketle erişilebilir", () => {
  render(<Checkbox>Onaylıyorum</Checkbox>);
  expect(screen.getByRole("checkbox", { name: "Onaylıyorum" })).toBeInTheDocument();
});

test("Badge tonu ve Logo erişilebilir adı", () => {
  render(<><Badge tone="success">Aktif</Badge><Logo /></>);
  expect(screen.getByText("Aktif")).toHaveClass("ez-badge--success");
  expect(screen.getByRole("img", { name: "Ekipzen" })).toBeInTheDocument();
});

function MetaProbe() {
  usePageMeta("Fiyatlandırma", "Paketler");
  return null;
}

test("usePageMeta başlık ve açıklamayı yazar", () => {
  render(<MetaProbe />);
  expect(document.title).toBe("Fiyatlandırma · Ekipzen");
  expect(document.querySelector('meta[name="description"]')).toHaveAttribute("content", "Paketler");
});
