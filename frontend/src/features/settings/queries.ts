import { guidanceOperationsKey } from "../guidance/operationQueries";
import { dailyTasksKey } from "../guidance/queries";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type SettingsDto = components["schemas"]["SettingsDto"];
export type CompanyProfileDto = components["schemas"]["CompanyProfileDto"];
export type NotificationSettingsDto = components["schemas"]["NotificationSettingsDto"];
export type UpdateCompanyProfileCommand = components["schemas"]["UpdateCompanyProfileCommand"];

export const useSettings = () =>
  useQuery({
    queryKey: ["settings"],
    queryFn: () => apiFetch<SettingsDto>("/settings"),
  });

export const useUpdateCompany = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: UpdateCompanyProfileCommand) =>
      apiFetch<CompanyProfileDto>("/settings/company", {
        method: "PUT",
        body: JSON.stringify(command),
      }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["settings"] }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }), queryClient.invalidateQueries({ queryKey: dailyTasksKey })]),
  });
};

export const useUpdateNotifications = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: components["schemas"]["UpdateNotificationSettingsCommand"]) =>
      apiFetch<NotificationSettingsDto>("/settings/notifications", {
        method: "PUT",
        body: JSON.stringify(command),
      }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["settings"] }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey })]),
  });
};

export const useUpdateSecurity = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: { twoFactorSmsEnabled: boolean }) =>
      apiFetch<{ twoFactorSmsEnabled: boolean }>("/settings/security", {
        method: "PUT",
        body: JSON.stringify(command),
      }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["settings"] }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey })]),
  });
};

export const useUploadLogo = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ file, expectedRecordToken, guidanceContentRevisionId }: { file: File; expectedRecordToken?: string; guidanceContentRevisionId?: number }) => {
      const data = new FormData();
      data.append("file", file);
      if (expectedRecordToken) data.append("expectedRecordToken", expectedRecordToken);
      if (guidanceContentRevisionId != null) data.append("guidanceContentRevisionId", String(guidanceContentRevisionId));
      return apiFetch<{ logoPath: string }>("/settings/company/logo", { method: "POST", body: data });
    },
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["settings"] }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey })]),
  });
};

export const useChangePassword = () =>
  useMutation({
    mutationFn: (command: { currentPassword: string; newPassword: string }) =>
      apiFetch<null>("/auth/change-password", {
        method: "POST",
        body: JSON.stringify(command),
      }),
  });
