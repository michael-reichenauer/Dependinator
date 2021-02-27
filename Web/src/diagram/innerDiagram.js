import { Tweenable } from "shifty"
import { defaultNodeWidth } from "./figures";


export const moveAndZoomToShowInnerDiagram = (figure, done) => {
    moveToShowInnerDiagram(figure, () => zoomToShowInnerDiagram(figure, done))
}

const moveToShowInnerDiagram = (figure, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    const zoom = canvas.zoomFactor
    const figureCenter = { x: (figure.x + figure.width / 2) / zoom, y: (figure.y + figure.height / 2) / zoom }
    const canvasCenter = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    const sourcePoint = { x: area.scrollLeft(), y: area.scrollTop() }
    const targetPoint = { x: figureCenter.x - canvasCenter.x, y: figureCenter.y - canvasCenter.y }

    const tweenable = new Tweenable()
    tweenable.tween({
        from: sourcePoint,
        to: targetPoint,
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            area.scrollLeft(state.x)
            area.scrollTop(state.y)
        },
        finish: data => {
            done()
        }
    })
}

const zoomToShowInnerDiagram = (figure, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    const zoom = canvas.zoomFactor
    const targetZoom = 0.2 * figure.width / defaultNodeWidth

    const fc = { x: figure.x + figure.width / 2, y: figure.y + figure.height / 2 }
    const cc = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    const tweenable = new Tweenable()
    tweenable.tween({
        from: { zoom: zoom },
        to: { zoom: targetZoom },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            canvas.setZoom(state.zoom, false)

            // Adjusts scroll figure to center (id needed)
            const tp = { x: fc.x - cc.x * state.zoom, y: fc.y - cc.y * state.zoom }
            area.scrollLeft(tp.x / state.zoom)
            area.scrollTop(tp.y / state.zoom)
        },
        finish: data => {
            done()
        }
    })
}

