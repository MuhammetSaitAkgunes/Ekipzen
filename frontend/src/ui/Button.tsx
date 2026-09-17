import type { AnchorHTMLAttributes, ButtonHTMLAttributes } from "react";
import { Link } from "react-router-dom";

export type ButtonProps = {
  variant?: "primary" | "secondary" | "ghost" | "danger";
  size?: "sm" | "md" | "lg";
  block?: boolean;
};

const classes = ({ variant = "primary", size = "md", block }: ButtonProps, extra?: string) =>
  ["ez-btn", `ez-btn--${variant}`, `ez-btn--${size}`, block && "ez-btn--block", extra].filter(Boolean).join(" ");

export function Button({ variant, size, block, className, type = "button", ...rest }: ButtonProps & ButtonHTMLAttributes<HTMLButtonElement>) {
  return <button type={type} className={classes({ variant, size, block }, className)} {...rest} />;
}

export function ButtonLink({ variant, size, block, className, to, ...rest }: ButtonProps & { to: string } & Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "href">) {
  return <Link to={to} className={classes({ variant, size, block }, className)} {...rest} />;
}
