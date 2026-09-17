import { forwardRef, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";

const join = (...names: (string | undefined)[]) => names.filter(Boolean).join(" ");

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...rest }, ref) => <input ref={ref} className={join("ez-input", className)} {...rest} />,
);
Input.displayName = "Input";

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  ({ className, ...rest }, ref) => <textarea ref={ref} className={join("ez-input", "ez-textarea", className)} {...rest} />,
);
Textarea.displayName = "Textarea";

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(
  ({ className, ...rest }, ref) => <select ref={ref} className={join("ez-input", "ez-select", className)} {...rest} />,
);
Select.displayName = "Select";

export function Checkbox({ children, className, ...rest }: InputHTMLAttributes<HTMLInputElement> & { children: ReactNode }) {
  return (
    <label className={join("ez-check", className)}>
      {/* type en son yayılır: {...rest} içinde (kazara ya da başka bir yerden) bir
          `type` gelirse bile bu her zaman checkbox olarak kalmalıdır. */}
      <input className="ez-check__box" {...rest} type="checkbox" />
      <span className="ez-check__label">{children}</span>
    </label>
  );
}
