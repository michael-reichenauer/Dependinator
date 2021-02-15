import draw2d from "draw2d";
import { Tweenable } from "shifty"


export let WheelZoomPolicy = draw2d.policy.canvas.ZoomPolicy.extend(
  {
    NAME: "WheelZoomPolicy",

    init: function () {
      this._super()

      this.center = null
      this.debouncedZoomedCallback = this._debounce(() => {
        let canvas = this.canvas
        if (canvas !== null) {
          canvas.fireEvent("zoomed", { value: canvas.zoomFactor })
        }
        this.center = null
      }, 200)
    },

    onInstall: function (canvas) {
      this._super(canvas)
      canvas.setZoom(1)
      canvas.__wheelZoom = 1
    },

    onUninstall: function (canvas) {
      this._super(canvas)
      delete canvas.__wheelZoom
    },


    onMouseWheel: function (wheelDelta, x, y, shiftKey, ctrlKey) {
      wheelDelta = wheelDelta / 5024

      let newZoom = ((Math.min(10, Math.max(0.1, this.canvas.zoomFactor + wheelDelta)) * 10000 | 0) / 10000)

      //console.log('zoom', newZoom, this.canvas.initialWidth / this.canvas.canvasWidth, this.canvas.initialHeight / this.canvas.canvasHeight,)
      newZoom = Math.min(newZoom, Math.max(this.canvas.initialWidth / this.canvas.canvasWidth, this.canvas.initialHeight / this.canvas.canvasHeight))
      // if (this.canvas.initialHeight * this.)
      // Limit zoom to include all canvas figures
      // let maxZoom = Math.max(1, this.canvas.maxFigureWidth / this.canvas.canvasWidth, this.canvas.maxFigureHeight / this.canvas.canvasHeight)
      // maxZoom = Math.min(10, maxZoom)
      // if (newZoom > maxZoom) {
      //   newZoom = maxZoom
      // }

      // Center zoom around mouse pointer
      if (this.center === null) {
        let client = this.canvas.fromCanvasToDocumentCoordinate(x, y)

        this.center = {
          x: x,
          y: y,
          clientX: client.x,
          clientY: client.y
        }
      }

      this._zoom(newZoom, this.center)
      this.debouncedZoomedCallback()

      return false
    },

    setZoom: function (zoomFactor, animated) {

      // determine the center of the current canvas. We try to keep the
      // current center during zoom operation
      //
      let scrollTop = this.canvas.getScrollTop()
      let scrollLeft = this.canvas.getScrollLeft()
      let scrollWidth = this.canvas.getScrollArea().width()
      let scrollHeight = this.canvas.getScrollArea().height()
      let centerY = scrollTop + (scrollHeight / 2) * this.canvas.zoomFactor
      let centerX = scrollLeft + (scrollWidth / 2) * this.canvas.zoomFactor

      if (animated) {
        let myTweenable = new Tweenable()
        myTweenable.tween({
          from: { 'x': this.canvas.zoomFactor },
          to: { 'x': zoomFactor },
          duration: 300,
          easing: "easeOutSine",
          step: params => {
            this._zoom(params.x, centerX, centerY)
          },
          finish: state => {
            this.debouncedZoomedCallback()
          }
        })
      }
      else {
        this._zoom(zoomFactor, { x: centerX, y: centerY })
        this.debouncedZoomedCallback()
      }
    },

    _zoom: function (zoom, center) {
      let canvas = this.canvas

      if (zoom === canvas.zoomFactor) {
        return
      }


      canvas.zoomFactor = zoom
      //console.log('Zoom', zoom)

      canvas.paper.setViewBox(0, 0, canvas.initialWidth, canvas.initialHeight)
      // Change the width and the height attributes manually through DOM
      // unfortunately the raphaelJS 'setSize' method changes the viewBox as well and this is unwanted in this case
      canvas.html
        .find("svg")
        .attr({
          'width': canvas.initialWidth / zoom,
          'height': canvas.initialHeight / zoom,
        })

      // try to keep the document position to the given client position
      if (center.clientX) {
        let coordsAfter = canvas.fromCanvasToDocumentCoordinate(center.x, center.y)
        canvas.scrollTo(this.canvas.getScrollTop() - (center.clientY - coordsAfter.y), canvas.getScrollLeft() - (center.clientX - coordsAfter.x))
      }

      canvas.fireEvent("zoom", { value: canvas.zoomFactor })
    },


    // Returns a function, that, as long as it continues to be invoked, will not
    // be triggered. The function will be called after it stops being called for
    // N milliseconds. If `immediate` is passed, trigger the function on the
    // leading edge, instead of the trailing.
    _debounce: function (func, wait, immediate) {
      let timeout
      return () => {
        let context = this, args = arguments
        let later = () => {
          timeout = null
          if (!immediate) func.apply(context, args)
        }
        let callNow = immediate && !timeout
        clearTimeout(timeout)
        timeout = setTimeout(later, wait)
        if (callNow) func.apply(context, args)
      }
    }
  })
