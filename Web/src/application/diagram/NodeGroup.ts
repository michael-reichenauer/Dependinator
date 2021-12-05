import draw2d from "draw2d";
import cuid from "cuid";
import { menuItem, menuParentItem } from "../../common/Menus";
import Colors from "./Colors";
import { icons } from "../../common/icons";
import CommandChangeIcon from "./CommandChangeIcon";
import PubSub from "pubsub-js";
import { LabelEditor } from "./LabelEditor";
import CommandChangeColor from "./CommandChangeColor";
import { Canvas2d, Figure2d } from "./draw2dTypes";
import { FigureDto } from "./StoreDtos";

const defaultOptions = () => {
  return {
    id: cuid(),
    width: NodeGroup.defaultWidth,
    height: NodeGroup.defaultHeight,
    description: "",
    icon: "Default",
    sticky: false,
    colorName: "None",
  };
};

export default class NodeGroup extends draw2d.shape.composite.Raft {
  //export default class NodeGroup extends draw2d.shape.basic.Rectangle {
  static nodeType = "nodeGroup";
  static defaultWidth = 300;
  static defaultHeight = 200;

  type = NodeGroup.nodeType;
  nameLabel: Figure2d;
  descriptionLabel: Figure2d;
  colorName: string;

  getName = () => this.nameLabel?.text ?? "";
  getDescription = () => this.descriptionLabel?.text ?? "";

  constructor(options?: any) {
    super({
      id: options?.id ?? cuid(),
      stroke: 0.5,
      alpha: 0.4,
      color: Colors.canvasText,
      radius: 5,
      glow: true,
      dasharray: "- ",
    });
    const o = { ...defaultOptions(), ...options };
    const color = Colors.getBackgroundColor(o.colorName);
    this.attr({
      width: o.width,
      height: o.height,
      bgColor: color,
    });

    if (!o.name) {
      const ic = icons.getIcon(o.icon);
      o.name = ic.name;
    }

    this.colorName = o.colorName;
    this.addIcon(o.icon);
    this.addLabels(o.name, o.description);
    this.addPorts();

    // this.on("click", (s, e) => console.log('click node'))
    this.on("dblclick", (_s: any, _e: any) => {});
    this.on("resize", (_s: any, _e: any) => {});

    this.on("select", () => this.showConfig());
    this.on("unselect", () => this.hideConfig());

    // Adjust selection handle sizes
    const selectionPolicy = this.editPolicy.find(
      (p: any) =>
        p instanceof draw2d.policy.figure.RectangleSelectionFeedbackPolicy
    );
    if (selectionPolicy != null) {
      selectionPolicy.createResizeHandle = (owner: any, type: any) => {
        return new draw2d.ResizeHandle({
          owner: owner,
          type: type,
          width: 15,
          height: 15,
        });
      };
    }

    this.getAboardFiguresOrg = this.getAboardFigures;
    if (!o.sticky) {
      this.getAboardFigures = () => new draw2d.util.ArrayList();
    }
  }

  static deserialize(data: FigureDto) {
    return new NodeGroup({
      id: data.id,
      width: data.rect.w,
      height: data.rect.h,
      name: data.name,
      description: data.description,
      colorName: data.color,
      icon: data.icon,
      sticky: data.sticky,
    });
  }

  serialize(): FigureDto {
    const sticky = this.getAboardFigures === this.getAboardFiguresOrg;

    return {
      type: this.type,
      id: this.id,
      rect: { x: this.x, y: this.y, w: this.width, h: this.height },
      name: this.getName(),
      description: this.getDescription(),
      color: this.colorName,
      icon: this.iconName,
      sticky: sticky,
    };
  }

  toggleStickySubItems() {
    if (this.getAboardFigures === this.getAboardFiguresOrg) {
      this.getAboardFigures = () => new draw2d.util.ArrayList();
    } else {
      this.getAboardFigures = this.getAboardFiguresOrg;
    }
    this.canvas.save();
  }

  changeIcon(iconKey: string) {
    this.canvas.runCmd(new CommandChangeIcon(this, iconKey));
  }

  getContextMenuItems(_x: number, _y: number) {
    const colorItems = Colors.backgroundColorNames().map((name) => {
      return menuItem(name, () =>
        this.canvas.runCmd(new CommandChangeColor(this, name))
      );
    });

    const stickyText =
      this.getAboardFigures === this.getAboardFiguresOrg
        ? "Disable sticky sub items"
        : "Enable sticky sub items";
    return [
      menuItem("To front", () => this.moveToFront()),
      menuItem("To back", () => this.moveToBack()),
      menuItem("Edit label ...", () => this.nameLabel.editor.start(this)),
      menuItem("Change icon ...", () =>
        PubSub.publish("nodes.showDialog", {
          add: false,
          group: true,
          action: (iconKey: string) => this.changeIcon(iconKey),
        })
      ),
      menuParentItem("Set background color", colorItems),
      menuItem(stickyText, () => this.toggleStickySubItems()),
      menuItem(
        "Delete node",
        () => this.canvas.runCmd(new draw2d.command.CommandDelete(this)),
        this.canDelete
      ),
    ];
  }

  moveToBack() {
    this.toBack();
    PubSub.publish("canvas.Save");
  }

  moveToFront() {
    this.toFront();
    PubSub.publish("canvas.Save");
  }

  setName(name: string) {
    this.nameLabel?.setText(name);
  }

  setDefaultSize() {
    this.setWidth(NodeGroup.defaultWidth);
    this.setHeight(NodeGroup.defaultHeight);
  }

  setCanvas(canvas: Canvas2d) {
    // Since parent type is a composite, the parent will call toBack().
    // However, we do not want that, so we signal to toBack() to act differently when called
    this.isSetCanvas = true;
    super.setCanvas(canvas);
    this.isSetCanvas = false;
  }

