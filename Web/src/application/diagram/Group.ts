import draw2d from "draw2d";
import PubSub from "pubsub-js";
import cuid from "cuid";
import { menuItem } from "../../common/Menus";
import Colors from "./Colors";
import { Canvas2d, Figure2d } from "./draw2dTypes";
import { FigureDto } from "./StoreDtos";

const def = {
  id: cuid(),
  width: 1000,
  height: (1000 * 150) / 230,
  description: "Description",
};

export default class Group extends draw2d.shape.composite.Raft {
  NAME = "Group";

  static groupType = "group";

  type = Group.groupType;
  colorName: string = "none";
  nameLabel: Figure2d;
  descriptionLabel: Figure2d;

  getName = () => this.nameLabel?.text ?? "";
  getDescription = () => this.descriptionLabel?.text ?? "";

  constructor(name = "Group", description = "Description", options?: any) {
    super({
      id: options?.id ?? def.id,
      width: options?.width ?? def.width,
      height: options?.height ?? def.height,
      bgColor: Colors.canvasBackground,
      alpha: 0.5,
      color: Colors.canvasText,
      dasharray: "- ",
      radius: 5,
      stroke: 2,
    });

    this.setDeleteable(false);
    this.addLabels(name, description);
    this.addPorts();

    this.on("click", (_s: any, _e: any) =>
      PubSub.publish("canvas.SetEditMode", false)
    );
    this.on("dblclick", (_s: any, e: any) =>
      PubSub.publish("nodes.showDialog", { add: true, x: e.x, y: e.y })
    );

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
  }

  static deserialize(data: FigureDto) {
    return new Group(data.name, data.description, {
      id: data.id,
      width: data.w,
      height: data.h,
    });
  }

  serialize(): FigureDto {
    return {
      type: this.type,
      id: this.id,
      x: this.x,
      y: this.y,
      w: this.width,
      h: this.height,
      name: this.getName(),
      description: this.getDescription(),
      color: this.colorName,
      hasGroup: false,
    };
  }

  getContextMenuItems(x: number, y: number) {
    // Reuse the canvas context menu
    return [
      ...this.getCanvas().canvas.getContextMenuItems(x, y),
      menuItem("Set default size", () => this.setDefaultSize()),
    ];
  }

  setCanvas(canvas: Canvas2d) {
    super.setCanvas(canvas);

    if (canvas != null) {
      // Group is main node
      canvas.mainNodeId = this.id;
      // Cannot delete main node of canvas
      this.setDeleteable(false);
    }
  }

  setName(name: string) {
    this.nameLabel?.setText(name);
  }

  setDescription(description: string) {
    this.descriptionLabel?.setText(description);
  }

  setDefaultSize() {
    this.setWidth(Group.defaultWidth);
    this.setHeight(Group.defaultHeight);
  }

  addLabels(name: string, description: string) {
    this.nameLabel = new draw2d.shape.basic.Label({
      text: name,
      stroke: 0,
      fontSize: 30,
      fontColor: Colors.canvasText,
      bold: true,
    });
    this.nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
    this.add(this.nameLabel, new GroupNameLocator());

    this.descriptionLabel = new draw2d.shape.basic.Label({
      text: description,
      stroke: 0,
      fontSize: 14,
      fontColor: Colors.canvasText,
      bold: false,
    });
    this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
    this.add(this.descriptionLabel, new GroupDescriptionLocator());
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
  }
}

class GroupNameLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, target: Figure2d) {
    target.setPosition(2, -60);
  }
}

class GroupDescriptionLocator extends draw2d.layout.locator.Locator {
  relocate(_index: number, target: Figure2d) {
    target.setPosition(4, -24);
  }
}
