import { cloneElement, useId, type ReactElement } from "react";

type ControlProps = { id?: string; required?: boolean; "aria-describedby"?: string; "aria-invalid"?: boolean };

export function Field({ label, hint, error, required, id, children }: {
  label: string;
  hint?: string;
  error?: string;
  required?: boolean;
  id?: string;
  children: ReactElement<ControlProps>;
}) {
  const autoId = useId();
  const controlId = id ?? autoId;
  const hintId = hint ? `${controlId}-hint` : undefined;
  const errorId = error ? `${controlId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;
  const control = cloneElement(children, {
    id: controlId,
    "aria-describedby": describedBy,
    "aria-invalid": error ? true : undefined,
    ...(required !== undefined && { required }),
  });
  return (
    <div className={`ez-field${error ? " ez-field--error" : ""}`}>
      <label className="ez-field__label" htmlFor={controlId}>
        {label}
        {required && <span aria-hidden="true" className="ez-field__req"> *</span>}
      </label>
      {control}
      {hint && <p id={hintId} className="ez-field__hint">{hint}</p>}
      {error && <p id={errorId} className="ez-field__error" role="alert">{error}</p>}
    </div>
  );
}
