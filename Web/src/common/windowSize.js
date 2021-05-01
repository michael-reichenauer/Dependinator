import { useEffect, useState } from 'react';


// returns and monitors current window size and reacts on resizes
export default function useWindowSize() {
    const [dimensions, setDimensions] = useState({
        height: window.innerHeight,
        width: window.innerWidth
    })

    useEffect(() => {
        const debouncedHandleResize = debounce(() => {
            setDimensions({
                height: window.innerHeight,
                width: window.innerWidth
            })
        }, 300)

        window.addEventListener('resize', debouncedHandleResize)
        window.addEventListener('orientationchange ', debouncedHandleResize)

        return _ => {
            window.removeEventListener('resize', debouncedHandleResize)
            window.removeEventListener('orientationchange', debouncedHandleResize)
        }
    })

    return [dimensions]
}


function debounce(fn, timeoutMs) {
    let timer
    return _ => {
        clearTimeout(timer)
        timer = setTimeout(_ => {
            timer = null
            fn.apply(this, arguments)
        }, timeoutMs)
    };
}