import draw2d from "draw2d";
import PubSub from "pubsub-js";
import cuid from "cuid";
import { menuItem } from "../../common/Menus";
import timing from "../../common/timing";
import Colors from "./Colors";
import CommandChangeIcon from "./CommandChangeIcon";
import NodeIcons from "./NodeIcons";
import Label from "./Label";
import { icons } from "../../common/icons";
import { LabelEditor } from "./LabelEditor";
import NodeGroup from "./NodeGroup";
import NodeSelectionFeedbackPolicy from "./NodeSelectionFeedbackPolicy";
import { Canvas2d, Figure2d } from "./draw2dTypes";
import { FigureDto } from "./StoreDtos";

const defaultIconKey = "Azure/General/Module";

const defaultOptions = (type: string) => {
  const dv = {
    id: cuid(),
    width: Node.defaultWidth,
    height: Node.defaultHeight,
    description: "",
  };

  switch (type) {
    case Node.nodeType:
      return { ...dv, icon: defaultIconKey };
    case Node.systemType:
      return {
        ...dv,
        name: "System",
        icon: "Azure/Compute/CloudServices(Classic)",
      };
    case Node.userType:
      return {
        ...dv,
        name: "External Users",
        icon: "Azure/Management+Governance/MyCustomers",
      };
    case Node.externalType:
      return {
        ...dv,
        name: "External Systems",
        icon: "Azure/Databases/VirtualClusters",
      };
    default:
      throw new Error("Unknown type: " + type);
  }
};

export default class Node extends draw2d.shape.node.Between {
  static nodeType = "node";
  static systemType = "system";
  static userType = "user";
  static externalType = "external";
  static defaultWidth = 230;
  static defaultHeight = 150;

  nodeIcons: NodeIcons = new NodeIcons();
  figure: Figure2d = null;
  type: string;
  colorName: string;
  nameLabel: Figure2d;
  descriptionLabel: Figure2d;
  icon: Figure2d;
  diagramIcon: Figure2d;
  canDelete: boolean = true;

  getName = () => this.nameLabel?.text ?? "";
  getDescription = () => this.descriptionLabel?.text ?? "";

  constructor(type: string = Node.nodeType, options?: any) {
    super({
      id: options?.id ?? cuid(),
      width: 60,
      height: 60,
      stroke: 0.1,
      bgColor: "none",
      color: "none",
      radius: 5,
      glow: true,
      resizeable: false,
    });

    const o = { ...defaultOptions(type), ...options };
    if (!o.name) {
      const ic = icons.getIcon(o.icon);
      o.name = ic.name;
    }

    // const icon = new draw2d.shape.basic.Image({ path: ic.src, width: 22, height: 22, bgColor: 'none' })

    this.type = type;
    this.colorName = o.colorName;

    this.addLabels(o.name, o.description);
    this.addIcon(o.icon);
    // this.addConfigIcon()
    // this.hideConfig()
    this.addPorts();
    //this.addInnerDiagramIcon()

    // this.on("click", (s, e) => console.log('click node'))
    this.on("dblclick", (_s: any, _e: any) => {});
    this.on("resize", (_s: any, _e: any) => this.handleResize());

    this.on("select", () => this.showConfig());
    this.on("unselect", () => this.hideConfig());

    // Adjust selection handle sizes
    this.installEditPolicy(new NodeSelectionFeedbackPolicy());
  }

  setCanvas(canvas: Canvas2d) {
    super.setCanvas(canvas);

    if (canvas != null) {
      this.diagramIcon?.shape?.attr({ cursor: "pointer" });
    }
  }

  static deserialize(data: FigureDto) {
    return new Node(data.type, {
      id: data.id,
      width: data.rect.w,
      height: data.rect.h,
      name: data.name,
      description: data.description,
      colorName: data.color,
      icon: data.icon,
    });
  }

  serialize(): FigureDto {
    return {
      type: this.type,
      id: this.id,
      rect: { x: this.x, y: this.y, w: this.width, h: this.height },
      name: this.getName(),
      description: this.getDescription(),
      color: this.colorName,
      icon: this.iconName,
    };
  }

