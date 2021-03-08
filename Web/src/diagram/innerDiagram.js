import { Tweenable } from "shifty"
import { getInnerDiagram } from "./figures"




export const moveAndZoomEnough = (figure, done) => {
    moveEnough(figure, () => zoomEnough(figure, done))
}

const moveEnough = (figure, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    // Calculate how to scroll from current point to get figure to the center near the top
    const zoom = canvas.zoomFactor
    const sourcePoint = { x: area.scrollLeft() * zoom, y: area.scrollTop() * zoom }
    const targetPoint = getTargetPoint(canvas, figure, zoom)

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


const zoomEnough = (figure, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    const zoom = canvas.zoomFactor
    const targetZoom = getInnerDiagram(figure).innerZoom

    const tweenable = new Tweenable()
    tweenable.tween({
        from: { zoom: zoom },
        to: { zoom: targetZoom },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            canvas.setZoom(state.zoom, false)

            const tp = getTargetPoint(canvas, figure, state.zoom)
            area.scrollLeft(tp.x / state.zoom)
            area.scrollTop(tp.y / state.zoom)
        },
        finish: data => {
            done()
        }
    })
}


export const moveAndZoomToShowOuterDiagram = (figure, targetZoom, targetPoint, marginY, done) => {
    zoomToShowOuterDiagram(figure, targetZoom, marginY,
        () => moveToShowOuterDiagram(figure, targetPoint, marginY, done))
}

const zoomToShowOuterDiagram = (figure, targetZoom, marginY, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    const zoom = canvas.zoomFactor

    const tweenable = new Tweenable()
    tweenable.tween({
        from: { zoom: zoom },
        to: { zoom: targetZoom },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            //console.log('zoom to', state.zoom)
            canvas.setZoom(state.zoom, false)

            const tp = getTargetScrollPoint(canvas, figure, state.zoom, marginY)
            area.scrollLeft(tp.x / state.zoom)
            area.scrollTop(tp.y / state.zoom)
        },
        finish: data => {
            done()
        }
    })
}

const moveToShowOuterDiagram = (figure, target, marginY, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    // Calculate how to scroll from current point to get figure to the center near the top
    const zoom = canvas.zoomFactor
    const sourcePoint = { x: area.scrollLeft() * zoom, y: area.scrollTop() * zoom }
    const targetPoint = { x: target.x * zoom, y: target.y * zoom }

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



export const moveAndZoomToShowInnerDiagram = (figure, marginY, done) => {
    moveToShowInnerDiagram(figure, marginY, () => zoomToShowInnerDiagram(figure, marginY, done))
}

const moveToShowInnerDiagram = (figure, marginY, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    // Calculate how to scroll from current point to get figure to the center near the top
    const zoom = canvas.zoomFactor
    const sourcePoint = { x: area.scrollLeft() * zoom, y: area.scrollTop() * zoom }
    const targetPoint = getTargetScrollPoint(canvas, figure, zoom, marginY)

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



const zoomToShowInnerDiagram = (figure, marginY, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    const zoom = canvas.zoomFactor
    const targetZoom = figure.innerZoom

    const tweenable = new Tweenable()
    tweenable.tween({
        from: { zoom: zoom },
        to: { zoom: targetZoom },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            canvas.setZoom(state.zoom, false)

            const tp = getTargetScrollPoint(canvas, figure, state.zoom, marginY)
            area.scrollLeft(tp.x / state.zoom)
            area.scrollTop(tp.y / state.zoom)
        },
        finish: data => {
            done()
        }
    })
}

const getTargetScrollPoint = (canvas, figure, zoom, marginY) => {
    const figurePoint = { x: figure.x + figure.width / 2, y: figure.y }
    const canvasPoint = { x: (canvas.getWidth() / 2) * zoom, y: (250 - marginY) * zoom }

    return { x: figurePoint.x - canvasPoint.x, y: figurePoint.y - canvasPoint.y }
}

const getTargetPoint = (canvas, figure, zoom) => {
    const figurePoint = { x: figure.x + figure.width / 2, y: figure.y + figure.height / 2 }
    const canvasPoint = { x: (canvas.getWidth() / 2) * zoom, y: (canvas.getHeight() / 2) * zoom }

    return { x: figurePoint.x - canvasPoint.x, y: figurePoint.y - canvasPoint.y }
}