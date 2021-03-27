import { useCallback, useRef } from "react";

const useLongPress = (
    onLongPress,
    { shouldPreventDefault = false, delay = 800 } = {}
) => {

    const timeout = useRef();
    const target = useRef();
    const isMouseDown = useRef(false);

    const start = useCallback(event => {
        if (shouldPreventDefault) {
            event.target?.addEventListener("touchend", preventDefault, { passive: false });
            target.current = event.target;
        }
        timeout.current = setTimeout(() => onLongPress(event), delay);
    },
        [onLongPress, delay, shouldPreventDefault]
    );

    const clear = useCallback((event) => {
        clearTimeout(timeout.current);
        if (shouldPreventDefault) {
            target.current?.removeEventListener("touchend", preventDefault);
        }
    },
        [shouldPreventDefault]
    );


    return {
        onMouseDown: e => {
            isMouseDown.current = true;
            console.log('onMouseDown', e.target);
            start(e);
        },

        onMouseUp: e => {
            isMouseDown.current = false; console.log('onMouseUp');
            clear(e);
        },

        onTouchStart: e => {
            console.log('onTouchStart');
            start(e);
        },

        onMouseMove: e => {
            if (isMouseDown.current) {
                console.log('onMouseMove', isMouseDown.current);
                clear(e);
            }
        },

        onTouchMove: e => {
            console.log('onTouchMove'); clear(e);
        },



        onMouseLeave: e => {
            isMouseDown.current = false; console.log('onMouseLeave');
            clear(e);
        },

        onTouchEnd: e => {
            console.log('touch end');
            clear(e);
        },
    };
};

const isTouchEvent = event => {
    return "touches" in event;
};

const preventDefault = event => {
    if (!isTouchEvent(event)) return;

    if (event.touches.length < 2 && event.preventDefault) {
        event.preventDefault();
    }
};

export default useLongPress;