import draw2d from "draw2d";

export class Label extends draw2d.shape.basic.Text {
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
            "stroke-width": this.outlineStroke
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

    wrappedTextAttr(text, width) {
        let words = text.split(" ")
        if (this.canvas === null || words.length === 0) {
            return { text: text, width: width, height: 20 }
        }

        if (this.cachedWrappedAttr === null) {
            let abc = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
            let svgText = this.canvas.paper.text(0, 0, "").attr({ ...this.calculateTextAttr(), ...{ text: abc } })

            // get a good estimation of a letter width...not correct but this is working for the very first draft implementation
            let letterWidth = svgText.getBBox(true).width / abc.length

            let s = [words[0]], x = s[0].length * letterWidth
            let w = null
            for (let i = 1; i < words.length; i++) {
                w = words[i]
                let l = w.length * letterWidth
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
            //
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