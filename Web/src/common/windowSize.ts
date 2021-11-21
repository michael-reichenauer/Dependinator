import { useEffect, useState } from "react";

// returns and monitors current window size and reacts on resizes
export default function useWindowSize() {
  const [dimensions, setDimensions] = useState({
    height: window.innerHeight,
    width: window.innerWidth,
  });

  // @ts-ignore
  useEffect(() => {
    const debouncedHandleResize = debounce(() => {
      setDimensions({
        height: window.innerHeight,
        width: window.innerWidth,
      });
    }, 300);

    window.addEventListener("resize", debouncedHandleResize);
    window.addEventListener("orientationchange ", debouncedHandleResize);

    return (_: any) => {
      window.removeEventListener("resize", debouncedHandleResize);
      window.removeEventListener("orientationchange", debouncedHandleResize);
    };
  });

  return [dimensions];
}

function debounce(fn: any, timeoutMs: number) {
  let timer: any;
  return (_: any) => {
    clearTimeout(timer);
    timer = setTimeout((_: any) => {
      timer = null;
      // @ts-ignore
      fn.apply(this, arguments);
      // @ts-ignore
    }, timeoutMs);
  };
}
