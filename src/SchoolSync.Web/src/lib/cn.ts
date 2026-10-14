import { clsx, type ClassValue } from 'clsx';
import { extendTailwindMerge } from 'tailwind-merge';

// tailwind-merge must know the token-named font sizes, or `text-h2 text-text-secondary` would be
// merged as two conflicting colour classes and one silently dropped.
const merge = extendTailwindMerge({
  extend: {
    classGroups: {
      'font-size': [{ text: ['display', 'h1', 'h2', 'body-lg', 'body', 'body-sm', 'caption', 'micro-label'] }],
    },
  },
});

export function cn(...inputs: ClassValue[]) {
  return merge(clsx(inputs));
}
