import { Tweenable } from "shifty"
import { defaultGroupNodeWidth } from "./figures";


export const moveAndZoomToShowInnerDiagram = (figure, done) => {
    moveToShowInnerDiagram(figure, () => zoomToShowInnerDiagram(figure, done))
}

const moveToShowInnerDiagram = (figure, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    // Calculate how to scroll from current point to get figure to the center near the top
    const zoom = canvas.zoomFactor
    const sourcePoint = { x: area.scrollLeft() * zoom, y: area.scrollTop() * zoom }
    const targetPoint = getTargetScrollPoint(canvas, figure, zoom)

    // Scroll step by step from source point to target point
    const tweenable = new Tweenable()
    tweenable.tween({
        from: sourcePoint,
        to: targetPoint,
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            area.scrollLeft(state.x / zoom)
            area.scrollTop(state.y / zoom)
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

    const targetZoom = figure.width / defaultGroupNodeWidth

    const tweenable = new Tweenable()
    tweenable.tween({
        from: { zoom: zoom },
        to: { zoom: targetZoom },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            canvas.setZoom(state.zoom, false)

            const tp = getTargetScrollPoint(canvas, figure, state.zoom)
            area.scrollLeft(tp.x / state.zoom)
            area.scrollTop(tp.y / state.zoom)
        },
        finish: data => {
            done()
        }
    })
}

const getTargetScrollPoint = (canvas, figure, zoom) => {
    const figurePoint = { x: figure.x + figure.width / 2, y: figure.y }
    const canvasPoint = { x: zoom * canvas.getWidth() / 2, y: zoom * 250 }

    return { x: figurePoint.x - canvasPoint.x, y: figurePoint.y - canvasPoint.y }
}