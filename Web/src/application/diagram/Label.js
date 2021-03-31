import draw2d from "draw2d";


// Label which has centered text and specified width
export default class Label extends draw2d.shape.basic.Text {
    textWidth = 0
    constructor(textWidth, attr, getter, setter) {
        super(attr, getter, setter)
        this.textWidth = textWidth
    }

    calculateTextAttr() {
        let attr = {
            "text-anchor": "middle",
            "font-size": this.fontSize,
            "font-weight": (this.bold === true) ? "bold" : "normal",
            fill: this.fontColor.rgba(),
            stroke: this.outlineColor.rgba(),
            "stroke-width": this.outlineStroke,
        }
        if (this.fontFamily !== null) {
            attr["font-family"] = this.fontFamily
        }
        return attr
    }

    setTextWidth(textWidth) {
        // Force re-calculate of text width
        this.cachedWrappedAttr = null
        this.textWidth = textWidth
    }

    repaint(attributes) {
        if (this.repaintBlocked === true || this.shape === null) {
            return
        }

        this.svgNodes.attr({ ...this.calculateTextAttr(), ...this.wrappedTextAttr(this.text, this.getWidth() - this.padding.left - this.padding.right) })

        // set of the x/y must be done AFTER the font-size and bold has been set.
        // Reason: the getHeight method needs the font-size for calculation because
        //         it redirects the calculation to the SVG element.
        this.svgNodes.attr({ x: this.getWidth() / 2, y: this.getHeight() / 2 })

        // this is an exception call. Don't call the super method (Label) to avoid
        // the calculation in this method.
        draw2d.SetFigure.prototype.repaint.call(this, attributes)
    }


    wrappedTextAttr(text, width) {
        let words = text.replaceAll('[', ';[').replaceAll(']', '];')
            .replaceAll(';', ' ; ').split(" ")
        if (this.canvas === null || words.length === 0) {
            return { text: text, width: width, height: 20 }
        }

        if (this.cachedWrappedAttr === null) {
            let abc = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
            let svgText = this.canvas.paper.text(0, 0, "").attr({ ...this.calculateTextAttr(), ...{ text: abc } })

            // get a good estimation of a letter width...not correct but this is working for the very first draft implementation
            let letterWidth = svgText.getBBox(true).width / abc.length

            let s = []
            let x = 0
            let w = null
            for (let i = 0; i < words.length; i++) {
                w = words[i]
                let isStart = false

                if (w.startsWith(';')) {
                    w = w.substr(1)
                    if (i != 1) {
                        isStart = true
                    }
                }

                let l = w.length * letterWidth
                if (isStart) {
                    s.push("\n")
                    x = l
                }

                if ((x + l) > this.textWidth) {
                    s.push("\n")
                    x = l
                } else {
                    s.push(" ")
                    x += l
                }

                s.push(w)
            }

            // set the wrapped text and get the resulted bounding box
            svgText.attr({ text: s.join("") })
            let bbox = svgText.getBBox(true)
            svgText.remove()
            this.cachedWrappedAttr = {
                text: s.join(""),
                width: (Math.max(width, bbox.width) + this.padding.left + this.padding.right),
                height: (bbox.height + this.padding.top + this.padding.bottom)
            }
        }
        return this.cachedWrappedAttr
    }
}