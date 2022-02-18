import draw2d from "draw2d";
import PubSub from "pubsub-js";
import Colors from "./Colors";

import { Figure2d, Icon2d, Rectangle2d } from "./draw2dTypes";

export interface INodeToolbar {
  showConfig(): void;
  hideConfig(): void;
}

export interface Button {
  icon: Icon2d;
  menu: () => any;
}

interface IconButton {
  button: Figure2d;
  icon: Figure2d;
}

export class NodeToolbar implements INodeToolbar {
  IconButtons: IconButton[] = [];

  constructor(private node: Rectangle2d, private buttons: Button[]) {}

  public showConfig(): void {
    this.IconButtons = this.buttons.map((button, index) =>
      this.toIconButton(button, index)
    );

    this.IconButtons.forEach((buttonIcon) => {
      this.node.add(buttonIcon.button, buttonIcon.button.locator);
      this.node.add(buttonIcon.icon, buttonIcon.icon.locator);
    });

    this.node.repaint();
  }

  private toIconButton(button: Button, index: number): IconButton {
    const x = index * 23;
    const y = -35;

    const buttonRect = new draw2d.shape.basic.Rectangle({
      bgColor: Colors.buttonBackground,
      alpha: 1,
      width: 20,
      height: 20,
      radius: 3,
      stroke: 0.1,
    });
    buttonRect.locator = new IconLocator(x, y);
    buttonRect.on("click", () => this.showMenu(x + 6, y + 5, button.menu));

    const icon = new button.icon({
      width: 16,
      height: 16,
      color: Colors.button,
      bgColor: Colors.buttonBackground,
    });
    icon.locator = new IconLocator(x + 2, y + 2);

    return { button: buttonRect, icon: icon };
  }

  public hideConfig(): void {
    this.IconButtons.forEach((buttonIcon) => {
      this.node.remove(buttonIcon.icon);
      this.node.remove(buttonIcon.button);
    });
    this.IconButtons = [];

    this.node.repaint();
  }

  private showMenu(x: number, y: number, menu: () => any): void {
    const cx = this.node.x + x;
    const cy = this.node.y + y;

    const cc = this.node.canvas.fromCanvasToDocumentCoordinate(cx, cy);

    const menuItems = menu();
    PubSub.publish("canvas.ShowContextMenu", {
      items: menuItems,
      x: cc.x,
      y: cc.y,
    });
  }
}

class IconLocator extends draw2d.layout.locator.Locator {
  constructor(public x: number, public y: number) {
    super();
  }
  relocate(_index: number, figure: Figure2d) {
    figure.setPosition(this.x, this.y);
  }
}
