import { Tweenable } from "shifty"


export const zoomAndMoveShowTotalDiagram = (canvas) => {
    if (!canvas.selection.all.isEmpty()) {
        // Deselect items, since zooming with selected figures is slow
        canvas.selection.getAll().each((i, f) => f.unselect())
        canvas.selection.clear()
    }

    moveToShowTotalDiagram(canvas, () => zoomToShowTotalDiagram(canvas))
}

const moveToShowTotalDiagram = (canvas, done) => {
    const area = canvas.getScrollArea()

    const { x, y, w, h } = canvas.getFiguresRect()

    const zoom = canvas.zoomFactor
    const fc = { x: (x + w / 2) / zoom, y: (y + h / 2) / zoom }
    const cc = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    const tp = { x: fc.x - cc.x, y: fc.y - cc.y }

    let tweenable = new Tweenable()
    tweenable.tween({
        from: { x: area.scrollLeft(), y: area.scrollTop() },
        to: { x: (tp.x), y: tp.y },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            area.scrollLeft(state.x)
            area.scrollTop(state.y)
        },
        finish: state => {
            if (done != null) {
                done()
            }
        }
    })
}

const zoomToShowTotalDiagram = (canvas, done) => {
    const area = canvas.getScrollArea()

    const { x, y, w, h } = canvas.getFiguresRect()

    const fc = { x: x + w / 2, y: y + h / 2 }
    const cc = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    const targetZoom = Math.max(1, w / (canvas.getWidth() - 100), h / (canvas.getHeight() - 100))

    let tweenable = new Tweenable()
    tweenable.tween({
        from: { 'zoom': canvas.zoomFactor },
        to: { 'zoom': targetZoom },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            canvas.setZoom(state.zoom, false)

            // Adjust scroll to center, since canvas zoom lacks zoom at center point
            const tp = { x: fc.x - cc.x * state.zoom, y: fc.y - cc.y * state.zoom }
            area.scrollLeft((tp.x) / state.zoom)
            area.scrollTop((tp.y) / state.zoom)
        },
        finish: state => {
            if (done != null) {
                done()
            }
        }
    })
}