  addPorts() {
    this.createPort("input", new draw2d.layout.locator.XYRelPortLocator(0, 50));
    this.createPort("input", new draw2d.layout.locator.XYRelPortLocator(50, 0));
    this.createPort(
      "output",
      new draw2d.layout.locator.XYRelPortLocator(100, 50)
    );
    this.createPort(
      "output",
      new draw2d.layout.locator.XYRelPortLocator(50, 100)
    );

    // Make ports larger to support touch
    this.getPorts().each((_i: number, p: Figure2d) => {
      p.setCoronaWidth(5);
      p.setDimension(10);
    });
  }

  toBack() {
    if (this.isSetCanvas) {
      // Since parent type is a composite, the parent called toBack() when setCanvas() was called.
      // However, we do not want that, just be back behind all figures, but in front of all groups
      this.moveAllFiguresToFront();
      return;
    }

    super.toBack();
    // const group = this.getCanvas()?.group
    // group?.toBack()
  }

  toFront() {
    super.toFront();

    // When moving group to front, move all figures to front as well to ensure groups are behind
    this.moveAllFiguresToFront();
  }

  setNodeColor(colorName: string) {
    this.colorName = colorName;
    const color = Colors.getBackgroundColor(colorName);
    this.setBackgroundColor(color);
  }

  moveAllFiguresToFront() {
    // Get all figures in z order
    const figures = this.canvas.getFigures().clone();
    figures.sort((a: Figure2d, b: Figure2d) => {
      return a.getZOrder() > b.getZOrder() ? 1 : -1;
    });

    // move all group nodes to back to be behind all nodes
    figures.asArray().forEach((f: Figure2d) => {
      if (!(f instanceof NodeGroup)) {
        f.toFront();
      }
    });
  }

  handleResize(): void {
    this.nameLabel?.setTextWidth(this.width);
    this.nameLabel?.repaint();
  }

  setChildrenVisible(isVisible: boolean): void {
    this.nameLabel?.setVisible(isVisible);
  }

  addLabels = (name: string, description: string): void => {
    this.nameLabel = new draw2d.shape.basic.Label({
      text: name,
      stroke: 0,
      fontSize: 12,
      fontColor: Colors.canvasText,
      bold: true,
    });

    this.nameLabel.installEditor(new LabelEditor(this));
    this.nameLabel.labelLocator = new NodeGroupNameLocator();
    this.add(this.nameLabel, this.nameLabel.labelLocator);

    this.descriptionLabel = new draw2d.shape.basic.Label({
      text: description,
      stroke: 0,
      fontSize: 9,
      fontColor: Colors.canvasText,
      bold: false,
    });

    this.descriptionLabel.installEditor(new LabelEditor(this));
    this.descriptionLabel.labelLocator = new NodeGroupDescriptionLocator();
    this.add(this.descriptionLabel, this.descriptionLabel.labelLocator);
  };

  setIcon(name: string): void {
    if (this.icon != null) {
      this.remove(this.icon);
      this.icon = null;
      this.iconName = null;
    }
    this.addIcon(name);
    this.repaint();
  }

  addIcon(iconKey: string): void {
    //console.log('add icon key', iconKey)
    if (iconKey == null) {
      return;
    }

    const ic = icons.getIcon(iconKey);
    const icon = new draw2d.shape.basic.Image({
      path: ic.src,
      width: 20,
      height: 20,
      bgColor: "none",
    });

    this.iconName = iconKey;
    this.icon = icon;
    this.add(icon, new NodeIconLocator());
  }

  showConfigMenu = (): void => {
    const { x, y } = this.canvas.fromCanvasToDocumentCoordinate(
      this.x + this.getWidth(),
      this.y
    );

    PubSub.publish("canvas.TuneSelected", { x: x - 20, y: y - 20 });
  };

  showConfig(): void {
    const iconColor = Colors.getNodeFontColor(this.colorName);
    this.configIcon = new draw2d.shape.icon.Run({
      width: 16,
      height: 16,
      color: iconColor,
      bgColor: Colors.buttonBackground,
    });
    //this.configIcon.on("click", () => { console.log('click') })

    this.configBkr = new draw2d.shape.basic.Rectangle({
      bgColor: Colors.buttonBackground,
      alpha: 1,
      width: 20,
      height: 20,
      radius: 3,
      stroke: 0.1,
    });
    this.configBkr.on("click", this.showConfigMenu);

    this.add(this.configBkr, new ConfigBackgroundLocator());
    this.add(this.configIcon, new ConfigIconLocator());
    this.repaint();
  }

  hideConfig(): void {
    this.remove(this.configIcon);
    this.remove(this.configBkr);
    this.repaint();
  }
}

class NodeGroupNameLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, label: Figure2d) {
    const node = label.getParent();
    const y = node.getDescription() === "" ? 2 : -3;
    label.setPosition(22, y);
  }
}

class NodeGroupDescriptionLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, label: Figure2d) {
    const node = label.getParent();
    const nameHeight = node.nameLabel.getHeight();
    const x = node.nameLabel.x;
    const y = node.nameLabel.y + nameHeight - 8;
    label.setPosition(x, y);
  }
}

class NodeIconLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, icon: Figure2d) {
    icon.setPosition(3, 3);
  }
}

class ConfigIconLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, figure: Figure2d) {
    const parent = figure.getParent();
    figure.setPosition(parent.getWidth() - 19, -32);
  }
}

class ConfigBackgroundLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, figure: Figure2d) {
    const parent = figure.getParent();
    figure.setPosition(parent.getWidth() - 21, -34);
  }
}
