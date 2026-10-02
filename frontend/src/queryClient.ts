import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "./api/client";

/**
 * Sorgu varsayılanları tek kaynakta. Gerekçeler:
 *
 * - 4xx'te retry YOK: istek hatalıdır (bulunamadı, yetkisiz, doğrulama), tekrarı da
 *   hatalı olur. Varsayılan 3 deneme kullanıcıyı ~7 sn boyunca "Yükleniyor"da tutup
 *   sonunda aynı hatayı gösteriyordu.
 * - 5xx ve ağ hataları geçici olabilir → en fazla 2 deneme.
 * - staleTime 30 sn: sekmeye her dönüşte tüm ekranın yeniden yüklenmesini önler.
 *   Sıfır olduğunda her odaklanma bir refetch dalgası tetikliyordu.
 */
export const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      mutations: { retry: false },
      queries: {
        staleTime: 30_000,
        retry: (failureCount: number, error: unknown) => {
          const isClientError =
            error instanceof ApiError && error.status >= 400 && error.status < 500;
          return isClientError ? false : failureCount < 2;
        },
      },
    },
  });
