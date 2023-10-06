export function listenToWindowResize(dotNetHelper) {

    function resizeEventHandler() {
        dotNetHelper.invokeMethodAsync('WindowResizeEvent');
    }

    window.addEventListener("resize", resizeEventHandler);

    dotNetHelper.invokeMethodAsync('WindowResizeEvent');
}

export function getBoundingRectangle(element, parm) {
    return element.getBoundingClientRect();
}

export function getWindowSizeDetails(parm) {

    var e = window, a = 'inner';

    if (!('innerWidth' in window)) {
        a = 'client';
        e = document.documentElement || document.body;
    }

    let windowSize =
    {
        innerWidth: e[a + 'Width'],
        innerHeight: e[a + 'Height'],
        screenWidth: window.screen.width,
        screenHeight: window.screen.height
    };

    return windowSize;
}