  getContextMenuItems(_x: number, _y: number) {
    //const hasDiagramIcon = this.diagramIcon != null

    return [
      menuItem("To front", () => this.moveToFront()),
      menuItem("To back", () => this.moveToBack()),
      menuItem("Edit label ...", () => this.nameLabel.editor.start(this)),
      // menuParentItem('Inner diagram', [
      //     menuItem('Show', () => this.showInnerDiagram(), this.innerDiagram == null, hasDiagramIcon),
      //     menuItem('Hide (click)', () => this.hideInnerDiagram(), this.innerDiagram != null, hasDiagramIcon),
      //     menuItem('Edit (dbl-click)', () => this.editInnerDiagram(), true, hasDiagramIcon),
      // ], true, hasDiagramIcon),

      menuItem("Edit icon ...", () =>
        PubSub.publish("nodes.showDialog", {
          add: false,
          action: (iconKey: string) => this.changeIcon(iconKey),
        })
      ),
      //  menuItem('Set default size', () => this.setDefaultSize()),
      menuItem(
        "Delete node",
        () => this.canvas.runCmd(new draw2d.command.CommandDelete(this)),
        this.canDelete
      ),
    ];
  }

  toBack(figure: Figure2d) {
    super.toBack(figure);

    // When node is moved back, all groups should be moved back as well
    this.moveAllGroupsToBack();
    // const group = this.getCanvas()?.group
    // group?.toBack()
  }

  moveAllGroupsToBack() {
    // Get all figures in z order
    const figures = this.canvas.getFigures().clone();
    figures.sort((a: Figure2d, b: Figure2d) => {
      // return 1  if a before b
      // return -1 if b before a
      return a.getZOrder() > b.getZOrder() ? -1 : 1;
    });

    // move all group nodes to back to be behind all nodes
    figures.asArray().forEach((f: Figure2d) => {
      if (f instanceof NodeGroup) {
        f.toBack();
      }
    });
  }

  moveToBack(): void {
    this.toBack(this);
    PubSub.publish("canvas.Save");
  }

  moveToFront(): void {
    this.toFront();
    PubSub.publish("canvas.Save");
  }

  changeIcon(iconKey: string): void {
    this.canvas.runCmd(new CommandChangeIcon(this, iconKey));
  }

  setName(name: string): void {
    this.nameLabel?.setText(name);
  }

  setDescription(description: string): void {
    this.descriptionLabel?.setText(description);
  }

  getAllConnections() {
    return this.getPorts()
      .asArray()
      .flatMap((p: any) => p.getConnections().asArray());
  }

  setDefaultSize(): void {
    this.setWidth(Node.defaultWidth);
    this.setHeight(Node.defaultHeight);
  }

  setNodeColor(colorName: string): void {
    this.colorName = colorName;
    const color = Colors.getNodeColor(colorName);
    const borderColor = Colors.getNodeBorderColor(colorName);
    const fontColor = Colors.getNodeFontColor(colorName);

    this.setBackgroundColor(color);
    this.setColor(borderColor);

    this.nameLabel?.setFontColor(fontColor);
    this.descriptionLabel?.setFontColor(fontColor);
    // this.icon?.setColor(fontColor)
    this.diagramIcon?.setColor(fontColor);
  }

  setDeleteable(flag: boolean) {
    super.setDeleteable(flag);
    this.canDelete = flag;
  }

  setIcon(name: string) {
    if (this.icon != null) {
      this.remove(this.icon);
      this.icon = null;
      this.iconName = null;
    }
    this.addIcon(name);
    this.repaint();
  }

  showInnerDiagram(): void {
    // const t = timing();
    // this.setChildrenVisible(false);
    // const canvasDto = store.tryGetCanvas(
    //   this.getCanvas().diagramId,
    //   this.getId()
    // );
    // if (isError(canvasDto)) {
    //   return;
    // }
    // this.innerDiagram = new InnerDiagramFigure(this, canvasDto);
    // this.innerDiagram.onClick = clickHandler(
    //   () => this.hideInnerDiagram(),
    //   () => this.editInnerDiagram()
    // );
    // this.add(this.innerDiagram, new InnerDiagramLocator());
    // this.repaint();
    // t.log();
  }

  hideInnerDiagram(): void {
    const t = timing();
    if (this.innerDiagram == null) {
      return;
    }

    this.setChildrenVisible(true);
    this.remove(this.innerDiagram);
    this.innerDiagram = null;
    t.log();
  }

  editInnerDiagram(): void {
    if (this.diagramIcon == null) {
      return;
    }

    if (this.innerDiagram == null) {
      this.showInnerDiagram();
    }

    PubSub.publish("canvas.EditInnerDiagram", this);
  }

