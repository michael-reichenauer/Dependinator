
export const clickHandler = (onSingleClick, onDoubleClick) => {
    let clickTimeout = null
    let clicks = 0

    return () => {
        clearTimeout(clickTimeout)
        clicks++
        //console.log('click #', clicks)
        if (clicks === 1) {
            clickTimeout = setTimeout(() => {
                // single click
                clearTimeout(clickTimeout)
                clicks = 0
                onSingleClick()
            }, 300);
        } else if (clicks === 2) {
            // Double click
            // console.log('click time ', (performance.now() - this.clickTime).toFixed(1))
            clicks = 0
            onDoubleClick()
        }
    }
}