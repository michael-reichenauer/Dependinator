import draw2d from "draw2d";

// Label which has centered text and specified width
export default class Label extends draw2d.shape.basic.Text {
  textWidth = 0;

  constructor(textWidth: number, attr: any, getter?: any, setter?: any) {
    super(attr, getter, setter);
    this.textWidth = textWidth;
  }

  calculateTextAttr(): any {
    let attr: any = {
      "text-anchor": "middle",
      "font-size": this.fontSize,
      "font-weight": this.bold === true ? "bold" : "normal",
      fill: this.fontColor.rgba(),
      stroke: this.outlineColor.rgba(),
      "stroke-width": this.outlineStroke,
    };
    if (this.fontFamily !== null) {
      attr["font-family"] = this.fontFamily;
    }
    return attr;
  }

  setTextWidth(textWidth: number): void {
    // Force re-calculate of text width
    this.cachedWrappedAttr = null;
    this.textWidth = textWidth;
  }

  repaint(attributes: any): void {
    if (this.repaintBlocked === true || this.shape === null) {
      return;
    }

    this.svgNodes.attr({
      ...this.calculateTextAttr(),
      ...this.wrappedTextAttr(
        this.text,
        this.getWidth() - this.padding.left - this.padding.right
      ),
    });

    // set of the x/y must be done AFTER the font-size and bold has been set.
    // Reason: the getHeight method needs the font-size for calculation because
    //         it redirects the calculation to the SVG element.
    this.svgNodes.attr({ x: this.getWidth() / 2, y: this.getHeight() / 2 });

    // this is an exception call. Don't call the super method (Label) to avoid
    // the calculation in this method.
    draw2d.SetFigure.prototype.repaint.call(this, attributes);
  }

  wrappedTextAttr(text: string, width: number) {
    text = text.trim();
    text = text
      .replaceAll("\n", ";")
      .replaceAll("[", ";[")
      .replaceAll("]", "];");
    if (text.startsWith(";[")) {
      text = text.substr(1);
    }
    if (text.endsWith("];")) {
      text = text.substr(0, text.length - 1);
    }
    // console.log('text', text)

    text = text.replaceAll(";", " \n ");

    let words = text.split(" ");
    if (this.canvas === null || words.length === 0) {
      return { text: text, width: width, height: 20 };
    }

    if (this.cachedWrappedAttr === null) {
      let abc = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
      let svgText = this.canvas.paper
        .text(0, 0, "")
        .attr({ ...this.calculateTextAttr(), ...{ text: abc } });

      // get a good estimation of a letter width...not correct but this is working for the very first draft implementation
      let letterWidth = svgText.getBBox(true).width / abc.length;

      let s = [];
      let x = 0;
      let word = null;
      for (let i = 0; i < words.length; i++) {
        word = words[i];
        let wordWidth = word.length * letterWidth;

        if (word === "\n") {
          s.push("\n");
          x = 0;
        } else if (x + wordWidth > this.textWidth) {
          // Word does not fit, put on next line (unless not first word on line)
          if (x > 0) {
            s.push("\n");
          }
          s.push(word);
          x = wordWidth;
        } else {
          // Word does fit, just add a space before (unless not first word on line)
          if (x > 0) {
            s.push(" ");
            s.push(word);
            x += wordWidth + 1 * letterWidth;
          } else {
            s.push(word);
            x += wordWidth;
          }
        }
      }

      // set the wrapped text and get the resulted bounding box
      svgText.attr({ text: s.join("") });
      let bbox = svgText.getBBox(true);
      svgText.remove();
      this.cachedWrappedAttr = {
        text: s.join(""),
        width:
          Math.max(width, bbox.width) + this.padding.left + this.padding.right,
        height: bbox.height + this.padding.top + this.padding.bottom,
      };
    }
    return this.cachedWrappedAttr;
  }
}
