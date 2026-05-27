export function debounce<TArgs extends unknown[]>(callback: (...args: TArgs) => void, delayMs: number) {
  let timeoutId: NodeJS.Timeout | undefined;

  return (...args: TArgs) => {
    if (timeoutId) {
      clearTimeout(timeoutId);
    }

    timeoutId = setTimeout(() => {
      callback(...args);
    }, delayMs);
  };
}

