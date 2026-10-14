import { X } from 'lucide-react';
import { create } from 'zustand';
import { cn } from '@/lib/cn';

// _shared.md toast pattern (the mockup's toastHost): success/failure feedback for every mutation.
// Announced through a polite live region (errors: assertive) so screen-reader users hear the outcome.
interface Toast { id: number; message: string; tone: 'success' | 'error' }
interface ToastState { toasts: Toast[]; push: (message: string, tone?: Toast['tone']) => void; dismiss: (id: number) => void }

let next = 1;
export const useToasts = create<ToastState>((set) => ({
  toasts: [],
  push: (message, tone = 'success') => {
    const id = next++;
    set((s) => ({ toasts: [...s.toasts, { id, message, tone }] }));
    setTimeout(() => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })), 5000);
  },
  dismiss: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));

export const toast = {
  success: (m: string) => useToasts.getState().push(m, 'success'),
  error: (m: string) => useToasts.getState().push(m, 'error'),
};

export function ToastHost() {
  const { toasts, dismiss } = useToasts();
  return (
    <>
      <div aria-live="polite" className="sr-only">{toasts.filter((t) => t.tone === 'success').map((t) => t.message).join('. ')}</div>
      <div aria-live="assertive" className="sr-only">{toasts.filter((t) => t.tone === 'error').map((t) => t.message).join('. ')}</div>
      <div className="pointer-events-none fixed inset-x-4 bottom-4 z-[60] flex flex-col items-end gap-2 sm:left-auto">
        {toasts.map((t) => (
          <div key={t.id} data-testid="toast" className={cn('pointer-events-auto flex max-w-sm items-start gap-3 rounded-lg px-4 py-3 text-body-sm shadow-card',
            t.tone === 'success' ? 'bg-text-primary text-white' : 'bg-error text-white')}>
            <p className="flex-1">{t.message}</p>
            <button type="button" aria-label="Dismiss notification" onClick={() => dismiss(t.id)} className="rounded-sm text-white/90 hover:text-white"><X aria-hidden className="size-4" /></button>
          </div>
        ))}
      </div>
    </>
  );
}