  handleResize(): void {
    this.nameLabel?.setTextWidth(this.width);
    this.nameLabel?.repaint();
    this.descriptionLabel?.setTextWidth(this.width);
    this.descriptionLabel?.repaint();

    if (this.innerDiagram == null) {
      return;
    }

    this.hideInnerDiagram();
    this.showInnerDiagram();
  }

  setChildrenVisible(isVisible: boolean): void {
    this.nameLabel?.setVisible(isVisible);
    this.descriptionLabel?.setVisible(isVisible);
    this.icon?.setVisible(isVisible);
    this.diagramIcon?.setVisible(isVisible);
  }

  addLabels = (name: string, description: string): void => {
    const fontColor = Colors.labelColor;

    this.nameLabel = new Label(this.width + 40, {
      text: name,
      stroke: 0,
      fontSize: 12,
      fontColor: fontColor,
      bold: true,
    });

    this.nameLabel.installEditor(new LabelEditor(this));
    this.nameLabel.labelLocator = new NodeNameLocator();
    this.add(this.nameLabel, this.nameLabel.labelLocator);

    this.descriptionLabel = new Label(this.width + 40, {
      text: description,
      stroke: 0,
      fontSize: 9,
      fontColor: fontColor,
      bold: false,
    });

    this.descriptionLabel.installEditor(new LabelEditor(this));
    this.descriptionLabel.labelLocator = new NodeDescriptionLocator();
    this.add(this.descriptionLabel, this.descriptionLabel.labelLocator);
  };

  addIcon(iconKey: string): void {
    //console.log('add icon key', iconKey)
    if (iconKey == null) {
      return;
    }

    const ic = icons.getIcon(iconKey);
    const icon = new draw2d.shape.basic.Image({
      path: ic.src,
      width: this.width,
      height: this.height,
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

  addInnerDiagramIcon(): void {
    const iconColor = Colors.getNodeFontColor(this.colorName);
    this.diagramIcon = new draw2d.shape.icon.Diagram({
      width: 15,
      height: 15,
      color: iconColor,
      bgColor: "none",
    });

    this.diagramIcon.on("click", () => this.showInnerDiagram());

    this.add(this.diagramIcon, new InnerDiagramIconLocator());
  }

  addPorts(): void {
    this.createPort("input", new draw2d.layout.locator.XYRelPortLocator(50, 0));
    this.createPort(
      "output",
      new draw2d.layout.locator.XYRelPortLocator(50, 100)
    );

    // this.getPorts().each(function (i, port) {
    //     port.setConnectionAnchor(new draw2d.layout.anchor.FanConnectionAnchor(port));
    // });

    // Make ports larger to support touch
    this.getPorts().each((_i: number, p: Figure2d) => {
      p.setCoronaWidth(5);
      p.setDimension(10);
    });
  }
}

class NodeNameLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, label: Figure2d) {
    const node = label.getParent();
    const x = node.getWidth() / 2 - label.getWidth() / 2;
    const y = node.getHeight() + 0;
    label.setPosition(x, y);
  }
}

class NodeDescriptionLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, label: Figure2d) {
    const node = label.getParent();
    const nameHeight = node.nameLabel.getHeight();
    const x = node.getWidth() / 2 - label.getWidth() / 2;
    const y = node.getHeight() + nameHeight - 8;
    label.setPosition(x, y);
  }
}

class NodeIconLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, icon: Figure2d) {
    icon.setPosition(0, 0);
  }
}

class InnerDiagramIconLocator extends draw2d.layout.locator.PortLocator {
  relocate(_index: number, figure: Figure2d) {
    const parent = figure.getParent();
    this.applyConsiderRotation(figure, 3, parent.getHeight() - 18);
  }
}

class ConfigIconLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, figure: Figure2d) {
    const parent = figure.getParent();
    figure.setPosition(parent.getWidth() - 11, -28);
  }
}

class ConfigBackgroundLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, figure: Figure2d) {
    const parent = figure.getParent();
    figure.setPosition(parent.getWidth() - 13, -30);
  }
}

// class InnerDiagramLocator extends draw2d.layout.locator.Locator {
//   relocate(_index: number, target: Figure2d) {
//     target.setPosition(2, 2);
//   }
// }
