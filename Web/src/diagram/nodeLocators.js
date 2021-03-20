import draw2d from "draw2d";


export class LabelLocator extends draw2d.layout.locator.XYRelPortLocator {
    cachedWidth = null
    constructor(y) {
        super(0, y)
    }

    relocate(index, figure) {
        let parent = figure.getParent()
        this.applyConsiderRotation(
            figure,
            parent.getWidth() / 2 - figure.getWidth() / 2,
            parent.getHeight() / 100 * this.y
        )
    }
}


export class InnerDiagramIconLocator extends draw2d.layout.locator.PortLocator {
    relocate(index, figure) {
        const parent = figure.getParent()
        this.applyConsiderRotation(figure, parent.getWidth() / 2 - 8, parent.getHeight() - 23);
    }
}


export class InnerDiagramLocator extends draw2d.layout.locator.Locator {
    relocate(index, target) {
        target.setPosition(2, 2)
    }
}


export class InputTopPortLocator extends draw2d.layout.locator.PortLocator {
    relocate(index, figure) {
        this.applyConsiderRotation(figure, figure.getParent().getWidth() / 2, 0);
    }
}


export class OutputBottomPortLocator extends draw2d.layout.locator.PortLocator {
    relocate(index, figure) {
        var p = figure.getParent();
        this.applyConsiderRotation(figure, p.getWidth() / 2, p.getHeight());
    }
}

